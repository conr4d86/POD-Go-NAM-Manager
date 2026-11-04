# Borrador para guitarristas.info — no publicado

## NAM A2 Lite en POD Go, con gestor en español e inglés

**POD Go NAM Manager 0.8.6** es una app para instalar el custom
firmware y cargar capturas NAM A2 Lite por USB. El sonido se procesa en la
propia pedalera; después no hace falta dejar el PC conectado.

Primero haz un backup con **POD Go Edit** y conserva el `.pgb`. Abre la app:

**Conectar POD Go → CF NAM Conr4d y cargar backup → Instalar CF NAM.**

El custom viene incluido en el EXE. Espera al resultado final sin desconectar
la pedalera. Si ya tienes el custom v8, no necesitas reinstalarlo para usar 0.8.6.

Después, cambiar de NAM son tres acciones:

**Abrir NAM → elegir Model → Enviar.**

La app muestra qué posiciones están libres, cuáles contienen NAM y cuáles son
IR de pantalla protegidos. Puedes poner un nombre corto, ajustar **Volumen (dB)**
con **Automático**, editar el volumen de un NAM guardado y borrar NAM para
liberar posiciones. La app conserva una copia previa al editar, borrar o
sustituir. No hay que reinstalar el firmware por cada NAM.
El Cab/IR sigue siendo un bloque independiente.

Automático compara cada captura con Developed by Conr4d a −5 dB mediante una
prueba breve en el PC y conserva la saturación. Solo hay un campo de volumen.
También se puede volver al firmware oficial y cargar el backup con POD Go Edit.

Según los reportes de usuarios, la espera de instalación puede aumentar con los
IR guardados en la POD Go e incluidos en el backup. Un usuario ha comunicado
**5–10 minutos**; es orientativo y puede tardar más. Espera al resultado final
sin desconectar la pedalera. Esta espera no se repite al cambiar un NAM.

Descarga: [POD Go NAM Manager 0.8.6](https://github.com/conr4d86/POD-Go-NAM-Manager/releases/tag/v0.8.6)

El detalle de las pruebas está en [validación](VALIDATION.md). Las nuevas
operaciones de borrar y editar volumen tienen pruebas locales con dispositivo
simulado; su comprobación USB física queda pendiente. Este texto es un borrador
para el foro y no se ha enviado desde la app.

---

### English version

**POD Go NAM Manager** runs NAM A2 Lite on the pedalboard and manages captures
over USB, with Spanish and English interfaces.

First, make a **POD Go Edit** backup. In Manager use **Connect POD Go → CF NAM
Conr4d and load backup → Install NAM firmware**. The custom is bundled. Existing
v8 users do not need to reflash for 0.8.6.

After that, changing a NAM takes three actions:

**Open NAM → choose Model → Upload.**

Empty slots, existing NAMs and protected cabinet IRs are clearly labelled.
Short names, one **Volume (dB)** control with **Automatic**, stored-volume editing
and NAM deletion are included. Editing, deletion and replacement keep a backup.
Automatic compares each capture with Developed by Conr4d at −5 dB using a brief
local probe, preserving saturation.
Changing a NAM does not reinstall firmware. The Cab/IR block remains independent.

A short first-install guide explains the backup and official-firmware recovery.
User reports suggest that installation can take longer with more IRs stored on
the POD Go and included in its backup. One user reported **5–10 minutes**;
this is approximate and may take longer. Keep the pedalboard connected until
the final result. Changing a NAM does not repeat the firmware installation.
The new delete and volume-edit operations passed offline fixture tests; physical
USB testing is pending. See [validation](VALIDATION.md).
