# Building from source — developers only

The end-user download is the **PODGoNamManager.exe 0.8.6**, with the
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
start the SDK or open USB. These checks run locally; no hosted CI pass is claimed.

## Release packaging

```powershell
python tools/package_release.py
```

Packaging reads the newly built 0.8.6 executable from `build/app`, checks the
version, and creates the EXE, Windows ZIP, source ZIP, manifest and SHA-256 list.
Runtime ZIPs, backups, local logs and credentials are excluded from the source
repository. The downloadable EXE contains its CF and engine.

Offline regression and rendering commands (no USB or SDK):

```powershell
build/app/PODGoNamManager.exe --layout-self-test . build/layout-checks
build/app/PODGoNamManager.exe --release-self-test . build/release-checks
build/app/PODGoNamManager.exe --update-self-test . build/update-checks
```

The layout suite renders both sections and languages at several window sizes,
checks restoration to full size, and checks input gain encoding. File-system
protection can block replacing screenshots; use a fresh output directory.

The update suite checks carrier round trips, the reference-volume analyzer,
single-field volume UI and managed delete/edit operations with a simulated
device. The analyzer uses the embedded `reference-nam.wav` resource. These
tests do not open the SDK or establish physical USB operation.

The packaging command creates local artifacts only; GitHub publication is a
separate operation. Physical validation of the v8 display remains separate
from source compilation and file verification.
