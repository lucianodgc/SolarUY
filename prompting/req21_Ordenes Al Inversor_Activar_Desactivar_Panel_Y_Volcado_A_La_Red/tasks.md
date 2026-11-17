# Tareas: Órdenes al inversor

Fuente única de alcance técnico: `specs.md` de esta carpeta. Tareas completadas y verificadas; evidencia y límites en `implementations.md`. Las referencias D/DOM/APP/INF/PRE/V corresponden a esa especificación. Resolver cada decisión antes de implementar los cambios que dependan de ella.

## 1. Clarificaciones y compatibilidad

- [x] 1. Cerrar las decisiones de alcance, ejecución y auditoría. (Secciones 1–2)
  - [x] 1.1. Confirmar el alcance global del volcado comprobado en `IInversorClient` y definir cómo identificar ese destino en la confirmación y auditoría, sin asumir ámbito por parque/nodo. (D1)
  - [x] 1.2. Acordar interpretación de ACK/resultado falso, errores/rechazos y estados ya alcanzados; definir tratamiento de incertidumbre, reintentos, correlación de solicitudes y órdenes opuestas entre instancias. (D2)
  - [x] 1.3. Acordar campos y estados de auditoría, intentos sin autorización/sub o con destino inexistente, fallos de guardado inicial/final y recuperación después de caída. (D3)
  - [x] 1.4. Acordar evidencia y vigencia de desactivación voluntaria para salud, preservando estado administrativo, señal de falla explícita y lecturas históricas. (D4)
  - [x] 1.5. Actualizar `specs.md` y las tareas dependientes con los acuerdos antes de cerrar contratos, transiciones y migración. (Secciones 2 y 8)

## 2. Domain

- [x] 2. Modelar el intento de orden y las reglas acordadas con sus pruebas simultáneas. (DOM1–DOM3)
  - [x] 2.1. Definir las cuatro operaciones de `IntentoOrdenInversor` y sus datos de actor, destino y solicitud UTC según D1/D3. (DOM1)
  - [x] 2.2. Encapsular evolución de resultado y evidencia de confirmación, diferenciando en curso, rechazo, fallo demostrado e incertidumbre según D2/D3. (DOM1)
  - [x] 2.3. Preservar identidad, solicitud, actor y destino originales, con información de error segura y acotada; impedir finalizaciones incompatibles. (DOM1)
  - [x] 2.4. Incorporar únicamente los ajustes de interpretación operacional/salud aprobados en D4, sin convertir cero en falla ni una orden en cambio administrativo del panel. (DOM2)
  - [x] 2.5. Crear simultáneamente las pruebas xUnit de construcción, UTC, transiciones, confirmación, conservación de datos y reglas de salud afectadas. (DOM3, V1)

## 3. Application

