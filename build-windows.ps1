param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'build\app'),[string]$CustomFirmware=(Join-Path $PSScriptRoot 'build\POD_GO_NAM_Conr4d_v8.hxf'))
$ErrorActionPreference='Stop'
$podPublicRoot=$PSScriptRoot
$podPython=Get-Command python -ErrorAction Stop
& $podPython.Source -B (Join-Path $podPublicRoot 'tools\make_bundle.py') $CustomFirmware
if($LASTEXITCODE -ne 0){throw 'Bundled CF preparation failed'}
& $podPython.Source -B -c "import sys; from pathlib import Path; sys.path.insert(0,str(Path(sys.argv[1])/'tools')); from localize_sources import prepare; prepare(sys.argv[1],Path(sys.argv[1])/'build/generated')" $podPublicRoot
if($LASTEXITCODE -ne 0){throw 'Source localization failed'}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory=(Resolve-Path -LiteralPath $OutputDirectory).Path
$podCompiler='C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
$podSources=@(Get-ChildItem -LiteralPath (Join-Path $podPublicRoot 'src') -Filter *.cs | ForEach-Object {Join-Path (Join-Path $podPublicRoot 'build\generated') $_.Name})
$podArguments=@('/nologo','/platform:x86','/target:winexe','/optimize+','/main:StudioApp',"/out:$OutputDirectory\PODGoNamManager.exe",'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll','/reference:System.IO.Compression.dll',"/resource:$podPublicRoot\build\runtime.zip,runtime.zip","/resource:$podPublicRoot\assets\en.json,en.json","/resource:$podPublicRoot\tests\default-carrier.wav,reference-nam.wav","/resource:$podPublicRoot\assets\podgo-nam-manager.png,podgo-icon.png","/win32icon:$podPublicRoot\assets\podgo-nam-manager.ico")+$podSources
& $podCompiler $podArguments
if($LASTEXITCODE -ne 0){throw 'Public app compilation failed'}
Set-Content -LiteralPath (Join-Path $OutputDirectory 'known-models.json') -Value '[]' -Encoding utf8
Get-FileHash -LiteralPath (Join-Path $OutputDirectory 'PODGoNamManager.exe') -Algorithm SHA256 | Select-Object Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256.json') -Encoding utf8
Write-Output (Join-Path $OutputDirectory 'PODGoNamManager.exe')
