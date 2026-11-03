using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

// One official native engine hosted by this app; no Central/editor process.
// No getter for currentDeviceID (it stalled in the earlier POD query).
sealed class EngineSession {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool SetDllDirectory(string path);
 static EngineSession active;
 readonly BlockingCollection<Action> queue=new BlockingCollection<Action>();
 readonly List<Delegate> roots=new List<Delegate>();object engine;Type type;Thread thread;
 readonly Action<string> log;readonly Action changed;readonly Action<double> progress;
 readonly object sync=new object();List<int> ids=new List<int>();int selectedId;bool running,transferring,completed,selectionReady,disconnecting;
 string selectedVersion="",lastFailure="",sessionHash="";FirmwareFile selectedFirmware;ManualResetEvent terminal;int ownerThreadId;volatile bool usbClosed=true;
 public bool UsbClosed{get{return usbClosed;}}
 public bool Running{get{lock(sync)return running;}} public bool Busy{get{lock(sync)return transferring;}}
 public bool IsAlive{get{return thread!=null&&thread.IsAlive;}}
 public bool TargetReady{get{lock(sync)return running&&selectionReady&&selectedId==FirmwareFile.Product;}}
 public string DeviceText{get{lock(sync)return TargetReady?"POD Go · "+(selectedVersion==""?"versión pendiente":selectedVersion):running?"Buscando POD Go…":"Sin conectar";}}
 public EngineSession(Action<string> logger,Action notification,Action<double> progressHandler){log=logger;changed=notification;progress=progressHandler;}
 object Call(string method,params object[] args){if(ownerThreadId==0||Thread.CurrentThread.ManagedThreadId!=ownerThreadId)throw new InvalidOperationException("Llamada SDK fuera de su hilo propietario: "+method);try{return type.GetMethod(method).Invoke(engine,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 void Post(Action action){queue.Add(()=>{try{action();}catch(Exception e){log("ERROR "+e);changed();}});}
 public void Start(){
  if(thread!=null&&thread.IsAlive)return;
  thread=new Thread(()=>{try{
   ownerThreadId=Thread.CurrentThread.ManagedThreadId;
   PortableRuntime.Prepare();string home=Path.Combine(PortableRuntime.Root,"engine");SetDllDirectory(home);
   AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string p=Path.Combine(home,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
   type=Assembly.LoadFile(Path.Combine(home,"EngineCLIWrapper.dll")).GetType("EngineCLIWrapper.EngineWrapper",true);active=this;
   foreach(string signal in new[]{"SendFloat","sendInt","sendBool","SendString","SendStringArray","SendDictionary"}){
    var ev=type.GetEvent(signal);var method=typeof(EngineSession).GetMethod("Receive_"+signal,BindingFlags.NonPublic|BindingFlags.Static);
    Delegate callback=Delegate.CreateDelegate(ev.EventHandlerType,method);roots.Add(callback);ev.AddEventHandler(null,callback);
   }
   engine=Activator.CreateInstance(type);usbClosed=false;bool initialized=(bool)Call("initialize","1.6");if(!initialized)throw new IOException("El motor no pudo inicializarse");bool started=(bool)Call("start");log("Motor integrado: initialize="+initialized+"; start="+started+"; hilo="+ownerThreadId);if(!started)throw new IOException("El motor no pudo iniciar los trabajadores");
   lock(sync)running=true;changed();Thread.Sleep(3500);RefreshDevices();
   foreach(var action in queue.GetConsumingEnumerable())action();
  }catch(Exception e){log("ERROR MOTOR "+e);if(engine!=null){try{Call("setStringParamValue","systemState","tearDownEngine","");Call("shutDown");usbClosed=true;}catch(Exception close){log("ERROR CIERRE "+close.Message);}}Unsubscribe();lock(sync){running=false;selectionReady=false;selectedId=0;}changed();}});
  thread.IsBackground=true;thread.SetApartmentState(ApartmentState.STA);thread.Start();
 }
 public void Refresh(){if(Running)Post(RefreshDevices);}
 void RefreshDevices(){
  var listing=Call("getDictionaryParamValue","systemState","deviceListData") as Dictionary<string,object>;
  if(listing!=null)OnDictionary("systemState","deviceListData",listing,false);
  List<int> current;lock(sync)current=ids.ToList();
  if(current.Count(id=>id==FirmwareFile.Product)>1)throw new InvalidOperationException("Hay varias POD Go conectadas. Deja sólo una para identificarla inequívocamente.");
  int index=current.IndexOf(FirmwareFile.Product);
  if(index<0){lock(sync){selectionReady=false;selectedId=0;}log("No se selecciona ningún otro equipo. POD Go no aparece con su identidad normal.");changed();return;}
  if(!TargetReady){int rc=(int)Call("setIntParamValue","systemState","currentDeviceIndex",index);log("Seleccionar POD Go: "+rc);if(rc!=0)throw new IOException("No se pudo seleccionar POD Go");lock(sync){selectedId=FirmwareFile.Product;selectionReady=true;}}
  changed();
 }
 public void IdentifyRecovery(){if(!Running)return;Post(()=>{
  RefreshDevices();List<int> current;lock(sync)current=ids.ToList();
  var boot=current.Select((id,index)=>new{ID=id,Index=index}).Where(v=>IsPodRecoveryId(v.ID)).ToList();
  if(boot.Count!=1)throw new InvalidOperationException("No hay una identidad de recuperación POD Go inequívoca. Se conserva el diagnóstico; no se identifica otro modelo.");
  var request=new Dictionary<string,object>{{"setDeviceIdentity_deviceIndexKey",boot[0].Index},{"setDeviceIdentity_currentDeviceProductIDKey",boot[0].ID},{"setDeviceIdentity_newDeviceProductIDKey",FirmwareFile.Product},{"setDeviceIdentity_newDeviceFriendlyNameKey","POD GO"}};
  log("Identificar recuperación POD Go: "+Call("setDictionaryParamValue","stepsSystemState","setDeviceIdentity",request));Thread.Sleep(1000);RefreshDevices();
 });}
 public static bool IsPodRecoveryId(int id){return ((uint)id>>16)==0x21&&id!=FirmwareFile.Product&&id!=2162696;}
 public void Install(FirmwareFile file,string backup){
  if(!TargetReady||Busy)throw new InvalidOperationException("Conexión POD Go no verificada o motor ocupado");
  if(file.IsWithdrawn)throw new InvalidOperationException("El candidato v4 está retirado tras el fallo físico");
  if(!File.Exists(backup))throw new IOException("Falta el backup conservado");
  FirmwareFile.PreserveBackup(backup,Path.GetDirectoryName(backup));
  selectedFirmware=file;sessionHash=file.Hash;terminal=new ManualResetEvent(false);completed=false;lastFailure="";
  lock(sync)transferring=true;changed();
  Post(()=>{try{
   RefreshDevices();if(!TargetReady)throw new IOException("POD Go dejó de estar disponible antes de la operación");
   List<int> current;lock(sync)current=ids.ToList();int index=current.IndexOf(FirmwareFile.Product);if(index<0||(int)Call("setIntParamValue","systemState","currentDeviceIndex",index)!=0)throw new IOException("No se pudo reafirmar la identidad POD Go");
   var fresh=FirmwareFile.Read(file.PathName);if(fresh.Hash!=sessionHash)throw new IOException("El firmware cambió después de validarlo");
   log("INICIO ACTUALIZACIÓN SHA256="+sessionHash);
   var request=new Dictionary<string,object>{{"updateDeviceFirmwareOffline_imagePath",fresh.PathName},{"updateDeviceFirmwareOffline_stepsPath",""}};
   int rc=(int)Call("setDictionaryParamValue","systemState","updateDeviceFirmwareOffline",request);log("Preparación del motor="+rc);if(rc!=0)throw new IOException("El motor rechazó iniciar: "+rc);
   // Drain callbacks on the owner STA after the native start call returns.
   // Never terminate an active flash on a timer.
   while(!terminal.WaitOne(100)){Action pending;if(queue.TryTake(out pending,100))pending();}
   log(completed?"TRANSFERENCIA COMPLETADA. Arranque y audio todavía pendientes de comprobar.":"ACTUALIZACIÓN FALLIDA: "+lastFailure+". No se conoce cuántos bytes se grabaron.");
  }finally{lock(sync)transferring=false;selectedFirmware=null;changed();}});
 }
 public void Disconnect(){lock(sync){if(transferring)throw new InvalidOperationException("Hay una transferencia en curso");if(!running||disconnecting)return;disconnecting=true;}Post(()=>{
  log("Cierre de trabajadores="+Call("setStringParamValue","systemState","tearDownEngine",""));Call("shutDown");usbClosed=true;
  foreach(var callback in roots)GC.KeepAlive(callback);Unsubscribe();lock(sync){running=false;selectionReady=false;selectedId=0;}queue.CompleteAdding();log("Motor cerrado ordenadamente");changed();
 });}
 void Unsubscribe(){if(type!=null&&roots.Count==6){int i=0;foreach(string name in new[]{"SendFloat","sendInt","sendBool","SendString","SendStringArray","SendDictionary"})type.GetEvent(name).RemoveEventHandler(null,roots[i++]);roots.Clear();}if(active==this)active=null;}
 static bool Sensitive(string p){return new[]{"password","token","secret","serial","authentication","cookie"}.Any(s=>p.IndexOf(s,StringComparison.OrdinalIgnoreCase)>=0);}
 string Render(object value){if(value==null)return "null";var seq=value as IEnumerable;if(seq!=null&&!(value is string))return "["+string.Join(",",seq.Cast<object>().Select(Render).ToArray())+"]";return value.ToString();}
 void Event(string g,string p,object v,bool device){
  string text=Sensitive(p)?"<omitido>":Render(v);
  string failure=Busy?FirmwareSignals.Failure(p,v,text):null;
  string phase=Busy?FirmwareSignals.Phase(p):null;
  if(p=="engineUpdateProcessClientStep_displayEula"||text.IndexOf("<html",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("<!DOCTYPE",StringComparison.OrdinalIgnoreCase)>=0)text="<HTML omitido>";
  log((device?"DEVICE ":"ENGINE ")+g+"/"+p+"="+text);
  if(phase!=null)log("FASE "+phase);
  lock(sync){
   if(p=="currentDeviceID"){selectedId=Convert.ToInt32(v);selectionReady=selectedId==FirmwareFile.Product;}
   if(p=="currentDeviceFlashFirmwareVersionString")selectedVersion=Convert.ToString(v);
  }
  // engineProgress is already in percentage points (0..100), including values
  // below 1. Other per-stage counters need not share that unit.
  if(p=="engineProgress"&&(v is float||v is double))progress(Math.Max(0,Math.Min(100,Convert.ToDouble(v))));
  if(Busy&&(p=="engineUpdateProcessCompleted"||p=="engineUpdateProcessPostUpdateDeviceArrived")){completed=true;terminal.Set();}
  if(failure!=null){completed=false;lastFailure=g+"/"+p+": "+failure;log("FALLO "+lastFailure);terminal.Set();}
  if(Busy&&p=="engineUpdateProcessClientStep_displayEula"){
   // A synchronous EULA callback must not reenter SDK while its start call runs.
   // The user's Install action authorizes this exact revalidated file.
   Post(()=>{try{var captured=selectedFirmware;if(captured==null||new FileInfo(captured.PathName).Length!=captured.Bytes||FirmwareFile.Sha(File.ReadAllBytes(captured.PathName))!=sessionHash)throw new IOException("Archivo cambiado antes de la transferencia");
    log("Aceptar instalación en hilo propietario="+Thread.CurrentThread.ManagedThreadId);
    log("Continuar instalación autorizada="+Call("setStringParamValue","engineUpdateProcessClientResponse","engineUpdateProcessClientResponse_eulaAccepted",""));
   }catch(Exception e){lastFailure="No se continúa la transferencia: "+e.Message;terminal.Set();changed();}});
  }
  changed();
 }
 void OnDictionary(string g,string p,Dictionary<string,object> data,bool d){
  if(data==null){log("Diccionario vacío: "+g+"/"+p);return;}
  if(p=="deviceListData"&&data.ContainsKey("deviceListData_IdentifiersKey"))lock(sync){
   ids=((IEnumerable)data["deviceListData_IdentifiersKey"]).Cast<object>().Select(Convert.ToInt32).ToList();
   int at=ids.IndexOf(FirmwareFile.Product);
   if(at<0){selectionReady=false;selectedId=0;}else if(data.ContainsKey("deviceListData_flashVersionStringsKey")){
    var versions=((IEnumerable)data["deviceListData_flashVersionStringsKey"]).Cast<object>().Select(Convert.ToString).ToList();if(at<versions.Count)selectedVersion=versions[at];
   }
  }
  foreach(var item in data)Event(g,p+"/"+item.Key,item.Value,d);changed();
 }
 static void Receive_SendFloat(string g,string p,float v,bool d){if(active!=null)active.Event(g,p,v,d);}
 static void Receive_sendInt(string g,string p,int v,bool d){if(active!=null)active.Event(g,p,v,d);}
 static void Receive_sendBool(string g,string p,bool v,bool d){if(active!=null)active.Event(g,p,v,d);}
 static void Receive_SendString(string g,string p,string v,bool d){if(active!=null)active.Event(g,p,v,d);}
 static void Receive_SendStringArray(string g,string p,List<string> v,bool d){if(active!=null)active.Event(g,p,v,d);}
 static void Receive_SendDictionary(string g,string p,Dictionary<string,object> v,bool d){if(active!=null)active.OnDictionary(g,p,v,d);}
}
