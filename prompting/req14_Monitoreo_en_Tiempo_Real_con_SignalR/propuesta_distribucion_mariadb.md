# Propuesta para validar con Luciano: distribución de monitoreo mediante MariaDB

Estado: **aprobada por Luciano según confirmación del usuario el 29/10/2026; implementación autorizada**. Este documento conserva el diseño acordado. La implementación y los resultados vigentes se registran en `implementations.md`.

## Resultado propuesto

Utilizar exclusivamente la MariaDB compartida existente para mantener un registro transaccional de avisos. Cada instancia lee ese registro de forma independiente y entrega los avisos a sus propios clientes SignalR. No agregar Redis, brokers ni servicios externos. Este observador lee avisos persistidos: nunca consulta inversores ni genera lecturas.

## Publicación atómica

Proponer una tabla técnica de avisos, otra de secuencia y un cursor por instancia. Sus nombres, columnas, retención y migraciones requieren aprobación de Luciano/equipo antes de implementarse; no son entidades nuevas del dominio solar.

Dentro de la misma transacción que guarda el lote de `LecturaPanel`:

1. Bloquear la fila de secuencia compartida, asignar una secuencia y mantener ese bloqueo hasta confirmar la transacción.
2. Persistir lecturas y aviso con secuencia, parques afectados e identidades panel/instante UTC.
3. Confirmar ambas escrituras juntas. Ante rollback, no quedan ni lecturas del lote ni aviso.

La secuencia debe representar orden de confirmación, no limitarse a un `AUTO_INCREMENT` reservado antes del commit: avanzar un cursor sobre identificadores de transacciones aún no confirmadas podría omitir avisos. El bloqueo de la secuencia y la transacción deben coordinarse con el bloqueo exclusivo de recolección existente y mantener un orden fijo de adquisición para evitar interbloqueos.

Esto elimina el hueco entre guardar y publicar. La entrega inmediata local existente podrá emplearse como optimización, pero el registro compartido será la fuente recuperable de avisos y la persistencia de lecturas seguirá siendo la fuente de verdad de los valores.

## Consumo por todas las instancias

- Cada instancia tendrá un identificador externo estable y su propio cursor persistido. No usar una cola con un consumidor exclusivo global: eso entregaría el aviso a una sola instancia.
- Un observador por instancia consultará secuencias posteriores a su cursor en lotes limitados y ordenados. Propuesta inicial para discutir: espera de un segundo y lotes de hasta 100 avisos, ambos configurables y validados; no se configura todavía en el código.
- Tras enviar una invalidación a los grupos locales correspondientes, avanzará su cursor. Si falla la lectura o el envío, no confirmará el aviso y reintentará con cancelación y registro de errores.
- Reiniciar una instancia conserva el cursor. La instancia que no recolecta también consume y entrega cada aviso; no necesita adquirir el bloqueo de recolección.
- Usar índice por secuencia y una clave única de cursor por identificador de instancia. Acotar las consultas; no recorrer el histórico completo en cada iteración.

## Duplicados y recuperación

La garantía propuesta es entrega al menos una vez hacia cada instancia activa, con recuperación al reiniciar mientras el aviso siga retenido. Una caída después del envío y antes del avance de cursor puede repetir el aviso. La secuencia permite deduplicarlo en cada receptor; la pantalla ya trata el aviso como invalidación y conserva un solo punto lógico por intervalo UTC.

Un envío completado por SignalR no demuestra recepción por un navegador desconectado. Al conectar/reconectar, el cliente se suscribe y consulta el estado vigente; los avisos concurrentes provocan una nueva consulta. No se promete entrega exactamente una vez ni reproducción de todos los puntos durante una desconexión del navegador.

La retención debe acordarse: conservar avisos no confirmados por instancias activas y definir cuándo retirar una instancia permanentemente apagada. Si un cursor queda anterior al historial retenido, recuperar un snapshot de MariaDB y establecer una nueva base consistente antes de continuar; nunca ignorar silenciosamente el hueco. Una pérdida de MariaDB impide entregar novedades hasta su recuperación y debe mostrarse como desconexión/error sin fabricar datos.

## Configuración de alta disponibilidad a coordinar

- Rutas del hub `/hubs/monitoreo`, negociación y conexiones de Blazor; WebSockets, tiempos de espera y afinidad en Nginx.
- Claves de Data Protection y nombre de aplicación compatibles entre instancias para validar las mismas cookies, con almacenamiento protegido y fuera del repositorio.
- Autenticación y expiración de conexión; al reemplazar la instancia de un circuito Blazor, recuperación/recreación del circuito y del parque seleccionado, además de reconectar el hub propio.
- Identificadores de instancia, parámetros de observación, credenciales con mínimos privilegios y política de retención.
- Orden de despliegue de esquema/publicador/observadores y compatibilidad durante un reemplazo gradual.

## Criterios de validación conjunta

1. Dos clientes identificados detrás de Nginx, cada uno conectado a una instancia diferente.
2. Un único recolector global persiste un lote: ambos observadores reciben el aviso y ambos clientes consultan el nuevo estado, sin puntos duplicados.
3. Alternar cuál instancia puede recolectar y repetir el escenario sin cambiar consumidores ni grupos.
4. Forzar rollback: no se distribuyen avisos del lote fallido.
5. Detener el publicador después del commit: el otro observador y el reiniciado recuperan el aviso persistido.
6. Detener un receptor después del envío pero antes de confirmar cursor: el reintento no duplica valores ni puntos.
7. Interrumpir MariaDB y recuperar: continuar desde el cursor o reconciliar un hueco declarado.
8. Reemplazar una instancia y recuperar el cliente en la otra, incluida autenticación y circuito Blazor.

Registrar latencias observadas y límite operativo acordado; no se establece un SLA. Se autorizó implementar y verificar estos puntos tras la aprobación comunicada por el usuario.
