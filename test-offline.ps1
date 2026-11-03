param([string]$OfficialFirmware='')
$ErrorActionPreference='Stop'
$podOfflineExe=Join-Path $PSScriptRoot 'build\app\PODGoNamManager.exe'
$podOfflineRunner=Join-Path $PSScriptRoot 'build\ManagerOfflineChecks.exe'
Copy-Item -LiteralPath $podOfflineExe -Destination $podOfflineRunner -Force
$podOfflineArgs=@('--public-self-test',$PSScriptRoot)
if($OfficialFirmware){$podOfflineArgs+=$OfficialFirmware}
$podOfflineQuoted=@($podOfflineArgs | ForEach-Object {'"'+$_+'"'})
$podOfflineProcess=Start-Process -FilePath $podOfflineRunner -ArgumentList $podOfflineQuoted -WindowStyle Hidden -Wait -PassThru
$podOfflineReport=Join-Path $PSScriptRoot 'build\public-tests\results.json'
if(Test-Path -LiteralPath $podOfflineReport){Get-Content -LiteralPath $podOfflineReport}
if($podOfflineProcess.ExitCode -ne 0){throw ('Offline checks failed: '+$podOfflineProcess.ExitCode)}
