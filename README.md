<p align="center"><img src="assets/Icon.JPG" width="240" alt="POD Go NAM Manager"></p>

# POD Go NAM Manager

[Español](README.es.md) · **English**

Run **NAM A2 Lite inside your POD Go**, and change captures over USB without
reinstalling firmware for each model. One Windows app provides **Firmware and
recovery** and a **NAM Library**, with Spanish and English interfaces.

**Experimental preview.** The earlier NAM firmware and USB capture upload
have been tested with audio on a POD Go. The v8 Model value display and this public,
bilingual app are still awaiting the complete hardware test. Offline checks
are described in [validation](docs/VALIDATION.md).

[Download for Windows](https://github.com/conr4d86/POD-Go-NAM-Manager/releases/tag/v0.7.2-preview)

## Change a NAM in three actions

With custom firmware installed and the library connected:

1. **Open NAM…** — choose your capture.
2. **Choose a Model slot** — empty slots and existing NAMs are clearly marked.
3. **Upload to Model…** — wait for upload and full readback verification.

Select that Model in the NAM A2 Lite amp block on the pedalboard. You can then
disconnect the computer: the POD Go processes the audio itself.

Use the **short name** field for a Model value such as `53. FRIEDMAN HBE`. Existing
captures uploaded with older Manager versions need uploading again to add a
name. An occupied NAM requires **Replace this NAM**, with a verified backup.
Cabinet IRs are protected. Output defaults to **−5 dB** and is adjustable. Names are shortened to fit the screen; the app previews the result.

## First installation

Make a backup in **POD Go Edit** first and keep its `.pgb` file. Install the
official POD Go driver and Line 6 Central, then close the Line 6 applications.
Keep the original **POD Go 2.50 HXF** from [Line 6 Downloads](https://line6.com/software/).

Open Manager; it starts on **Firmware and recovery**:

1. **Connect POD Go** in normal startup mode.
2. Choose **CF NAM Conr4d** and **Load your backup (.pgb)…**. The first preparation
   also asks for your original 2.50 HXF; the app applies the included patch locally.
3. Click **Install NAM firmware**, read the backup reminder and confirm **OK**.

Wait for the final result and normal startup before checking audio. This is a
full firmware transfer and takes substantially longer than uploading a NAM.
There is no automatic installation when opening the app.

Complete instructions: [Quick start](docs/QUICKSTART.md).

![Firmware and recovery — English interface preview](assets/screenshots/firmware-en.png)

*Offline interface preview; the current hardware-validation status is listed below.*

## Models, slots and cabinets

| Item | Meaning |
| --- | --- |
| Model 0 | Built-in, read-only `0. Dev by Conr4d`; capture by **conr4daudio** |
| Model 1–128 | The corresponding user IR slot; NAMs and cabinet IRs share this bank |
| Model knob | Shows the number and short name above **Model**; the title stays **Amp NAM A2 Lite** |
| Cab/IR block | Separate from NAM; keep it enabled or bypass it as usual |

The amp model replaces **Line 6 Clarity**. Compatible captures are **A2 Lite,
3 channels, 1,871 weights, 48 kHz**, including a SlimmableContainer with that
submodel. Other NAM architectures are rejected with an explanation.

Uploading data is possible with official firmware, too; **NAM playback requires
custom firmware**. A successful upload confirms stored data, not an installed CF.
The app does not yet reliably identify official versus custom firmware over USB.

This port targets the **standard POD Go, base 2.50**. Wireless and other Line 6
models are not claimed as supported. Installing this firmware does not implement
the separate AMP OUT / cabinet routing modification.

## Recovery

Choose **Load Official Firmware…** and provide the exact original POD Go 2.50
HXF. After restoring it, load your backup in POD Go Edit to restore your previous
presets, including presets that had used the NAM block.

If normal startup fails, use the documented POD Go update mode (hold **PAGE >**
while powering on) and the official Line 6 recovery tools. Do not perform a
factory reset as part of this guide. See [recovery](docs/RECOVERY.md).

## Build and test

- Windows, .NET Framework 4.5+ and Python 3.10+ for source preparation.
- `powershell -ExecutionPolicy Bypass -File .\build-windows.ps1`
- `powershell -ExecutionPolicy Bypass -File .\test-offline.ps1`
- The firmware source rebuild additionally uses `keystone-engine==0.9.2`:
  `python tools/build_firmware.py path/to/original.hxf`.

The app build embeds our source-generated patch and artwork. It does **not**
embed a complete HXF or Line 6 DLLs. The firmware engine uses the user's installed
Line 6 Central files. The stock HXF and generated files remain local.
See [building](docs/BUILDING.md) for reproducibility and release packaging.

## Credits and licence

Developed by **Conr4d / conr4daudio**.
The included NAM is credited to **conr4daudio**.

The NAM carrier/kernel work derives from
[nocaseguy/hx-stomp-nam-lite](https://github.com/nocaseguy/hx-stomp-nam-lite),
pinned at `2e801641df4a083c29ee13c5718c047d336ef692`. Source, modifications and
licence notices are included. Code is **GPL-3.0-or-later**; see [NOTICE](NOTICE)
and [LICENSE](LICENSE). The included capture has its own [notice](models/NOTICE.txt).

Independent, unofficial and experimental. Not affiliated with Line 6 or Yamaha.
