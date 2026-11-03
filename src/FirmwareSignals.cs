using System;

// Signals come from the installed PODGOSteps definition and the SDK callbacks.
// They report the engine's state; none identifies an underlying USB fault alone.
static class FirmwareSignals {
 public static string Failure(string parameter,object value,string rendered){
  if(parameter=="firmwareUpdateState"&&value is int&&(int)value==3)return "firmwareUpdateState=3 (UPDATE_FAILED; causa pendiente)";
  if(parameter=="firmwareUpdateFailed"||parameter=="engineUpdateProcessFailed"||parameter=="engineUpdateProcessClientStep_updateFailed")return parameter;
  if(parameter=="engineUpdateProcessCancelled")return "Actualización cancelada por el motor";
  if(parameter.StartsWith("engineUpdateProcessClientStep_displayStaticInfoViewDismiss",StringComparison.Ordinal)&&
     rendered.IndexOf("Firmware Update Failed",StringComparison.OrdinalIgnoreCase)>=0)
   return "El motor muestra Firmware Update Failed (sin código de causa)";
  return null;
 }
 public static string Phase(string parameter){
  if(parameter=="engineUpdateProcessStarted")return "Proceso iniciado";
  if(parameter=="engineUpdateProcessClientStep_displayEula")return "Archivo preparado; esperando continuar";
  if(parameter=="engineUpdateProcessClientStep_reportUpdateEngineProgress")return "Motor de actualización solicitado";
  if(parameter=="engineUpdateProcessCompleted")return "Motor informa de finalización";
  if(parameter=="engineUpdateProcessPostUpdateDeviceArrived")return "Equipo detectado tras la actualización";
  return null;
 }
}
