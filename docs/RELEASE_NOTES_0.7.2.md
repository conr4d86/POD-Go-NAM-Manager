# 0.7.2 preview — hardware review in progress

- Unified NAM library and firmware/recovery application.
- Spanish/English selector; firmware page opens first.
- v8 puts number and short name **above Model**, retaining the block title.
- Built-in Model 0: `0. Dev by Conr4d`.
- User-selected slots, protected cabinet IRs, backed-up NAM replacement.
- Adjustable output, default −5 dB; Model preview follows screen limits.
- Backup reminder before CF installation and information on restoring presets
  after returning to official firmware.

The source rebuild, ARM name tests and app fixtures pass offline. The v8
display still needs manual hardware verification.
This is an experimental prerelease, not a stable hardware-validated version.


The public download is now the exact existing 0.7.2 EXE, with the CF and engine
included. Installing CF NAM does not ask for the original firmware. Only the
official-restoration option asks the user to select an original HXF.
