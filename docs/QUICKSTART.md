# Quick start

## Install custom firmware for the first time

**Before starting:** create a backup in POD Go Edit and keep its `.pgb`.
CF NAM Conr4d is included in the app. Close the other Line 6 applications.

1. Open Manager on **Firmware and recovery** and click **Connect POD Go**.
2. Click **CF NAM Conr4d** (included) and **Load your backup (.pgb)…**.
3. Click **Install NAM firmware** and confirm **OK** in the backup reminder.

**Waiting time:** user reports suggest that more IRs stored on the POD Go and
included in its backup can mean a longer wait. One user reported an installation
of **5–10 minutes**; this is approximate and yours may take longer. Do not close
the app or disconnect the pedalboard during installation.

Wait for the final result. Check startup, presets and audio.
Find **NAM A2 Lite** in the amp list; Model 0 is built-in.
For your other captures, open **NAM Library** and follow the three actions below.

More details: [FAQ](FAQ.md#english).

## Change a NAM — three actions

Custom firmware must be installed and the library connected.

1. **Open NAM…**
2. **Choose Model** — select an empty slot or a NAM to replace.
3. **Upload to Model…** — when verified, select that number on the pedalboard.

You can close the app. Changing a NAM does not reinstall firmware.

Optionally adjust the short name first. **Automatic** preserves the original
NAM input at **0 dB**; uncheck it to adjust input manually. Output attenuation
stays at **−5 dB**. Automatic does not analyze audio.
An occupied NAM offers **Replace Model…**, with confirmation and a backup.
Cabinet IRs are not overwritten.

The value above **Model** shows its number and short name. Model 0 displays
`0. Dev by Conr4d`; the block title remains **Amp NAM A2 Lite**.
Upload an old capture again with this version to add its short name.

## Language

The sidebar selector offers **Español / English** and remembers your choice.
It is disabled during transfers.

## Return to official firmware

**Load Official Firmware…** → select the original 2.50 → load your backup in
the app → restore. Afterwards, load your backup in **POD Go Edit** to restore
your previous presets.

## Window size

Version 0.8.0 supports maximizing and scales proportionally to available space.
Very small windows offer scrolling to keep controls readable. Updating Manager
does not require reinstalling the v8 custom firmware.
