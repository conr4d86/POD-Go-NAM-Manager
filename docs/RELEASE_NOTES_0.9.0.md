# PODGoNamManager 0.9 - CFv9

## Español

La versión 0.9 reúne **Biblioteca NAM**, **Custom Editor** y **Firmware / Restaurar** en un solo ejecutable para Windows con USB activo. Incluye **CF NAM Conr4d v9** para la **POD Go estándar** con base 2.50.

### Qué puedes hacer

- **Biblioteca NAM.** Cargar capturas NAM A2 Lite de 48 kHz en posiciones de la pedalera y ajustar su nivel con la opción automática.
- **Custom Editor.** Seleccionar presets y snapshots, editar parámetros y bypass, arrastrar bloques en la cadena y guardar el preset activo en la POD Go. El panel de asignaciones incluye pulsadores virtuales, color de pulsador, controles de expresión y asignaciones A/B. En `Model`, A/B permite elegir dos capturas NAM y alternar entre ellas con un pulsador.
- **Firmware / Restaurar.** Instalar el CF v9 incluido y consultar la guía de recuperación. Con un CF v9 compatible, la app abre directamente el editor. Si detecta firmware oficial o anterior, abre Firmware / Restaurar.

En la pedalera se comprobaron las cargas NAM consecutivas, el movimiento de bloques con guardado, el cambio de color de un pulsador y la asignación de Gain a EXP2 con guardado.

### Antes de instalar el CF v9

Crea y conserva un backup completo `.pgb` con POD Go Edit. Cierra POD Go Edit antes de conectar desde el Manager. NAM A2 Lite sustituye a **Line 6 Clarity** y el Manager avisa si la copia contiene presets o valores predeterminados que usan ese ampli. Mantén la POD Go conectada por USB y a la corriente hasta la confirmación de la instalación.

### Qué queda pendiente

1. **Edición del banco User por USB.** Renombrar, mover y reemplazar presets completos desde la lista quedan deshabilitados mientras se investiga el error de lectura del directorio. Puedes seleccionar presets, editar el activo y guardar sus cambios.
2. **Importación individual de presets.** Queda pendiente cargar un `.pgp` completo directamente en una posición de la POD Go. Para restaurar un backup completo `.pgb`, utiliza POD Go Edit.
3. **Asignaciones con más de dos valores.** Esta versión utiliza A/B para las asignaciones de valores por USB, incluido `Model`.

## English

Version 0.9 brings **NAM Library**, **Custom Editor**, and **Firmware / Restore** together in one USB-enabled Windows executable. It includes **CF NAM Conr4d v9** for the **standard POD Go**, based on firmware 2.50.

### What you can do

- **NAM Library.** Upload 48 kHz NAM A2 Lite captures to the pedalboard and use automatic level matching.
- **Custom Editor.** Select presets and snapshots, edit parameters and bypass, drag blocks in the signal chain, and save the active preset to the POD Go. The Assignments panel includes virtual footswitches, footswitch colors, expression controls, and A/B assignments. With `Model`, A/B lets you switch between two NAM captures using a footswitch.
- **Firmware / Restore.** Install the bundled CF v9 and open the recovery guide. A compatible CF v9 opens the editor directly. Stock firmware or an earlier revision opens Firmware / Restore.

Hardware checks covered consecutive NAM uploads, moving blocks and saving, changing a footswitch color, and assigning Gain to EXP2 and saving.

### Before installing CF v9

Create and keep a full `.pgb` backup in POD Go Edit. Close POD Go Edit before connecting with the Manager. NAM A2 Lite replaces **Line 6 Clarity**, and the Manager flags backups with presets or defaults that use that amp. Keep the POD Go connected to USB and power until installation is confirmed.

### What's pending

1. **User bank editing over USB.** Renaming, moving, and replacing complete presets from the list are disabled while the directory read error is investigated. You can still select presets, edit the active preset, and save its changes.
2. **Individual preset import.** Importing a complete `.pgp` directly into a POD Go slot is pending. Use POD Go Edit to restore a full `.pgb` backup.
3. **Assignments with more than two values.** This release uses A/B for USB value assignments, including `Model`.

## Windows download

`PODGoNamManager-CFv9-USB.exe`

SHA-256 `315673b0e70e146d48c379c851eba6e62a02fc8348746298289253b06e553b07`
