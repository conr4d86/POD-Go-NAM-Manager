using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;

static class SelfTests {
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 static void Reject(string name,byte[] data,string directory,List<string> cases){string path=Path.Combine(directory,name+".hxf");File.WriteAllBytes(path,data);bool rejected=false;try{FirmwareFile.Read(path);}catch{rejected=true;}Check(rejected,"Caso negativo aceptado: "+name);cases.Add(name+" rechazado");}
 public static int Run(string[] args){
  if(args.Length!=5)throw new ArgumentException("--self-test <official> <failed-v4> <backup> <output>");
  string directory=Path.GetFullPath(args[4]);Directory.CreateDirectory(directory);var cases=new List<string>();
  var original=FirmwareFile.Read(args[1]);Check(original.IsOfficial&&!original.IsWithdrawn&&original.ImageCount==17,"Original no verificado");cases.Add("HXF oficial: 17 CRC/Fletcher, MD5 y Adler32");
  var v4=FirmwareFile.Read(args[2]);Check(v4.IsWithdrawn&&!v4.IsOfficial,"v4 no retirado");cases.Add("v4 válido en formato, retirado para instalar");
  byte[] wrong=File.ReadAllBytes(args[1]);wrong[31]^=1;Reject("otro-modelo",wrong,directory,cases);
  byte[] corrupt=File.ReadAllBytes(args[1]);corrupt[corrupt.Length-10]^=1;Reject("datos-alterados",corrupt,directory,cases);
  byte[] truncated=new byte[128];Buffer.BlockCopy(File.ReadAllBytes(args[1]),0,truncated,0,truncated.Length);Reject("truncado",truncated,directory,cases);
  string oversized=Path.Combine(directory,"demasiado-grande.hxf");using(var output=new FileStream(oversized,FileMode.Create,FileAccess.Write))output.SetLength(128L*1024*1024+1);bool sizeRejected=false;try{FirmwareFile.Read(oversized);}catch(InvalidDataException){sizeRejected=true;}Check(sizeRejected,"Archivo enorme aceptado");cases.Add("Límite de tamaño aplicado antes de leer el HXF");
  string backup=FirmwareFile.PreserveBackup(args[3],Path.Combine(directory,"backups"));Check(FirmwareFile.Sha(File.ReadAllBytes(backup))==FirmwareFile.Sha(File.ReadAllBytes(args[3])),"Backup alterado");cases.Add("Copia del PGB idéntica por SHA256");
  Check(!EngineSession.IsPodRecoveryId(2424833)&&!EngineSession.IsPodRecoveryId(2428928)&&!EngineSession.IsPodRecoveryId(2162696)&&EngineSession.IsPodRecoveryId(0x211000),"Selección de otro modelo permitida");cases.Add("Filtro de identidad POD excluye otros modelos");
  PortableRuntime.Prepare();Check(File.Exists(PortableRuntime.Custom)&&FirmwareFile.Sha(File.ReadAllBytes(PortableRuntime.Custom))==PortableRuntime.CustomHash&&!Directory.Exists(Path.Combine(PortableRuntime.Root,"official")),"Paquete custom incorrecto o contiene el original");cases.Add("Custom integrado verificado; firmware oficial ausente; SDK sin iniciar");
  var steps=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(PortableRuntime.Root,"engine","res","PODGOSteps.json"))) as Dictionary<string,object>;
  var update=(Dictionary<string,object>)steps["updateData"];var fail=(Dictionary<string,object>)update["updateFailed"];string signal="engineUpdateProcessClientStep_"+(string)fail["clientAction"];
  Check(FirmwareSignals.Failure(signal,fail["HTML"],(string)fail["HTML"])!=null,"Se pierde el fallo HTML real de PODGOSteps");
  Check(FirmwareSignals.Failure(signal,"<html>Update Succeeded</html>","<html>Update Succeeded</html>")==null,"Éxito confundido con fallo");cases.Add("Fallo HTML real del SDK reconocido antes de ocultarlo; éxito distinguido");
  Check(FirmwareSignals.Failure("engineUpdateProcessCancelled","","")!=null&&FirmwareSignals.Failure("firmwareUpdateState",3,"3").IndexOf("USB",StringComparison.Ordinal)<0,"Cancelación/estado genérico mal clasificados");cases.Add("Cancelación terminal y estado 3 sin atribuir causa USB");
  byte[] config={9,2,27,0,1,1,0,0x80,50,9,4,0,0,0,0xfe,1,2,0,9,0x21,0x0d,0xe8,3,0,4,0x10,1};var functions=DfuDiagnostics.ParseConfiguration(config);Check(functions.Count==1&&functions[0].can_download&&!functions[0].can_upload&&functions[0].transfer_size==1024&&functions[0].protocol==2,"Descriptor DFU mal interpretado");cases.Add("Descriptor DFU: capacidades, interfaz y tamaño de transferencia");
  config[18]=0;bool badDescriptor=false;try{DfuDiagnostics.ParseConfiguration(config);}catch(InvalidDataException){badDescriptor=true;}Check(badDescriptor,"Longitud cero de descriptor aceptada");cases.Add("Descriptor USB de longitud cero rechazado sin bucle");
  var report=new{status="PASS_LOCAL_ONLY",cases=cases,USB_opened=false,SDK_started=false,firmware_written=false,hardware_validated=false};
  File.WriteAllText(Path.Combine(directory,"self-test.json"),new JavaScriptSerializer().Serialize(report));Console.WriteLine("PASS_LOCAL_ONLY "+cases.Count+" casos; sin USB/SDK");return 0;
 }
}
