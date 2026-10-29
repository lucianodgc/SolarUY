# Especificaciones técnicas: Recolección periódica de lecturas

## 1. Alcance y base existente

Fuente funcional: `requirements.md` de req010. Esta entrega completa la recolección automática y persistente de mediciones puntuales de potencia y salud, con exclusión entre instancias, recuperación e idempotencia. No incorpora pantallas, alertas, reportes ni nuevas consultas públicas de histórico.

Se mantienen .NET 8, EF Core 8, Pomelo.EntityFrameworkCore.MySql 8.0.3 y MariaDB 11.8 LTS. No se introducen bibliotecas, proveedores ni MediatR. Domain usa C# puro; Application depende únicamente de Domain; Infrastructure implementa los contratos; Presentation configura y aloja el proceso.

El repositorio ya contiene `LecturaPanel`, `EstadoSaludLectura`, `RecoleccionLecturasService`, `IInversorClient`, `RecoleccionLecturasRepository`, `RecoleccionLecturasWorker` y la migración `AddLecturaPanel`. Se deben adaptar estos componentes, sin duplicar el circuito de recolección ni crear otra tabla de mediciones sin resolver antes las decisiones de la sección 2.

Brechas observadas:

- La entidad guarda `FechaHora` UTC y potencia en W, pero no conserva directamente el nodo histórico.
- El índice actual `(PanelId, FechaHora, PotenciaGeneradaW)` no es único y no garantiza la identidad de la medición.
- El repositorio omite paneles que ya tienen una lectura en el mismo minuto; esto impide recolectar con intervalos menores de un minuto.
- El intervalo actual es un `TimeSpan`, limitado a entre un segundo y cinco minutos; req010 exige configuración en segundos y positividad, sin definir ese máximo.
- El DTO de medición no devuelve una fecha de origen: el simulador recibe el instante del servicio. Un nuevo `UtcNow` por intento no identifica establemente una misma medición.

## 2. Decisiones confirmadas y dependencias

El usuario confirmó las decisiones siguientes el 28/10/2026. La tabla original identifica el conflicto de partida; la resolución indicada debajo gobierna la implementación.

| Id | Duda | Impacto |
|---|---|---|
| D1 | Req010 exige `Lectura` y menciona `FechaHoraUtc`; el modelo de referencia y los consumidores existentes usan `LecturaPanel.FechaHora`. Confirmar si estos nombres existentes satisfacen el requerimiento o se exige un renombrado compatible. | No crear una segunda entidad/tabla ni renombrar consumidores por suposición. |
| D2 | Definir cómo conservar el nodo de la medición ante cambios de topología. El modelo solo relaciona la lectura con el panel; req010 exige nodo obligatorio e histórico inalterable. Confirmar si se autoriza incorporar relación histórica `Nodo`/`NodoId` o si existe una regla de inmutabilidad de asignación que permita derivarlo. | Determina el modelo, las relaciones y el índice por nodo; no reconstruir un nodo histórico desconocido con la topología actual. |
| D3 | Confirmar el instante estable: fecha de medición provista por el origen o política explícita para asignarlo en el simulador y conservarlo entre reintentos e instancias. Definir también precisión temporal y normalización compatibles con MariaDB. | Bloquea la idempotencia final. No elegir arbitrariamente ventanas de tiempo ni usar la hora de cada reintento. |
| D4 | Definir tratamiento de posibles duplicados ya almacenados antes de añadir unicidad. | La migración no puede borrar datos ni elegir qué lectura descartar sin decisión expresa. Ante duplicados debe detenerse con diagnóstico y requerir resolución. |

Resolución aprobada:

