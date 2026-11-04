# POD Go NAM Manager 0.8.5

- Un único campo **Volumen (dB)** con **Automático**.
- Automático normaliza el volumen comparando cada A2 Lite con Developed by Conr4d a −5 dB mediante la misma señal de prueba local. No modifica la saturación ni necesita datos de calibración.
- **Editar volumen…**: cambia el NAM seleccionado en su mismo Model, conserva una copia y verifica la lectura completa. También se abre con doble clic en su volumen. Los NAM ya ajustados con versiones anteriores conservan sus pesos de entrada.
- **Borrar NAM…**: guarda una copia, libera la posición y verifica que queda vacía. Los IR y Model 0 siguen protegidos.
- Texto de Preparar carga corregido sin NAM seleccionado, en español e inglés.
- Custom v8 incluido sin cambios. No hace falta reinstalarlo para utilizar estas funciones.

## Pruebas

94 comprobaciones offline aprobadas: 24 nuevas, 34 NAM, 27 interfaz y exclusión, 3 escalado y 6 release/idiomas. La auditoría del paquete verifica las 17 imágenes del firmware incluido. La primera normalización de la prueba tardó unos 0,68 s y otro modelo unos 0,32 s en el PC de desarrollo; el tiempo puede variar.

Las nuevas operaciones USB de borrado y edición están comprobadas con el protocolo y un dispositivo simulado; queda su prueba en la pedalera. No se ha abierto USB ni SDK en esta compilación.

## English

One **Volume (dB)** field with **Automatic**. Automatic compares every A2 Lite with Developed by Conr4d at −5 dB using the same local probe. It preserves model saturation. **Edit volume…** updates the selected Model with backup and full readback verification; **Delete NAM…** frees a selected managed slot with backup. Cabinet IRs and Model 0 stay protected. The empty capture prompt is fixed. Existing v8 users do not need to reflash.

94 offline checks passed. Physical testing of the new delete and volume-edit operations remains separate.
