# Building from source — developers only

The end-user download is the existing **PODGoNamManager.exe 0.7.2**, with the
CF NAM Conr4d and installation engine already included. **Installing CF NAM
does not ask for an original firmware file.** Only restoring official firmware
requires selecting an official POD Go 2.50 HXF.

## Developer source build

Windows, .NET Framework's 32-bit C# compiler and Python 3.10+ are required.
Developers also need locally installed Line 6 Central engine files to create
the embedded runtime. This is a build dependency, not an end-user setup step.

```powershell
python -m pip install -r requirements-dev.txt
python tools/build_firmware.py "C:\path\to\original.hxf"
python tools/verify_ui.py build/POD_GO_NAM_Conr4d_v8.hxf
powershell -ExecutionPolicy Bypass -File .\build-windows.ps1
powershell -ExecutionPolicy Bypass -File .\test-offline.ps1
```

The original HXF above is used by developers to reconstruct the reviewed CF
from preferred source. It is never requested by the CF installation button
in the published app. `tools/make_bundle.py` bundles that custom HXF and the
local engine files into `build/runtime.zip`. The stock HXF is excluded.
Set `PODGO_LINE6_CENTRAL` to override the developer engine-file directory.

`test-offline.ps1` checks the assembly resource archive and validates all
17 images in its bundled custom firmware. It does not run the app's Main,
start the SDK or open USB. The optional workflow example remains in
`docs/windows-offline-workflow.yml`; no hosted CI pass is claimed.

## Release packaging

```powershell
python tools/package_release.py
```

Packaging publishes the exact existing 0.7.2 EXE, whose SHA-256 is
`8217facfbed83f162eff1cff2b37171c7d8fa5956074f6b37409011aa9f201b0`.
The source ZIP contains the same application logic, with test-only paths made
portable. Runtime ZIPs, backups, local logs and credentials are not put into
the source repository. The downloadable EXE contains its CF and engine.

The packaging command creates local artifacts only; GitHub publication is a
separate operation. Physical validation of the v8 display remains separate
from source compilation and file verification.