- D1: conservar `LecturaPanel.FechaHora` y la tabla `LecturasPanel`, sin renombrar consumidores.
- D2, actualización solicitada el 29/10/2026: guardar `NodoId` y navegación `Nodo` como asociación histórica. Las mediciones nuevas requieren un nodo y lo fijan al construirse. Conservar las filas anteriores sin procedencia demostrable con `NodoId = NULL`, sin completar desde `Panel.NodoId` ni usar una variable de sesión como prueba. Esas filas siguen consultables por panel; no participan en agregados por nodo/parque ni en potencia vigente. Mostrar su existencia en histórico/reportes y CSV, sin atribuirlas al parque seleccionado. Su eventual certificación exige evidencia por panel e intervalo y una operación posterior explícita y auditable; queda fuera de esta reparación.
- D3: para el simulador, usar límites de intervalos UTC de N segundos desde el origen de `DateTime`, truncando hacia abajo el instante actual. Cada límite identifica una muestra puntual del simulador, no un agregado del intervalo. El mismo intervalo e inventario permiten reintentar sin cambiar la identidad. Todas las instancias deben usar el mismo N y relojes UTC sincronizados. Las fechas de lectura se truncan a microsegundos y se almacenan en `datetime(6)`; un futuro origen real deberá suministrar su instante estable y requiere revisar este contrato.
- D4: detectar duplicados por panel/fecha antes de cualquier DDL y detener con un diagnóstico; no eliminar ni consolidar automáticamente filas anteriores. Detener los recolectores durante la migración para evitar escrituras entre comprobaciones y cambios de esquema.

## 3. Domain

### DOM1. Medición puntual y validaciones

Adaptar la entidad existente conforme a D1 y D2. Mantener identificador `Guid` generado por el constructor, referencias de navegación y `private set`. Panel y nodo deben ser obligatorios y coherentes al medir; no basta con aceptar identificadores ajenos sin verificar la relación. La existencia persistida se garantiza además mediante claves foráneas y la selección de topología en Application/Infrastructure.

Mantener `DateTime` obligatorio, distinto de `default` y con `Kind.Utc`; aplicar la política de instante y precisión aprobada en D3. No sustituir la medición por la hora local ni por la hora de guardado. Conservar potencia `decimal` en vatios, precisión existente `decimal(18,6)`, valor no negativo y representable. Reutilizar `EstadoSaludLectura` (`Sano`, `Fallado`, `Desconocido`) sin inventar estados ni convertir errores de comunicación en estados o mediciones ficticios.

### DOM2. Conservación del histórico

La potencia, fecha, salud y asociación histórica aprobada se fijan al construir la lectura. Cambios posteriores del panel, nodo o inversor no actualizan automáticamente esos datos. No se agregan operaciones de modificación de lecturas ni totales agregados. La unicidad global no se implementa en memoria dentro de una entidad: corresponde a persistencia.

## 4. Application

### APP1. Contratos y orquestación

Reutilizar `IRecoleccionLecturasService`/`RecoleccionLecturasService`, `IRecoleccionLecturasRepository`, `ISesionRecoleccionLecturas`, `IInversorClient` y los DTO existentes. Ajustar únicamente los contratos necesarios para transportar la fecha estable y la asociación histórica aprobadas; no exponer EF Core, SQL ni tipos del hospedaje.

Inyectar `ConfiguracionRecoleccion` con el intervalo validado desde la composición de Presentation. El contrato existente que recibe `instanteUtc` permite que el simulador mida en el límite acordado sin agregar una fecha redundante al DTO. Identificar conflictos de unicidad mediante `LecturaDuplicadaException`, definida en Application y generada por Infrastructure.

El ciclo intenta adquirir la sesión exclusiva antes de consultar inversores. Si no obtiene exclusión, termina sin consultar ni guardar. Con exclusión, obtiene la topología persistida, consulta las mediciones correspondientes, valida identidad y pertenencia de los paneles, construye las lecturas y solicita su persistencia. No crea parques, nodos, paneles ni inversores. Una respuesta ajena, duplicada o inválida no se transforma en datos válidos por defecto.

### APP2. Instante, cancelación y errores

Reemplazar la exclusión artificial de una lectura por minuto por la identificación exacta aprobada en D3. Dos mediciones distintas dentro de un minuto deben poder persistirse; reintentar la misma medición debe conservar su clave temporal y no duplicarla. La lectura repetida se reconoce por panel e instante normalizado, nunca por potencia ni por un nuevo identificador Guid.

Propagar `CancellationToken` en adquisición, consulta y persistencia; liberar siempre la sesión con `await using`. Mantener el comportamiento existente de validar el lote antes de guardarlo y persistirlo atómicamente. Un fallo descarta el lote no confirmado, se comunica al worker para registro y permite un ciclo posterior. No añadir reintentos automáticos que cambien el instante o pierdan exclusión. Un conflicto de unicidad debe distinguirse de otros errores de base de datos y tratarse sin sobrescribir el histórico.

