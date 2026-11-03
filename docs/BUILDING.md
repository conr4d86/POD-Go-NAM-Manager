# Building the app and firmware

Use Windows with .NET Framework's C# compiler and Python 3.10 or later. A
32-bit Windows application is built because the installed Central engine is
32-bit. No hardware connection is required for compilation or offline tests.

## App

```powershell
powershell -ExecutionPolicy Bypass -File .\build-windows.ps1
powershell -ExecutionPolicy Bypass -File .\test-offline.ps1
```

Output: `build/app/PODGoNamManager.exe`. The app includes artwork, translations
and a compact firmware delta. It does not embed a full HXF or Central DLLs.
The first CF preparation asks the user for the exact official POD Go 2.50 HXF.
The app reconstructs the custom locally, validates all images, and keeps the
result in the user's local application data. No firmware transfer starts
until the user clicks Install and accepts the backup reminder.

The firmware runtime loads the user's installed **Line 6 Central** files when
the user connects for firmware operations. Developers can override its path
with `PODGO_LINE6_CENTRAL`. Installing drivers and Central remains a separate
user setup step. NAM library transfers use the direct USB implementation.

## Firmware preferred source

```powershell
python -m pip install -r requirements-dev.txt
python tools/build_firmware.py "C:\path\to\original.hxf"
python tools/verify_ui.py build/POD_GO_NAM_Conr4d_v8.hxf
python tools/make_recipe.py "C:\path\to\original.hxf" build/POD_GO_NAM_Conr4d_v8.hxf
powershell -ExecutionPolicy Bypass -File .\build-windows.ps1
powershell -ExecutionPolicy Bypass -File .\test-offline.ps1 -OfficialFirmware "C:\path\to\original.hxf"
```

The stock input is pinned by SHA-256. `firmware/port/` holds the POD Go port,
including native loading geometry, the audio wrapper, graph reservations,
catalogue integration, built-in capture and Model-value UI hook. The pinned
upstream sources include the NAM kernel and SHARC assembler/simulator.
This is sufficient to rebuild the reviewed v8 inflated archive without
private project files. Generated vendor-derived images stay in `build/`.

`verify_ui.py` executes the patched Thumb instructions with Unicorn and the
native name getter/font measurement routines. It uses generated firmware
locally; no vendor ARM image is included as a test fixture. It does not
validate the physical display or open USB.

## Packaging for review

```powershell
python tools/package_release.py
```

This produces source and Windows ZIP files plus SHA-256 manifests under
`dist/`. Only selected source/assets are exported. Builds, presets, backups,
runtime DLLs, full HXF images and local logs are excluded. The public package
starts with an empty local capture catalogue. Keep the matching source ZIP
with every binary release.

The packaging command does not create a repository, authenticate to GitHub,
push commits or publish a release. Hardware sign-off remains separate.


The optional GitHub Actions definition is supplied as
`docs/windows-offline-workflow.yml`. Local offline checks use `test-offline.ps1`;
a hosted CI run is not claimed by this release.
