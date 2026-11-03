using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;

// Pure report interpretation: no SDK, P/Invoke, device enumeration or transfers.
// The original worker reduces several distinct failures to notification (5,0).
// A saved preflight report describes its own instant, not a past installation.
static class DfuAssessment {
 static int Number(Dictionary<string,object> value,string key,int fallback=-1){object found;return value.TryGetValue(key,out found)?Convert.ToInt32(found):fallback;}
 static bool Flag(Dictionary<string,object> value,string key){object found;return value.TryGetValue(key,out found)&&found is bool&&(bool)found;}
 static string Text(Dictionary<string,object> value,string key){object found;return value.TryGetValue(key,out found)?Convert.ToString(found):"";}
 static object[] Rows(Dictionary<string,object> value,string key){object found;return value.TryGetValue(key,out found)&&found is object[]?(object[])found:new object[0];}
 static byte[] Hex(string text,int length){if(text.Length!=length*2)throw new InvalidDataException("Respuesta DFU guardada incompleta");byte[] data=new byte[length];for(int i=0;i<length;i++)data[i]=Convert.ToByte(text.Substring(2*i,2),16);return data;}
 static void Add(List<object> findings,string code,string detail,int? nativeCode=null){findings.Add(new{code=code,detail=detail,related_native_code=nativeCode});}
 public static Dictionary<string,object> Analyze(Dictionary<string,object> report){
  var findings=new List<object>();int functions=0,readings=0;
  if(Flag(report,"USB_OUT_requests_sent")||Flag(report,"firmware_request_sent")||Flag(report,"device_reset_requested"))Add(findings,"INPUT_IS_NOT_READ_ONLY","El informe de entrada declara una acción de salida; no es el diagnóstico de lectura previsto.");
  if(Number(report,"matching_interfaces")==0)Add(findings,"NO_DFU_INTERFACE","El informe no encontró interfaz DFU; no se puede inferir el estado del equipo.");
  if(Number(report,"matching_interfaces")>1)Add(findings,"AMBIGUOUS_DFU_INTERFACE","El informe contiene varias interfaces NXP; no identifica una POD Go inequívocamente.");
  if(Text(report,"status")=="DFU_DIAGNOSTIC_ERROR")Add(findings,"READ_FAILED","Falló la lectura del diagnóstico: "+Text(report,"error")+"; Win32="+Number(report,"Win32_error")+". El código no identifica por sí solo la causa del intento anterior.");
  foreach(object item in Rows(report,"DFU_functions")){
   var fn=item as Dictionary<string,object>;if(fn==null)throw new InvalidDataException("Interfaz DFU guardada incorrecta");
   if(Number(fn,"alternate")!=0)continue;functions++;
   if(!Flag(fn,"can_download"))Add(findings,"CAN_DOWNLOAD_DISABLED","El descriptor observado no anuncia DNLOAD. El paquetizador nativo devuelve -3 con esa capacidad ausente.",-3);
   if(Number(fn,"transfer_size")<=0||Number(fn,"transfer_size")>65535)Add(findings,"INVALID_TRANSFER_SIZE","El tamaño de transferencia no es válido para la rutina nativa de 16 bits.");
   if(Number(fn,"interface_number")!=0)Add(findings,"NATIVE_INTERFACE_MISMATCH","La interfaz DFU observada no es 0; la ruta original analizada envía wIndex=0. Falta validar la selección de interfaz.");
   if(Number(fn,"protocol")!=2)Add(findings,"NOT_DFU_MODE_PROTOCOL","El descriptor anuncia protocolo "+Number(fn,"protocol")+" en vez de 2. Es una diferencia respecto a la especificación; no demuestra por sí sola una incompatibilidad ni explica el fallo anterior. No se ha probado una transferencia física.");
  }
  foreach(object item in Rows(report,"readings")){
   var reading=item as Dictionary<string,object>;if(reading==null)throw new InvalidDataException("Estado DFU guardado incorrecto");
   if(Text(reading,"error")!=""){Add(findings,"INTERFACE_READ_FAILED",Text(reading,"error"));continue;}
   byte[] status=Hex(Text(reading,"GETSTATUS_hex"),6),state=Hex(Text(reading,"GETSTATE_hex"),1);
   int timeout=status[1]|status[2]<<8|status[3]<<16;
   if(Number(reading,"bStatus")!=status[0]||Number(reading,"bState_status")!=status[4]||Number(reading,"bState")!=state[0]||Number(reading,"poll_timeout_ms")!=timeout)throw new InvalidDataException("Los campos DFU no coinciden con sus respuestas hex guardadas");
   readings++;
   if(status[0]!=0)Add(findings,"DFU_STATUS_ERROR","GETSTATUS observa bStatus="+status[0]+". El paquetizador nativo rechaza un status no nulo después de un envío.",-9209);
   if(status[4]==10||state[0]==10)Add(findings,"DFU_ERROR_STATE","Se observó dfuERROR en el informe. No se envían CLRSTATUS, ABORT ni reset.",-9209);
   else if(status[4]!=2||state[0]!=2)Add(findings,"DFU_NOT_IDLE","El informe observa un estado distinto de dfuIDLE antes del envío: "+status[4]+"/"+state[0]+".");
   if(status[4]!=state[0])Add(findings,"STATE_CHANGED_BETWEEN_READS","GETSTATUS y GETSTATE observaron estados distintos; la lectura no es una instantánea atómica.");
  }
  if(functions==0||readings==0)Add(findings,"INCOMPLETE_READ_ONLY_REPORT","Faltan descriptor DFU de alternate 0 o respuestas GETSTATUS/GETSTATE completas.");
  if(functions>0&&readings>0&&(Number(report,"matching_interfaces")!=1||!Flag(report,"USB_interface_opened")))Add(findings,"INCOMPLETE_INTERFACE_CONTEXT","El informe no confirma una única interfaz abierta durante la lectura guardada.");
  if(findings.Count==0&&Text(report,"status")!="READ_ONLY_DFU_DIAGNOSTICS_COMPLETE")Add(findings,"REPORT_NOT_COMPLETE","El informe no declara una lectura completa.");
  return new Dictionary<string,object>{{"status",findings.Count==0?"NO_BLOCKER_OBSERVED_IN_SAVED_PREFLIGHT":"SAVED_PREFLIGHT_HAS_FINDINGS"},{"findings",findings},{"DFU_functions_analyzed",functions},{"readings_analyzed",readings},{"analysis_only",true},{"USB_accessed_by_analysis",false},{"SDK_started_by_analysis",false},{"past_update_failure_identified",false},{"transfer_acceptance_verified",false},{"ready_for_flashing",false}};
 }
 public static int Run(string input,string directory){
  var info=new FileInfo(input);if(info.Length<=0||info.Length>1024*1024)throw new InvalidDataException("Informe fuera de límite");
  byte[] bytes=File.ReadAllBytes(input);var serializer=new JavaScriptSerializer();Directory.CreateDirectory(directory);string output=Path.Combine(directory,"dfu-assessment.json");
  try{
   var report=serializer.DeserializeObject(System.Text.Encoding.UTF8.GetString(bytes)) as Dictionary<string,object>;
   if(report==null)throw new InvalidDataException("No es un informe JSON");var result=Analyze(report);result["source_report_sha256"]=FirmwareFile.Sha(bytes);
   File.WriteAllText(output,serializer.Serialize(result));Console.WriteLine(output);return 0;
  }catch(Exception e){File.WriteAllText(output,serializer.Serialize(new{status="INVALID_SAVED_REPORT",error=e.Message,source_report_sha256=FirmwareFile.Sha(bytes),analysis_only=true,USB_accessed_by_analysis=false,SDK_started_by_analysis=false,ready_for_flashing=false}));throw;}
 }
}
