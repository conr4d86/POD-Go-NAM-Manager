# Preguntas frecuentes / FAQ

## Español

### ¿Cuánto tarda la instalación del custom firmware?

El tiempo no es fijo. Según los reportes de usuarios, la espera puede aumentar
con la cantidad de IR guardados en la POD Go e incluidos en su backup. Un usuario
ha comunicado **5–10 minutos** para completar la instalación; es una referencia,
no un plazo garantizado. Con más IR, el proceso puede tardar más.

Mantén la POD Go encendida y conectada y deja la app abierta hasta el resultado
final. Superar esos minutos no indica por sí solo un fallo.

### ¿La app restaura mi backup al seleccionarlo?

No. **Carga tu backup (.pgb)…** conserva una copia verificada del archivo.
Si vuelves al firmware oficial, restaura tus presets cargando ese backup desde
**POD Go Edit**, como explica la [guía de restauración](RECOVERY.md#español).

### ¿Hay que esperar la instalación completa cada vez que cambio de NAM?

No. El custom se instala una vez. Después usa **Abrir NAM → elegir Model →
Cargar** en la biblioteca; esa operación solo transfiere el NAM y verifica los
datos, sin reinstalar el firmware.

[Instalación paso a paso](INICIO_RAPIDO.md) · [Acerca del proyecto](../README.es.md)

### ¿Qué hace Automático?

Normaliza el volumen usando una señal de prueba común para tu captura y la
referencia **Developed by Conr4d a −5 dB**. El cálculo se hace en el PC antes de
enviar y no cambia la saturación del modelo. Solo hay un campo **Volumen (dB)**;
desmarca Automático para ajustarlo manualmente. El archivo original no cambia.

En el PC de desarrollo, la primera comparación tardó unos **0,68 s** y otro
modelo unos **0,32 s**; depende del ordenador. Es una referencia de volumen,
no una garantía de idéntica sonoridad con cualquier guitarra, preset o IR.
No es un control automático continuo mientras tocas.

### ¿Puedo ajustar un NAM guardado o borrar uno?

Sí: selecciónalo y pulsa **Editar volumen…** o **Borrar NAM…**. Se conserva una
copia previa y se verifica la lectura después de la operación. Model 0 y los IR
de pantalla están protegidos. Borrar libera la posición; los presets que usaban
ese NAM dejarán de encontrarlo.

Para editar volumen, este PC debe conservar los niveles del NAM. Si aparece
que no se conocen, abre su archivo original y actualiza la posición. Un guion
en la columna Volumen significa que el nivel no se conoce, no que sea 0 dB.

### ¿Debo reinstalar el custom para usar la app 0.8.5?

No, si ya tienes el custom v8. La 0.8.5 cambia el gestor y mantiene ese firmware.

## English

### How long does custom firmware installation take?

There is no fixed duration. User reports suggest that the wait can increase with
the number of IRs stored on the POD Go and included in its backup. One user
reported completing installation in **5–10 minutes**; this is a reference,
not a guaranteed duration. With more IRs, the process may take longer.

Keep the POD Go powered on and connected, and leave the app open until the final
result. Taking longer than those minutes does not by itself indicate a failure.

### Does selecting my backup restore it?

No. **Load your backup (.pgb)…** keeps a verified copy of that file. If you
return to official firmware, restore your presets by loading the backup in
**POD Go Edit**, as explained in the [recovery guide](RECOVERY.md#english).

### Do I need another full installation whenever I change a NAM?

No. Install the custom firmware once. Then use **Open NAM → choose Model →
Upload** in the library; that operation only transfers the NAM and verifies
the data, without reinstalling firmware.

[Step-by-step installation](QUICKSTART.md) · [About the project](../README.md)

### What does Automatic do?

It normalizes volume by running the same test signal through your capture and
the **Developed by Conr4d at −5 dB** reference. Analysis runs on the PC before
upload and preserves model saturation. There is one **Volume (dB)** field;
uncheck Automatic to set it manually. The source file is unchanged.

On the development PC the first comparison took about **0.68 s**, and another
model about **0.32 s**; timing varies. This is a volume reference, not a promise
of identical perceived loudness with every guitar, preset or IR. It is not
continuous automatic gain control while playing.

### Can I adjust a stored NAM or delete one?

Yes: select it and choose **Edit volume…** or **Delete NAM…**. Both save a backup
and verify the result. Model 0 and cabinet IRs are protected. Deleting frees
the slot; presets using that NAM will no longer find it.

Volume editing needs the NAM levels saved on this PC. If these are unknown,
open the original NAM and update the slot. A dash in the Volume column means
the level is unknown, not 0 dB.

### Does Manager 0.8.5 require reinstalling custom firmware?

No, if v8 is already installed. Manager 0.8.5 keeps that firmware unchanged.
