using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;

// Intentionally no DNLOAD, DETACH, ABORT, CLRSTATUS, reset or vendor request.
// Only cached USB descriptors and incoming DFU GETSTATUS/GETSTATE are exposed.
static class DfuDiagnostics {
 static readonly Guid InterfaceGuid=new Guid("FD7BDAAB-7D76-B4C8-B9BE-209C747294D0");
 [StructLayout(LayoutKind.Sequential)]struct InterfaceData {public int Size;public Guid ClassGuid;public int Flags;public IntPtr Reserved;}
 [StructLayout(LayoutKind.Sequential,Pack=1)]struct Setup {public byte Type,Request;public ushort Value,Index,Length;}
 [StructLayout(LayoutKind.Sequential,Pack=1)]struct UsbInterface {public byte Length,Type,Number,Alternate,Endpoints,Class,Subclass,Protocol,String;}
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr SetupDiGetClassDevs(ref Guid guid,IntPtr enumerator,IntPtr parent,uint flags);
 [DllImport("setupapi.dll",SetLastError=true)]static extern bool SetupDiEnumDeviceInterfaces(IntPtr info,IntPtr device,ref Guid guid,uint index,ref InterfaceData data);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr info,ref InterfaceData data,IntPtr detail,uint size,out uint needed,IntPtr device);
 [DllImport("setupapi.dll")]static extern bool SetupDiDestroyDeviceInfoList(IntPtr info);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
 [DllImport("winusb.dll",SetLastError=true)]static extern bool WinUsb_Initialize(SafeFileHandle file,out IntPtr handle);
 [DllImport("winusb.dll",SetLastError=true)]static extern bool WinUsb_Free(IntPtr handle);
 [DllImport("winusb.dll",SetLastError=true)]static extern bool WinUsb_GetDescriptor(IntPtr handle,byte type,byte index,ushort language,byte[] buffer,uint length,out uint actual);
 [DllImport("winusb.dll",SetLastError=true)]static extern bool WinUsb_ControlTransfer(IntPtr handle,Setup packet,byte[] buffer,uint length,out uint actual,IntPtr overlapped);
 [DllImport("winusb.dll",SetLastError=true)]static extern bool WinUsb_QueryInterfaceSettings(IntPtr handle,byte alternate,out UsbInterface data);
 [DllImport("winusb.dll",SetLastError=true)]static extern bool WinUsb_GetAssociatedInterface(IntPtr handle,byte index,out IntPtr associated);
 [DllImport("winusb.dll",SetLastError=true)]static extern bool WinUsb_SetPipePolicy(IntPtr handle,byte pipe,uint policy,uint size,ref uint value);
 public sealed class Functional {public int interface_number,alternate,protocol,attributes,transfer_size,detach_timeout_ms,dfu_version;public bool can_download,can_upload,manifest_tolerant,will_detach;}
 static void Require(bool ok,string operation){if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error(),operation);}
 static int Word(byte[] b,int at){return b[at]|(b[at+1]<<8);}

 public static List<Functional> ParseConfiguration(byte[] b){
  if(b.Length<9||b[0]!=9||b[1]!=2||Word(b,2)!=b.Length)throw new InvalidDataException("Descriptor de configuración truncado");
  var result=new List<Functional>();int number=-1,alternate=0,protocol=0;bool dfu=false;
  for(int p=0;p<b.Length;){if(p>b.Length-2||b[p]<2||b[p]>b.Length-p)throw new InvalidDataException("Longitud de descriptor USB incorrecta");int n=b[p],type=b[p+1];
   if(type==4){if(n<9)throw new InvalidDataException("Interfaz USB incompleta");number=b[p+2];alternate=b[p+3];dfu=b[p+5]==0xfe&&b[p+6]==1;protocol=b[p+7];}
   if(type==0x21&&dfu){if(n<9)throw new InvalidDataException("Descriptor funcional DFU incompleto");int flags=b[p+2];result.Add(new Functional{interface_number=number,alternate=alternate,protocol=protocol,attributes=flags,transfer_size=Word(b,p+5),detach_timeout_ms=Word(b,p+3),dfu_version=Word(b,p+7),can_download=(flags&1)!=0,can_upload=(flags&2)!=0,manifest_tolerant=(flags&4)!=0,will_detach=(flags&8)!=0});}
   p+=n;
  }return result;
 }
 internal static List<string> Paths(){var result=new List<string>();Guid guid=InterfaceGuid;IntPtr info=SetupDiGetClassDevs(ref guid,IntPtr.Zero,IntPtr.Zero,0x12);if(info==new IntPtr(-1))throw new Win32Exception(Marshal.GetLastWin32Error());
  try{for(uint i=0;;i++){var data=new InterfaceData{Size=Marshal.SizeOf(typeof(InterfaceData))};if(!SetupDiEnumDeviceInterfaces(info,IntPtr.Zero,ref guid,i,ref data)){int error=Marshal.GetLastWin32Error();if(error==259)break;throw new Win32Exception(error);}
   uint needed;SetupDiGetDeviceInterfaceDetail(info,ref data,IntPtr.Zero,0,out needed,IntPtr.Zero);if(needed<6||needed>65536)throw new InvalidDataException("Ruta de interfaz USB fuera de límite");IntPtr detail=Marshal.AllocHGlobal((int)needed);
   try{Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);Require(SetupDiGetDeviceInterfaceDetail(info,ref data,detail,needed,out needed,IntPtr.Zero),"Leer ruta WinUSB");string path=Marshal.PtrToStringUni(IntPtr.Add(detail,4));if(path.IndexOf("vid_1fc9&pid_000c",StringComparison.OrdinalIgnoreCase)>=0)result.Add(path);}finally{Marshal.FreeHGlobal(detail);}
  }}finally{SetupDiDestroyDeviceInfoList(info);}return result;
 }
 static byte[] Descriptor(IntPtr handle,byte type,int size){byte[] b=new byte[size];uint actual;Require(WinUsb_GetDescriptor(handle,type,0,0,b,(uint)b.Length,out actual),"Leer descriptor USB "+type);if(actual!=b.Length)Array.Resize(ref b,(int)actual);return b;}
 static byte[] ReadState(IntPtr handle,int number,byte request){
  if(request!=3&&request!=5)throw new InvalidOperationException("Sólo se permiten GETSTATUS y GETSTATE");
  byte[] buffer=new byte[request==3?6:1];var packet=new Setup{Type=0xa1,Request=request,Index=(ushort)number,Length=(ushort)buffer.Length};uint actual;
  Require(WinUsb_ControlTransfer(handle,packet,buffer,(uint)buffer.Length,out actual,IntPtr.Zero),"DFU lectura "+request);if(actual!=buffer.Length)throw new InvalidDataException("Respuesta DFU incompleta");return buffer;
 }
 public static int Run(string directory,bool inventoryOnly=false){
  Directory.CreateDirectory(directory);string output=Path.Combine(directory,inventoryOnly?"dfu-inventory.json":"dfu-readonly.json");var report=new Dictionary<string,object>{{"time_utc",DateTime.UtcNow.ToString("O")},{"enumeration_only",inventoryOnly},{"USB_interface_opened",false},{"USB_OUT_requests_sent",false},{"firmware_request_sent",false},{"RAM_updater_uploaded",false},{"device_reset_requested",false},{"generic_DFU_ID_does_not_prove_POD_model",true}};int exit=0;
  try{var paths=Paths();report["matching_interfaces"]=paths.Count;if(inventoryOnly){report["status"]=paths.Count==0?"NO_DFU_INTERFACE_IN_WINDOWS":"DFU_INTERFACE_PRESENT_ENUMERATION_ONLY";}else if(paths.Count==0){report["status"]="NO_DFU_DEVICE_PRESENT";exit=2;}else{
   if(paths.Count!=1)throw new InvalidOperationException("Hay varias interfaces DFU NXP; no se abre ninguna");
   using(var file=CreateFile(paths[0],0xc0000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero)){
    if(file.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error(),"Abrir interfaz WinUSB DFU");report["USB_interface_opened"]=true;IntPtr handle;Require(WinUsb_Initialize(file,out handle),"Inicializar WinUSB DFU");
    try{byte[] device=Descriptor(handle,1,18);if(device.Length!=18||Word(device,8)!=0x1fc9||Word(device,10)!=0x000c)throw new InvalidDataException("La identidad USB cambió");report["USB_device_descriptor_hex"]=BitConverter.ToString(device).Replace("-","");
     byte[] head=Descriptor(handle,2,9);if(head.Length<9)throw new InvalidDataException("Sin cabecera de configuración USB");int total=Word(head,2);if(total<9||total>65535)throw new InvalidDataException("Configuración USB fuera de límite");
     byte[] config=Descriptor(handle,2,total);report["USB_configuration_hex"]=BitConverter.ToString(config).Replace("-","");var functions=ParseConfiguration(config);report["DFU_functions"]=functions;var readings=new List<Dictionary<string,object>>();
     foreach(var fn in functions){if(fn.alternate!=0)continue;IntPtr target=handle;bool extra=false;UsbInterface selected;Require(WinUsb_QueryInterfaceSettings(handle,0,out selected),"Consultar interfaz WinUSB");
      if(selected.Number!=fn.interface_number){target=IntPtr.Zero;for(byte at=0;at<16;at++){IntPtr candidate;if(!WinUsb_GetAssociatedInterface(handle,at,out candidate))break;UsbInterface other;if(WinUsb_QueryInterfaceSettings(candidate,0,out other)&&other.Number==fn.interface_number){target=candidate;extra=true;break;}WinUsb_Free(candidate);}if(target==IntPtr.Zero){readings.Add(new Dictionary<string,object>{{"interface",fn.interface_number},{"error","No se encuentra el handle de esta interfaz; no se envían controles"}});continue;}}
      try{uint timeout=5000;Require(WinUsb_SetPipePolicy(target,0,3,4,ref timeout),"Configurar timeout local del handle");byte[] status=ReadState(target,fn.interface_number,3),state=ReadState(target,fn.interface_number,5);readings.Add(new Dictionary<string,object>{{"interface",fn.interface_number},{"GETSTATUS_hex",BitConverter.ToString(status).Replace("-","")},{"GETSTATE_hex",BitConverter.ToString(state).Replace("-","")},{"bStatus",status[0]},{"bState_status",status[4]},{"poll_timeout_ms",status[1]|status[2]<<8|status[3]<<16},{"bState",state[0]}});}
      finally{if(extra)WinUsb_Free(target);}
     }report["readings"]=readings;report["status"]="READ_ONLY_DFU_DIAGNOSTICS_COMPLETE";
    }finally{WinUsb_Free(handle);}
   }
  }}catch(Exception e){report["status"]="DFU_DIAGNOSTIC_ERROR";report["error"]=e.Message;var win=e as Win32Exception;if(win!=null)report["Win32_error"]=win.NativeErrorCode;exit=1;}
  var serializer=new JavaScriptSerializer();
  if(!inventoryOnly){try{var saved=serializer.DeserializeObject(serializer.Serialize(report)) as Dictionary<string,object>;report["preflight_assessment"]=DfuAssessment.Analyze(saved);}catch(Exception e){report["assessment_error"]=e.Message;}}
  File.WriteAllText(output,serializer.Serialize(report));Console.WriteLine(output);return exit;
 }
}
