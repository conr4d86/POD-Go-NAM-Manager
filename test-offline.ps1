param([string]$Executable=(Join-Path $PSScriptRoot 'build\app\PODGoNamManager.exe'))
$ErrorActionPreference='Stop'
if([IntPtr]::Size -eq 8){
 & (Join-Path $env:WINDIR 'SysWOW64\WindowsPowerShell\v1.0\powershell.exe') -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $PSCommandPath -Executable ((Resolve-Path -LiteralPath $Executable).Path)
 exit $LASTEXITCODE
}
# Read resources only. Do not run Main, initialize the engine or open USB.
Add-Type -AssemblyName System.IO.Compression
$podAssembly=[Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $Executable).Path)
$podStream=$podAssembly.GetManifestResourceStream('runtime.zip')
if(-not $podStream){throw 'Bundled runtime missing'}
$podZip=[IO.Compression.ZipArchive]::new($podStream,[IO.Compression.ZipArchiveMode]::Read)
try {
 $podNames=@($podZip.Entries | ForEach-Object FullName)
 if(@($podNames | Where-Object {$_ -like 'official/*'}).Count){throw 'Original official HXF must not be bundled'}
 $podCustom=@($podZip.Entries | Where-Object {$_.FullName -like 'custom/*.hxf'})
 if($podCustom.Count -ne 1){throw 'Exactly one bundled CF is required'}
 $podSource=$podCustom[0].Open();$podMemory=[IO.MemoryStream]::new()
 try {$podSource.CopyTo($podMemory);$podBytes=$podMemory.ToArray()}finally{$podSource.Dispose();$podMemory.Dispose()}
 $podHash=[BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($podBytes)).Replace('-','').ToLowerInvariant()
 if($podHash -ne 'd3fc10a52d71920e60627c39cf67c88d9b9116137f42642321914677e7002eae'){throw 'Bundled CF differs from tested v8'}
 $podAuditDirectory=Join-Path $PSScriptRoot 'build\bundled-audit';New-Item -ItemType Directory -Force -Path $podAuditDirectory | Out-Null
 $podAuditFile=Join-Path $podAuditDirectory 'bundled-custom.hxf';[IO.File]::WriteAllBytes($podAuditFile,$podBytes)
 $podFileType=$podAssembly.GetType('FirmwareFile',$true)
 $podValidated=[FirmwareFile]::Read([string]$podAuditFile)
 if($podFileType.GetField('ImageCount').GetValue($podValidated) -ne 17){throw 'HXF image checks failed'}
 $podResult=[ordered]@{status='PASS_BUNDLED_FILE_AUDIT';custom_sha256=$podHash;images_validated=17;original_hxf_required_for_custom_install=$false;original_official_hxf_bundled=$false;engine_bundled=($podNames -contains 'engine/EngineCLIWrapper.dll');SDK_started=$false;USB_opened=$false}
 $podResult | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $podAuditDirectory 'results.json') -Encoding utf8
 $podResult | ConvertTo-Json
} finally {$podZip.Dispose();$podStream.Dispose()}
