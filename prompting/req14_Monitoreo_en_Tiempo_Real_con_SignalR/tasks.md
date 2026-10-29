# Tareas: Monitoreo en tiempo real con SignalR

Fuente de alcance técnico: `specs.md` de esta carpeta. Estado actualizado al 29/10/2026; resultados y límites en `implementations.md`. Las referencias corresponden a las secciones de esa especificación; las decisiones D1–D5 se resuelven antes de implementar sus cambios dependientes.

D1–D5 fueron confirmadas. El usuario comunicó la aprobación de Luciano para D2 y autorizó completar la distribución con MariaDB compartida. También autorizó Playwright con Chromium aislado para las verificaciones de pantalla. Los resultados finales se registran en implementations.md.

## 1. Clarificaciones e integración con alta disponibilidad

- [x] 1. Resolver y documentar las decisiones previas. (Sección 2)
  - [x] 1.1. Acordar vigencia, compatibilidad entre instantes, expiración sin eventos nuevos y significado de agregados incompletos. (D1)
  - [x] 1.2. Coordinar con Luciano el transporte compartido, recuperación de avisos perdidos, dependencias/versiones autorizadas y responsabilidades de despliegue. (D2)
    - [x] 1.2.1. Preparar la propuesta con MariaDB compartida en `propuesta_distribucion_mariadb.md`, sin agregar servicios; implementar sus tablas/observadores después de la aprobación.
    - [x] 1.2.2. Obtener la coordinación y validación de Luciano antes de implementar definitivamente la propuesta.
  - [x] 1.3. Definir máximo de puntos, fecha de referencia del agregado, tratamiento gráfico de cobertura incompleta y contenido inicial de la curva. (D3)
  - [x] 1.4. Confirmar cliente SignalR de navegador, versión y distribución aprobadas para conectar la pantalla al hub propio. (D4)
  - [x] 1.5. Acordar la pertenencia de muestras a nodos/parques cuando cambia la topología, sin modificar el histórico ni duplicar contribuciones. (D5)
  - [x] 1.6. Actualizar `specs.md` y las tareas afectadas con los acuerdos antes de cerrar contratos, cálculos o configuración dependientes. (Sección 2)

## 2. Domain

- [x] 2. Implementar el cálculo de generación y vigencia sin alterar entidades persistentes. (DOM1)
  - [x] 2.1. Crear `GeneracionActualService` con referencia UTC explícita y selección de una lectura válida por panel según D1/D5; crear simultáneamente las pruebas xUnit de selección y vigencia. (DOM1, V1)
  - [x] 2.2. Calcular potencia en W de nodos y parque sin sumar un panel dos veces y devolver cobertura/fechas según D1; incluir pruebas xUnit de agregados completos e incompletos. (DOM1, V1)
  - [x] 2.3. Diferenciar cero, ausencia y falta de vigencia, evitando retrocesos por muestras atrasadas o contribuciones repetidas; cubrir estos casos con pruebas de dominio. (DOM1, V1)

## 3. Application

- [x] 3. Incorporar consultas y contratos independientes de SignalR y EF Core. (APP1–APP3)
  - [x] 3.1. Definir `IMonitoreoRepository`, `IMonitoreoService` y los DTO `EstadoMonitoreoDto`, `NodoMonitoreoDto`, `PanelMonitoreoDto` y `PuntoGeneracionDto` según las decisiones aprobadas. (APP1)
  - [x] 3.2. Implementar `MonitoreoService` para listar parques y consultar estado, validando autenticación, rol, identificador y existencia antes del acceso a datos, con cancelación. (APP1)
  - [x] 3.3. Delegar el cálculo en `GeneracionActualService` y entregar a la vista potencia, disponibilidad, cobertura, fechas UTC y puntos ya calculados. (APP1, DOM1)
  - [x] 3.4. Definir `INotificadorLecturasConfirmadas` y el aviso de invalidación con parques afectados e identidad panel/instante, conforme a D2. (APP2)
  - [x] 3.5. Integrar la notificación después del guardado confirmado en el circuito existente; distinguir fallo de publicación de fallo de persistencia y no producir otra adquisición. (APP2)
  - [x] 3.6. Completar la reconciliación y recuperación según D2 mediante avisos transaccionales y cursores independientes, carga inicial, reconexión y reevaluación temporal. (APP2, APP3)

## 4. Infrastructure

- [x] 4. Implementar acceso de lectura y distribución acordada. (INF1, INF2)
  - [x] 4.1. Crear `MonitoreoRepository` con consultas de solo lectura a topología y últimas muestras del parque, sin cargar todo el histórico ni consultar individualmente por panel. (INF1)
  - [x] 4.2. Reutilizar los índices existentes y obtener un conjunto coherente para el cálculo, preservando paneles sin datos y la pertenencia aprobada en D5. (INF1)
  - [x] 4.3. Aplicar D3: iniciar con el punto vigente, sin consultar una ventana histórica adicional. (INF1)
  - [x] 4.4. Implementar la publicación atómica en el repositorio de recolección y el contrato `IDistribucionMonitoreoRepository`; recibir en cada instancia y entregar mediante `INotificadorLecturasConfirmadas`, sin referencias de Infrastructure a Presentation. (INF2)
  - [x] 4.5. Incorporar reconexión del transporte, recuperación de avisos omitidos y liberación de recursos según D2; no agregar persistencia técnica sin documentarla y aprobarla previamente. (APP2, INF2)