## 5. Infrastructure

### INF1. Persistencia, índices y migración incremental

Adaptar `LecturaPanelConfiguration`, el contexto y los mapeos afectados por D1/D2. Mantener panel obligatorio y borrado restrictivo; el nodo admite NULL exclusivamente para preservar filas legadas sin evidencia. El constructor y el repositorio de recolección siguen exigiéndolo en mediciones nuevas. Guardar/materializar fechas como UTC y configurar explícitamente la precisión temporal acordada en D3, de modo que la identidad comparada por Application coincida con la almacenada.

Crear un índice único `(PanelId, FechaHora)` usando el nombre aprobado en D1 para la fecha; equivale a la clave funcional `(PanelId, FechaHoraUtc)`. Su prefijo permite consultas por panel y rango temporal. Para nodo y rango, según D2: usar índice `(NodoId, FechaHora)` si se autoriza guardar el nodo histórico, o el recorrido por paneles con índice en `Panel.NodoId` y el índice temporal de lecturas si se confirma que la asociación no cambia. No introducir servicios de consulta ni alterar pantallas históricas.

Conservar adicionalmente el índice existente `(PanelId, FechaHora, PotenciaGeneradaW)`, utilizado como índice de cobertura por el histórico. La unicidad nueva no lo reemplaza: retirarlo provoca una regresión en la verificación existente de un millón de lecturas. Mantener los límites y aserciones originales de esa prueba.

Mantener las migraciones aplicadas. Excepción de reparación solicitada el 29/10/2026: se comprobó en MariaDB que `AddRecoleccionPeriodica` no estaba aplicada ni había ejecutado su DDL; ajustar esa migración pendiente para conservar NodoId NULL sin atribución. Agregar `PreserveUnknownHistoricalNodes` para compatibilizar instalaciones donde la versión anterior sí estaba aplicada, sin modificar sus nodos existentes. Proteger la reversión mientras existan nodos desconocidos. Verificar duplicados antes de imponer unicidad y aplicar D4. Respaldar/restaurar en una copia y comparar todos los campos originales antes de operar sobre la base habitual. Ante DDL parcial, detenerse e inspeccionar: no alterar el historial para saltarlo.

### INF2. Coordinación compartida

Conservar el mecanismo existente de bloqueo asesor de MariaDB: `GET_LOCK` con espera cero y nombre común derivado de la base, manteniendo una conexión física dedicada, sin pooling, durante toda la sesión. Solo su propietario consulta y persiste; las demás instancias omiten ese ciclo y vuelven a intentarlo después.

Comprobar la propiedad con `IS_USED_LOCK`/`CONNECTION_ID` antes de guardar y usar la misma conexión para persistir. No reconectar ni reintentar escrituras silenciosamente tras perder la conexión/bloqueo: abortar el ciclo. Liberar mediante `RELEASE_LOCK` y cerrar la conexión incluso ante error o cancelación. El índice único sigue siendo la última defensa frente a concurrencia y reintentos.

Al cerrar normalmente o cuando MariaDB detecta la desconexión, el bloqueo se libera y otra instancia puede adquirirlo en un ciclo posterior. Una interrupción de red puede retrasar la detección: no se promete recuperación instantánea. La implementación debe asegurar que perder coordinación impida continuar consultas/escrituras bajo una sesión inválida; validar este comportamiento con desconexión forzada y no solo con cierre ordenado. No se agregan tablas de lease ni coordinadores externos.

### INF3. Integración con inversores

Mantener `InversorSimulador` detrás de `IInversorClient`, utilizando la topología persistida para la recolección. Adaptar el transporte del instante según D3 y mantener W como unidad explícita. No construir una integración con hardware o un protocolo nuevo. La simulación prevista es una fuente válida del entorno; una falla al consultarla no habilita fabricar lecturas de reemplazo.

## 6. Presentation

### PRE1. Hospedaje y configuración

