# POD Go NAM Manager 0.8.6

[Descargar / Download PODGoNamManager.exe](https://github.com/conr4d86/POD-Go-NAM-Manager/releases/download/v0.8.6/PODGoNamManager.exe)

## Español

**Si ya tienes el custom v8, descarga y abre el nuevo EXE. No reinstales el
firmware.**

- Corrige el error intermitente **Respuesta incompleta** al editar volumen o
  actualizar la biblioteca. Se reúnen todas las partes de la respuesta USB
  antes de leerla, incluso cuando los metadatos ocupan pocos bytes.
- La misma corrección cubre las confirmaciones de apertura, cierre y borrado.
  Se comprueban el canal, la longitud y la identificación de la respuesta.
- El nivel se conserva en el catálogo en cuanto la lectura completa del NAM
  confirma el cambio. Si falla después el refresco, el mensaje indica que el
  volumen sí quedó guardado y verificado, y pide volver a conectar.
- Los registros de conexiones sucesivas se guardan en carpetas distintas.
- Mismo custom v8: **no hace falta reinstalar firmware**.

104 comprobaciones locales aprobadas, incluida la reproducción de la respuesta
real de 36 bytes recibida como 16 + 20. Estas pruebas no abren USB ni SDK;
la prueba de la corrección en la pedalera queda pendiente.

**Primera instalación:** crea un backup con POD Go Edit y usa **Conectar POD Go
→ CF NAM Conr4d y cargar backup → Instalar CF NAM**. El custom está incluido.
Espera al resultado final sin desconectar la pedalera.

**Cambiar un NAM:** **Abrir NAM → elegir Model → Cargar**.

[Inicio rápido](https://github.com/conr4d86/POD-Go-NAM-Manager/blob/codex/release-0.8.6/docs/INICIO_RAPIDO.md) · [Preguntas frecuentes](https://github.com/conr4d86/POD-Go-NAM-Manager/blob/codex/release-0.8.6/docs/FAQ.md#español)

## English

**Already using custom v8? Download and open the new EXE. Do not reinstall the
firmware.**

- Fixes intermittent **Incomplete response** errors when editing volume or
  refreshing the library. All USB response fragments are assembled before
  decoding, including short metadata and transfer/deletion acknowledgements.
- Reply channel, declared length and transaction identity are checked.
- Verified volume edits are retained in the local catalogue even if a later
  directory refresh fails. The error then explains that the volume was saved
  and verified, and asks you to reconnect.
- Repeated connections keep separate diagnostic folders.
- Same v8 custom firmware; **no reflash is needed**.

104 offline checks passed, including the captured 36-byte reply split into
16 + 20 bytes. Physical testing of the fix remains pending.

**First installation:** make a POD Go Edit backup, then use **Connect POD Go →
CF NAM Conr4d and load backup → Install NAM firmware**. The custom is bundled.
Wait for the final result without disconnecting the pedalboard.

**Change a NAM:** **Open NAM → choose Model → Upload**.

[Quick start](https://github.com/conr4d86/POD-Go-NAM-Manager/blob/codex/release-0.8.6/docs/QUICKSTART.md) · [FAQ](https://github.com/conr4d86/POD-Go-NAM-Manager/blob/codex/release-0.8.6/docs/FAQ.md#english)