## 5. Presentation y despliegue

- [x] 5. Incorporar hub, pantalla, gráfico y configuración coordinada. (PRE1–PRE4)
  - [x] 5.1. Crear y mapear `MonitoreoHub`, con autenticación y roles `Administrador`, `Tecnico` y `Consulta`, sin operaciones de escritura. (PRE1)
  - [x] 5.2. Validar autorización y existencia del parque antes de suscribir conexiones a sus grupos, incluidas las nuevas suscripciones después de reconectar. (PRE1)
  - [x] 5.3. Implementar el adaptador local con `IHubContext<MonitoreoHub>`; su conexión al receptor distribuido se completará en 4.4 tras validar D2. (PRE1, INF2)
  - [x] 5.4. Crear `Monitoreo.razor` y su entrada de navegación, con selección de parque y jerarquía identificable de nodos/paneles, potencia en W y fechas UTC. (PRE2)
  - [x] 5.5. Representar cobertura, ausencia, falta de vigencia y cero medido a partir del modelo recibido, sin cálculos de negocio en Razor o JavaScript. (PRE2)
  - [x] 5.6. Conectar el cliente aprobado en D4 al hub propio y adaptar los avisos al componente para consultar `IMonitoreoService` y actualizar el renderizado. (PRE2)
  - [x] 5.7. Gestionar carga inicial, estado de conexión, resuscripción y recuperación tras reconectar; descartar respuestas atrasadas y del parque abandonado. (APP3, PRE2)
  - [x] 5.8. Liberar conexiones, callbacks y recursos de interoperabilidad al salir, y evitar suscripciones duplicadas al cambiar de parque. (PRE2)
  - [x] 5.9. Incorporar el gráfico Radzen de potencia del parque con UTC/W, orden temporal, deduplicación y actualización del mismo punto lógico según D3. (PRE3)
  - [x] 5.10. Acotar los puntos a `MaximoPuntosGrafico` y representar huecos/cobertura incompleta según D1/D3, sin crear puntos por la mera llegada de avisos. (PRE3)
  - [x] 5.11. Registrar servicios y adaptadores en `Program.cs`, externalizar y validar configuración aprobada sin secretos, y conservar las reglas de recolección existentes. (PRE4)
  - [x] 5.12. Coordinar y aplicar con Luciano las rutas, negociación, WebSocket, afinidad y reemplazo de instancias en Nginx, junto con el transporte D2. (PRE4)

## 6. Verificación

- [x] 6. Ejecutar las verificaciones de la especificación y conservar evidencias. (Sección 7)
  - [x] 6.1. Ejecutar las pruebas de Domain y `CodeQuality.Tests`, manteniendo los guardarraíles sin modificaciones y corrigiendo el código si falla. (V1, V2)
  - [x] 6.2. Verificar en MariaDB estado inicial con/sin datos, últimas muestras acotadas, coherencia de agregados, pertenencia, unidades e índices; comprobar ausencia de escrituras o consultas a inversores desde monitoreo. (V3)
  - [x] 6.3. Verificar V4: commit atómico, rollback de lecturas/aviso/secuencia, fallo de entrega y recuperación independiente por instancia sin repetir la adquisición. (V4)
  - [x] 6.4. Comprobar actualización automática de valores/gráfico sin recarga, límite de puntos, eventos repetidos/atrasados, cambios de parque y respuestas tardías. (V5)
  - [x] 6.5. Verificar en navegador la liberación de suscripciones al abandonar la página y la expiración sin nuevas mediciones, incluida una interrupción real de MariaDB. (V5)
  - [x] 6.6. Probar los tres roles, rechazo de anónimos/roles ajenos, autorización de consultas y grupos del hub, y ausencia de operaciones de escritura expuestas. (V6)
  - [x] 6.7. Verificar V7 en navegador con el transporte D2, reconexión WebSocket y recuperación tras interrupción; comprobar también cierre por expiración de cookie. (V7)
  - [x] 6.8. Ejecutar Nginx con dos instancias y clientes en instancias distintas; comprobar recepción en ambas de lecturas persistidas desde cualquiera, manteniendo una sola recolección global. (V8)
  - [x] 6.9. Reemplazar una instancia y reconectar contra la otra, verificando recuperación desde MariaDB; registrar configuración y cualquier comprobación operativa no ejecutada. (V8)

## 7. Cierre/Documentación

- [x] 7. Crear el archivo implementations.md en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.
  - [x] 7.1. Documentar acuerdos D1–D5, coordinación con Luciano, configuración externa y pasos de ejecución del monitoreo. (Sección 8)
  - [x] 7.2. Incluir pasos reproducibles y resultados de dos instancias, reconexión y lecturas atrasadas; identificar verificaciones omitidas y sus causas. (Sección 8)