- [x] 3. Implementar casos de uso únicos, autorización y orquestación auditada. (APP1–APP3)
  - [x] 3.1. Definir `IOrdenInversorService`, `ResultadoOrdenInversorDto` y los contratos de solicitud/correlación aprobados sin aceptar actor, roles o credenciales del navegador. (APP1)
  - [x] 3.2. Proporcionar el sub real desde el contexto autenticado del servidor mediante una ampliación compatible; exigir autenticación, Administrador efectivo y sub válido en cada operación. (APP1, D3)
  - [x] 3.3. Validar panel, inventario y destino vigentes en el servidor; resolver IDs requeridos por el puerto y detectar traslados ocurridos durante la confirmación. (APP1)
  - [x] 3.4. Definir `IIntentoOrdenInversorRepository` para registrar, completar y consultar intentos, utilizando UTC de `TimeProvider`. (APP2)
  - [x] 3.5. Implementar registro durable anterior al envío y rechazo del envío si ese registro falla; auditar validaciones/rechazos según D3. (APP2)
  - [x] 3.6. Implementar el control de solicitudes repetidas y la coordinación de órdenes entre instancias acordados en D2, sin depender del bloqueo visual. (APP2)
  - [x] 3.7. Implementar activar panel invocando `IInversorClient.ActivarPanelAsync` con destino validado y resultado auditado. (APP1–APP2)
  - [x] 3.8. Implementar desactivar panel invocando `IInversorClient.DesactivarPanelAsync` con destino validado y resultado auditado. (APP1–APP2)
  - [x] 3.9. Implementar activar volcado invocando `IInversorClient.ActivarVolcadoRedAsync` con el ámbito global confirmado. (APP1–APP2, D1)
  - [x] 3.10. Implementar desactivar volcado invocando `IInversorClient.DesactivarVolcadoRedAsync` con el ámbito global confirmado. (APP1–APP2, D1)
  - [x] 3.11. Completar resultado conocido y auditoría, separando rechazo, fallo, cancelación e incertidumbre; recuperar intentos incompletos sin reenviar por errores de persistencia. (APP2, D2–D3)
  - [x] 3.12. Delegar las cuatro rutas existentes de `InversorService` en los casos auditados, sin ciclos de dependencias ni cambios a las consultas o a la orden de encendido. (APP3)
  - [x] 3.13. Reutilizar consultas de estado operacional y la actualización existente por lecturas/telemetría, diferenciando aceptación de orden de evidencia de su efecto sin iniciar otra adquisición. (APP3)

## 4. Infrastructure

- [x] 4. Implementar persistencia de auditoría y coherencia de la integración vigente. (INF1–INF3)
  - [x] 4.1. Crear el mapeo EF de `IntentoOrdenInversor`, con UTC, enums, campos acotados y trazabilidad de destino sin cascadas destructivas ni tabla Usuario nueva. (INF1)
  - [x] 4.2. Configurar restricciones de correlación/concurrencia e índices para localizar intentos y recuperar ejecuciones incompletas según D2/D3. (INF1)
  - [x] 4.3. Generar `AddAuditoriaOrdenesInversor` como migración incremental, preservando inventario, lecturas y salud sin reconstruir auditoría anterior. (INF1)
  - [x] 4.4. Implementar `IntentoOrdenInversorRepository` con registro inicial durable, finalización controlada y recuperación acordada, sin transacciones SQL abiertas durante TCP. (INF1, APP2)
  - [x] 4.5. Corregir `InversorTcpClient.EjecutarOrdenAsync` para no descartar el booleano de resultado y no confirmar una respuesta falsa, ausente o inválida. (INF2)
  - [x] 4.6. Preservar evidencia estructurada de rechazo y la incertidumbre de comunicación, aplicando solo las ampliaciones mínimas de tipos/códigos acordadas en D2. (INF2)
  - [x] 4.7. Unificar la interpretación de activación/desactivación entre telemetría y `MedirPanelRegistrado`, conservando precisión histórica y señal de falla. (INF3)
  - [x] 4.8. Conservar las banderas de órdenes al sincronizar inventario del mismo destino y respetar condiciones de operación al volver a activar. (INF3)
  - [x] 4.9. Mantener un endpoint TCP compartido por las instancias y la integración existente de SignalR/salud, sin simuladores privados ni transporte adicional. (INF2, APP3)

## 5. Presentation y configuración

