# Validation status

This file distinguishes observations on one physical POD Go from offline
tests. It is not a claim that all hardware revisions have been tested.

| Check | Evidence | Status |
| --- | --- | --- |
| Earlier v5 NAM CF boots and produces audio | User confirmed presets, lights, audio and block bypass | Observed on one standard POD Go |
| NAM upload and readback | Manager 0.4.2; Friedman in Models 52/53 | Observed; Model 52 audio and persistence after restart confirmed |
| Restore official 2.50 | Manager 0.6.1; user restored presets using POD Go Edit backup | Observed |
| v7 name experiment | User still saw a numeric Model value | Did not implement the requested display; superseded |
| v8 names in the Model value | 37 ARM emulator cases using native IR-name getter and font metrics | Offline passed; screen on hardware pending |
| Private test app 0.7.2 | 33 NAM + 27 UI/lease + 4 built-in + 6 bilingual/package checks | Offline passed; user trial pending |
| Earlier 0.7.2 clean source ZIP rebuild | Fresh extraction compiles the app and recreates the exact v8 HXF from original | Offline passed |
| Manager 0.8.0 | 34 NAM + 27 UI/lease + 6 bilingual/package + 3 layout/input checks | 70 offline checks passed; physical app trial pending |
| Public download 0.8.0 | SHA-256 `7135a035254350f8d2d5a5eca26df2f3c347ac25dc3619bfcda174063b219940` | New manager, same v8 CF and installation engine |

v8 preserves the v6 DSP code and built-in capture byte-for-byte. Only the ARM
application UI changes relative to v6; the other 16 archive images, including
the bootloader, and the updater trailer are unchanged. Display emulation does
not test audio timing, cold boot, renderer output or a physical firmware write.

Pinned original POD Go 2.50 HXF SHA-256:

`9ac67fdd55f8b8feb8c21f231eaafb5c6eb7883a8218430c9c4a23567c2f34d1`

v8 source rebuild HXF SHA-256:

`d3fc10a52d71920e60627c39cf67c88d9b9116137f42642321914677e7002eae`

v8 inflated archive SHA-256 (independent of compression):

`7f45301113ef6adbd859ee2edb33157884ce935279e908b627fe208731087b47`

The C# reconstruction uses a different compressor and can produce a different
HXF file hash. Its entire inflated archive must match the pinned value above,
and the ordinary HXF validator checks all 17 images before installation.

## Remaining manual checks before calling this stable

1. Finish the user's v8 trial: boot, presets, audio, `0. Dev by Conr4d` above
   **Model**, unchanged **Amp NAM A2 Lite** title.
2. Upload a named capture through the library; select it, verify its displayed
   short name and audio, and confirm it persists after a normal restart.
3. Finish the install/restore flow and both interface languages on the same device.

Results must be added only after they actually occur. No USB or SDK is opened
by the offline tests. The public preview remains experimental pending these manual checks.


## Manager 0.8.0 layout and input checks

Native screenshots use a synthetic bank, in both languages. Rendered client
sizes: 1500×900, 1348×730, 1264×680, 1008×680, 1920×1000, then back to
1500×900. Scaling retains original measurements and fonts; its lower limit
is 78%, with scrolling when necessary. Maximizing stays enabled.

Automatic preserves the input at 0 dB. Manual input trim scales all 72
raw-input coefficients (initial projection and 23 conditioning paths), keeps
the −5 dB output trim, and survives send-time source revalidation in the
fixture transport. The source NAM remains unchanged.

The firmware binary is unchanged. Offline rendering and fixture transport do
not establish physical audio timing, actual monitor DPI behavior, or USB
transfer performance. No device or SDK was opened for this release work.
