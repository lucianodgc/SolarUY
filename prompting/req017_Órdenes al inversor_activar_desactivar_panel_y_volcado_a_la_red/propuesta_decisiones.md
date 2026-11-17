# Propuesta de decisiones de req017 — D1–D4 confirmadas

Las tareas 1.1–1.4 exigen acordar estas decisiones antes de implementar los contratos dependientes y la migración. Confirmadas explícitamente por el usuario el 16/11/2026. Esta propuesta usa los contratos reales y no modifica el alcance de las cuatro órdenes.

## D1. Destino del volcado

Mantener el volcado global al único endpoint TCP configurado. Mostrar «Inversor configurado — alcance global» en confirmación y auditoría; el parque seleccionado no limita la operación. No modificar los flags de todos los registros Inversor del inventario a partir de esa respuesta global. Las órdenes de panel conservan la identidad y el nodo/parque vigentes comprobados en el servidor.

## D2. Confirmación, repetición y concurrencia

- Confirmar solo un ACK estructuralmente válido con Exito=true y resultado=true. Un rechazo remoto explícito o resultado=false se registra como Rechazada. Una pérdida de respuesta, timeout/cancelación después de posible envío o error remoto sin prueba de no ejecución se registra como Indeterminada. No reenviar automáticamente una orden.
- Usar estados Pendiente, Confirmada, Rechazada, Fallida e Indeterminada. Fallida se reserva para fallos locales demostrados anteriores al envío. Un ACK confirmado no implica que ya exista una nueva lectura ni que activar garantice potencia positiva.
- Las operaciones son «establecer activo/inactivo», nunca «invertir el estado». Repetir un estado ya alcanzado puede confirmarse según el ACK vigente del simulador.
- Asociar una correlación a la solicitud confirmada en Blazor Server. Repetir la misma correlación del mismo usuario/operación/destino devuelve el resultado guardado sin enviar otra vez; una correlación reutilizada con otro contenido se rechaza. Un intento explícitamente nuevo tiene nueva correlación y su propia auditoría.
- Serializar las cuatro órdenes del endpoint mediante el bloqueo de sesión compartido de MariaDB, con nombre independiente del recolector y sin transacción SQL abierta durante TCP. Si está ocupado, rechazar el nuevo intento sin enviarlo. No confiar en un semáforo local ni en la afinidad Nginx.
- El protocolo TCP no ofrece fencing ni deduplicación remota: no prometer exactamente una vez ante pérdida de la sesión de bloqueo o caída durante el envío. Un resultado incierto requiere revisar el estado observado y confirmar explícitamente cualquier intento nuevo.

## D3. Auditoría durable

Guardar un IntentoOrdenInversor por intento: Guid generado por Domain, correlación, sub real cuando exista, autenticación/autorización observadas, acción, destino solicitado y validado, fechas UTC de solicitud/resultado, estado y código/mensaje seguros. No guardar roles completos, tokens, credenciales, trazas o mensajes TCP sin filtrar. Conservar destino aunque luego cambie el inventario; no eliminar auditoría por cascada ni inventar usuarios.

Registrar también intentos rechazados por falta de permisos/sub o destino inexistente. El actor será nulo cuando no exista sub comprobable; nunca se atribuye como usuario «anónimo» o NameIdentifier ficticio. Las consultas existentes pueden mantener su fallback; las órdenes exigen sub explícito. No deduplicar intentos anónimos como si pertenecieran a la misma persona.

Antes de enviar, confirmar el registro Pendiente. Si no se puede guardar, no enviar y devolver un error seguro de auditoría. Al recibir el resultado, intentar persistirlo con cancelación independiente y acotada, para que cerrar la página no impida completar auditoría. Propuesta configurable: plazo de cierre de 5 segundos y como máximo dos intentos de persistencia; esos reintentos no llaman al inversor.

Si falla el guardado final, el intento durable queda pendiente y la respuesta distingue el resultado remoto conocido de la auditoría incompleta. Al recuperar una sesión de ejecución sin otro emisor activo, un Pendiente abandonado se convierte en Indeterminada; nunca se reenvía. La persistencia no puede garantizar registrar nuevos intentos durante una caída total de MariaDB: el sistema bloquea el envío y documenta esa limitación.

## D4. Operación y salud

Reutilizar EstaActivo del inversor como estado operacional observado, con su fecha. No cambiar Panel.Estado ni inferir habilitación desde la última orden o desde 0 W. Corregir el simulador para que telemetría y medición histórica respeten las mismas banderas, conservadas al sincronizar el mismo inventario.

Una desactivación voluntaria sin falla explícita produce 0 W y señal Sano; no crea una alerta técnica únicamente por potencia cero. Si existe una falla explícita, continúa informándose Fallado aun con el panel desactivado. La clasificación por potencia esperada permanece deshabilitada según req016: no se habilita sin evidencia adicional de operación y no se reconstruyen estados operacionales históricos inexistentes.

## Entradas existentes y verificación

Las cuatro rutas de InversorService y los controles de SimuladorInversor delegarán en los mismos casos auditados y pedirán confirmación visual. El encendido del inversor conserva su funcionalidad anterior, sin añadirlo como quinta operación de req017. Se probarán persistencia y protocolo reales, UI aislada y dos instancias sobre el mismo endpoint, siguiendo las herramientas existentes.