- [x] 5. Centralizar controles, confirmación y resultados en Simulador; Monitoreo solo observa efectos y enlaza a Simulador. (PRE1–PRE3)
  - [x] 5.1. Mostrar las cuatro operaciones al Administrador efectivo y conservar consulta de monitoreo/salud para Tecnico y Consulta. (PRE1)
  - [x] 5.2. Incorporar confirmación Radzen por acción con destino identificable; indicar el alcance global del volcado y no enviar al cancelar. (PRE1)
  - [x] 5.3. Obtener y presentar habilitación operacional con su disponibilidad/fecha, adaptando los DTO necesarios sin inferirla por potencia o estado administrativo. (PRE1)
  - [x] 5.4. Bloquear envíos visuales repetidos mientras la orden esté en curso, conservar el destino confirmado y descartar resultados de selecciones anteriores. (PRE1)
  - [x] 5.5. Mostrar resultado e identidad del intento según Application, distinguiendo incertidumbre y error de auditoría sin confirmaciones optimistas ni mensajes sensibles. (PRE2)
  - [x] 5.6. Adaptar los cuatro controles de `SimuladorInversor.razor` a la confirmación y los casos auditados, conservando lectura y encendido existentes. (PRE2)
  - [x] 5.7. Refrescar estado operacional y consumir nuevas lecturas por SignalR, manteniendo grupos/reconexión y sin generar mediciones ni puntos artificiales. (PRE2)
  - [x] 5.8. Registrar dependencias en el punto de composición y externalizar/validar parámetros aprobados, sin secretos ni nuevas bibliotecas. (PRE3)
  - [x] 5.9. Documentar el despliegue de la migración con un único operador, el destino común y la compatibilidad de órdenes durante reemplazo de instancias. (PRE3)

## 6. Verificación

- [x] 6. Ejecutar las comprobaciones de la especificación y conservar evidencia. (Sección 7)
  - [x] 6.1. Ejecutar pruebas de dominio nuevas y existentes, y corregir la implementación ante fallos. (V1)
  - [x] 6.2. Ejecutar `CodeQuality.Tests` sin modificar sus guardarraíles ni crear pruebas unitarias nuevas fuera de Domain. (V2)
  - [x] 6.3. Probar los cuatro casos de uso contra persistencia real y simulador TCP: métodos/destinos, ACK, resultado falso, rechazo, respuestas inválidas, pérdida de confirmación y cancelación. (V3)
  - [x] 6.4. Probar llamadas directas por Administrador, Tecnico, Consulta, anónimo, combinación de roles y actor sin sub, verificando ausencia de envío no autorizado. (V4)
  - [x] 6.5. Verificar auditoría en éxito/error y ante fallos de guardado inicial/final; comprobar identidad del servidor, destino, UTC, conservación de datos y recuperación sin reenvío ni secretos. (V5)
  - [x] 6.6. Probar desactivación, siguiente adquisición y monitoreo de 0 W; activación sin garantía de potencia, coherencia de salud y persistencia de la orden tras sincronización de inventario. (V6)
  - [x] 6.7. Probar activar/desactivar volcado, consulta del flag global y repetición de estado ya alcanzado sin suponer efectos eléctricos no modelados. (V7)
  - [x] 6.8. Verificar en navegador confirmación/cancelación, destino, doble clic, resultados, cambio de selección, reconexión y actualización automática; incluir los controles existentes del simulador. (V8)
  - [x] 6.9. Probar Nginx con dos instancias y un endpoint TCP común: órdenes desde ambas, observación en la otra, auditoría, concurrencia/repetición y reemplazo según D2/D3. (V9)
  - [x] 6.10. Registrar las verificaciones omitidas y sus causas, sin presentarlas como aprobadas. (Sección 7)

## 7. Cierre/Documentación

- [x] 7. Crear el archivo implementations.md en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.
  - [x] 7.1. Documentar acuerdos D1–D4, semántica de confirmación/reintentos, modelo y límites de auditoría, migración y configuración. (Sección 8)
  - [x] 7.2. Incluir pasos reproducibles de las cuatro órdenes, integración con entradas existentes y resultados V1–V9, con las comprobaciones omitidas. (Sección 8)
  - [x] 7.3. Revisar consistencia de requisitos, especificaciones y tareas con el alcance finalmente aprobado. (Sección 8)

## Ajuste de interfaz

- [x] Retirar el componente duplicado de órdenes y adaptar el escenario de navegador a Simulador.
- [x] Repetir el recorrido visual de esta interfaz con los cambios vigentes.