Adaptar `RecoleccionLecturasWorker` y `RecoleccionLecturasOptions`; mantener el registro con `AddHostedService` y el servicio de Application por DI en `Program.cs`. El worker se inicia automáticamente tras el arranque y ejecuta ciclos secuenciales sin solaparse localmente. Si un ciclo excede N segundos, no iniciar otro en paralelo; la exclusión compartida se exige además en cada ciclo.

Definir `RecoleccionLecturas:IntervaloSegundos` como entero obligatorio y positivo, convertido a `TimeSpan` tras validarlo. Declarar el valor en `appsettings.json` y permitir sobrescritura mediante `RecoleccionLecturas__IntervaloSegundos`. Valores ausentes, nulos, no numéricos, cero, negativos o no representables deben producir un error claro al arrancar con `ValidateOnStart`; no conservar el límite arbitrario de cinco minutos. Sustituir la antigua clave `Intervalo` y documentar el cambio. Mantener la opción existente `Habilitada` sin ampliar su alcance.

Crear un alcance asíncrono por ciclo y resolver dentro de él el servicio; no capturar un `DbContext` scoped desde el singleton. Propagar la cancelación a la espera y al ciclo y tratar el cierre solicitado como normal. Registrar fallos de consulta/persistencia con contexto del ciclo y continuar en ciclos posteriores. Credenciales y cadenas de conexión permanecen fuera del repositorio.

La representabilidad del temporizador limita técnicamente N a 4.294.967 segundos: `PeriodicTimer` admite hasta `uint.MaxValue - 1` milisegundos. Validar este límite en opciones al arrancar con diagnóstico de la clave; no equivale a restaurar el máximo funcional anterior de cinco minutos.

### PRE2. Interfaz

Los componentes Blazor, navegación, controladores y gráficos no requieren modificaciones. La única intervención de Presentation corresponde al hospedaje, DI y configuración descritos en PRE1.

## 7. Verificación de la implementación

Estas verificaciones corresponden a la futura implementación, no a la generación de estos documentos.

- V1: Crear o adaptar pruebas xUnit de Domain junto con los cambios de comportamiento: identificador, panel/nodo coherentes según D2, fecha obligatoria UTC, potencia representable en W y salud válida. Comprobar que modificar el estado actual no reescribe la lectura.
- V2: Ejecutar `dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj`; mantener ambas suites en verde sin relajar pruebas arquitectónicas.
- V3: Verificar en MariaDB 11.8 la migración desde una base nueva y otra con datos previos; comprobar conservación de filas, claves foráneas, precisión, índices y diagnóstico de duplicados preexistentes. Consultar por panel y nodo con rangos temporales y examinar el uso de índices.
- V4: Comprobar unicidad real con dos inserciones concurrentes del mismo panel/instante y con reintentos. Verificar que instantes distintos, incluso dentro de un minuto, se conservan y que normalizar la precisión no produce duplicados silenciosos.
- V5: Reutilizar las verificaciones de integración existentes en `SolarUY.Integration.Tests` y realizar una ejecución con dos procesos de la aplicación sobre la misma base: comprobar exclusión antes de consultar, ausencia de duplicados, relevo tras detener al propietario y recuperación tras desconexión/pérdida del bloqueo. Dos proveedores de DI en un proceso no sustituyen la prueba solicitada con dos instancias.
- V6: Verificar arranque y persistencia automática con N configurable, sobrescritura por entorno, rechazo de configuración inválida, cancelación y liberación de recursos. Inyectar fallos transitorios de consulta y guardado; confirmar registro del error y éxito posterior sin datos ficticios.
- No crear pruebas unitarias de Application, Infrastructure o Presentation. La unicidad entre lecturas se comprueba en integración contra MariaDB; una entidad aislada no puede garantizarla. No modificar `CodeQuality.Tests` para adaptar sus reglas al diseño.

## 8. Cierre y documentación

Al terminar la implementación, crear `implementations.md` en esta carpeta con cambios realizados, archivos tocados y resultados de pruebas. Incluir decisiones D1–D4 resueltas, mecanismo de bloqueo y límites de recuperación, configuración del intervalo, forma de ejecutar el servicio y pasos reproducibles de concurrencia, fallos y recuperación. Identificar expresamente pruebas omitidas y su motivo, sin presentarlas como aprobadas.
