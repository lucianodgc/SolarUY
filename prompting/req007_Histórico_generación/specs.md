# Especificaciones técnicas: Histórico de generación por día, mes y año

## Contexto, alcance y estado

Fuente funcional: `requirements.md` de req007. Este diseño contempla consultas históricas por parque, nodo o panel, períodos diarios, mensuales y anuales, comparación de rangos, máximos y mínimos, servicios reutilizables y visualización. Los cálculos se ejecutan mediante agregaciones en la base de datos y las consultas no modifican las lecturas.

Se mantienen .NET 8, MariaDB 11.8 LTS, Entity Framework Core 8 con Pomelo 8.0.3, Keycloak mediante OpenID Connect y Blazor Server con Radzen. No se incorporan MediatR, proveedores ni bibliotecas nuevas.

Las decisiones Q1–Q8 quedaron confirmadas por el usuario el 22/10/2026. La sección siguiente fija la interpretación que se implementa y verifica.

### C0. Situación comprobada al iniciar req007

- Existen `ParqueSolar`, `Nodo` y `Panel`, sus relaciones y su persistencia. Se reutilizan sin agregar altas ni modificar sus atributos.
- `requirements.md` menciona la entidad `Lectura`. El modelo de datos del proyecto la denomina `LecturaPanel`: se utiliza este nombre, sin crear una segunda entidad equivalente.
- No existe todavía `LecturaPanel` en Domain ni su conjunto/configuración en `ApplicationDbContext`. Su incorporación mínima es una dependencia necesaria del histórico, sujeta a Q1 y Q2.
- El modelo previsto contiene `Id`, `PanelId`/`Panel`, `FechaHora`, `PotenciaGeneradaW` y `EstadoSalud`. Almacena potencia instantánea en W, no energía en kWh.
- El simulador de req005 expone identificadores `string`, usa una topología independiente y no persiste lecturas del inventario, cuyos identificadores son `Guid`. No proporciona automáticamente la fuente histórica de req007.
- La baja de parques de req006 ya contempla eliminar `LecturasPanel` cuando la tabla existe. La nueva relación debe ser compatible con ese comportamiento; consultar el histórico no agrega operaciones de baja ni cambia la política existente.

## Decisiones confirmadas y dependencias

El usuario aprobó las siguientes respuestas a Q1–Q8; prevalecen sobre las preguntas de trazabilidad de la tabla inferior:

- **Q1:** mantener la potencia hasta la siguiente muestra, como máximo cinco minutos. Promediar duplicados por panel/instante. Integrar exclusivamente el solapamiento con cada intervalo consultado, incluyendo muestras de los cinco minutos previos al inicio. Energía en kWh sin redondeos intermedios, presentación a tres decimales. Por ejemplo, 1200 W durante cinco minutos aportan 0,1 kWh; 600 W durante dos minutos aportan 0,02 kWh.
- **Q2:** `EstadoSaludLectura` contiene `Sano`, `Fallado` y `Desconocido`. Potencia no negativa, persistida como decimal(18,6); rechazar valores no representables sin pérdida. La salud no altera la potencia medida.
- **Q3:** zona `America/Montevideo`, límites `[inicio, fin)`. Día seleccionado completo con serie horaria, mes completo con serie diaria y año completo con serie mensual. Rango personalizado con límites locales explícitos y serie diaria, recortando los intervalos extremos. Comparar totales observados sin normalizar la duración. Horas representadas sobre la línea UTC para respetar cambios históricos de horario; entradas locales ambiguas o inexistentes se rechazan con un mensaje de validación.
- **Q4:** extremos de energía de los intervalos de la serie, después de sumar paneles, mostrando todos los empates. Excluir intervalos sin cobertura y conservar los de cobertura parcial como energía observada.
- **Q5:** mostrar todos los intervalos; energía `null` sin cobertura y cero únicamente cuando hubo medición igual a cero. Informar cobertura completa/parcial/sin datos según tiempo cubierto frente al tiempo esperado de los paneles actuales del objetivo; un objetivo sin paneles no tiene datos. Los totales parciales se etiquetan como observados, sin imputación de huecos. No se reconstruyen cambios históricos de pertenencia o instalación que el modelo no registra.
- **Q6:** `Administrador`, `Tecnico` y `Consulta` pueden leer todos los parques, nodos y paneles. Validar identidad y rol en cada operación del servicio, incluidas opciones y comparaciones.
- **Q7:** un millón de lecturas, cuatro consultas concurrentes, menos de cinco segundos por consulta y menos de 256 MiB adicionales en el proceso de la aplicación; registrar entorno, distribución y mediciones. Medir lecturas diarias, mensuales, anuales y comparación por panel/nodo/parque.
- **Q8:** la consulta consume exclusivamente lecturas persistidas. La recolección automática, inicialmente excluida, fue solicitada posteriormente por el usuario y queda definida en R1.

Se generan límites de intervalos en Application y se transfieren como un parámetro JSON a `JSON_TABLE` en MariaDB. El repositorio usa CTE, `LEAD` y agregaciones/ventanas SQL para deduplicar, acotar tramos, integrar, calcular cobertura, totales y extremos sin materializar muestras en la aplicación. La migración se aplica en la base aislada de verificación; el despliegue de la migración en la base habitual se documenta como paso operativo.

La primera prueba de volumen detectó desbordamiento de temporales a disco con el límite predeterminado de 16 MiB. Se amplía el índice a `(PanelId, FechaHora, PotenciaGeneradaW)` para cubrir la consulta y se utiliza `SET STATEMENT` para permitir hasta 256 MiB por tabla temporal en esta consulta, sin modificar la configuración global del servidor. Se materializa una única selección de paneles y se usa un equijoin por día UTC para acotar los cruces entre tramos e intervalos. Esta memoria corresponde al motor MariaDB; el límite acordado de memoria adicional de la aplicación se mide aparte.

Las respuestas anteriores cierran contratos, fórmulas y casos esperados sin agregar atributos, entidades, tecnologías o relaciones ajenos al modelo autorizado. R1 incorpora el worker y la medición del inventario mediante el simulador, con ejecución exclusiva entre instancias. No se reconstruyen lecturas anteriores a su activación.

## 1. Domain

### D1. LecturaPanel como dependencia mínima

Incorporar `LecturaPanel` con los atributos ya previstos por el modelo, sin agregar energía acumulada, duración de muestra ni totales persistidos de parque/nodo. `Id` es un `Guid` generado por el constructor; la lectura conserva la relación de navegación con `Panel` y su `PanelId`. `FechaHora` es un `DateTime` UTC válido; `PotenciaGeneradaW` es `decimal`. La definición de `EstadoSalud` y las restricciones de potencia son las confirmadas en Q2.

La entidad utiliza constructor con validación, propiedades con `private set` y C# puro. No expone operaciones de edición de lecturas para esta funcionalidad. No se modifican `ParqueSolar`, `Nodo`, `Panel`, `Inversor` ni mantenimiento para completar artificialmente el modelo.

### D2. Reglas y pruebas de dominio

No se introduce un agregado de histórico ni una entidad para almacenar resultados. La regla funcional de energía de Q1 debe ser respetada por la agregación en Infrastructure; no se recorre una colección de lecturas en Domain para calcular el histórico.

Crear pruebas xUnit en `SolarUY.Domain.Tests` junto con el constructor de `LecturaPanel`: generación de identidad, asociación con panel, conservación de potencia/fecha/estado y rechazo de asociación nula, fechas inválidas/no UTC y estados inválidos según Q2. Agregar los casos de validación de potencia solo cuando Q2 los defina. Si las aclaraciones justifican comportamiento adicional en Domain, especificarlo primero y acompañarlo de su prueba unitaria.

## 2. Application

### A1. Servicio reutilizable y contratos

Crear el módulo `SolarUY.Application.HistoricoGeneracion` con `IHistoricoGeneracionService`/`HistoricoGeneracionService`. Expone operaciones asíncronas `ConsultarAsync`, `CompararAsync` y `ListarOpcionesAsync`, con `CancellationToken` e inyección por constructor. El servicio depende del puerto `IHistoricoGeneracionRepository` y de `IUsuarioActual`, sin referencias a EF Core, SQL, Blazor ni Infrastructure.

Definir los DTO necesarios con los campos temporales y de extremos acordados en Q1–Q5:

| Contrato | Responsabilidad |
| --- | --- |
| `FiltroHistoricoDto` | Identificar un único objetivo mediante tipo parque/nodo/panel e identificador `Guid`; expresar período diario/mensual/anual o rango y granularidad acordada. |
| `ResultadoHistoricoDto` | Identificar objetivo, período efectivo, unidad y total de energía, serie agregada, máximos/mínimos y situación de disponibilidad de datos según Q1–Q5. |
| `ComparacionHistoricoDto` | Entregar resultados identificados de los períodos solicitados para su presentación conjunta, conservando los límites y unidades de cada período. No agregar porcentajes o normalizaciones que no estén definidos por Q3. |
| `OpcionHistoricoDto` | Proveer tipo, identificador, etiqueta y relación con parque/nodo cuando corresponda, para seleccionar únicamente objetivos autorizados del inventario persistido. |

`IHistoricoGeneracionRepository` expone consultas agregadas, comprobación de existencia del objetivo y opciones de selección dentro del alcance autorizado. Devuelve DTO/proyecciones agregadas; no expone `DbContext`, `IQueryable` ni colecciones completas de `LecturaPanel`. Según Q6 el alcance es global para los tres roles, autorizado previamente en Application; no se agregan filtros de pertenencia por usuario inexistentes en el modelo.

### A2. Validación, permisos y orquestación

Antes de acceder a datos, verificar mediante `IUsuarioActual` identidad autenticada, identificador no vacío y política de acceso acordada en Q6. Aplicarla también a consultas directas por identificador, comparaciones y opciones; ocultar un elemento en la interfaz no constituye autorización.

Validar tipo de objetivo, identificador no vacío y períodos válidos, rechazando inicio posterior al fin. Resolver límites de día/mes/año y conversión a UTC según Q3 en el servicio, sin delegarlos a la vista. Distinguir entradas inválidas, acceso denegado y objetivo inexistente mediante errores controlados conforme a las convenciones existentes; el tratamiento del objetivo existente sin lecturas depende de Q5.

Cada rango de una comparación utiliza el mismo objetivo autorizado y las mismas reglas de unidad, agregación y disponibilidad. Application coordina consultas y entrega resultados listos para mostrar. No carga muestras para integrar, sumar o buscar extremos en memoria. No se añaden cambios de panel, órdenes al simulador ni escrituras de históricos.

### A3. Reutilización

Los contratos no dependen de componentes, sesión visual ni formatos de Radzen. Una funcionalidad futura, incluido el chatbot, podrá invocar el mismo servicio bajo la identidad y permisos del usuario. No se implementa el chatbot ni se le da acceso directo a la base de datos.

## 3. Infrastructure

### I1. Persistencia mínima

Agregar `DbSet<LecturaPanel>`, `LecturaPanelConfiguration` y la migración `AddLecturaPanel` en `SolarUY.Infrastructure/Migrations`, actualizando el snapshot generado sin editar migraciones históricas. Usar la tabla `LecturasPanel`, clave `Id`, FK `PanelId` a `Paneles` y relación restrictiva compatible con la baja explícita existente de req006. No activar nuevas cascadas al consultar ni cambiar la política de eliminación.

Configurar fecha UTC, incluida la recuperación con `DateTimeKind.Utc`, estado y precisión decimal conforme a Q2. Incorporar un índice compuesto no único `(PanelId, FechaHora, PotenciaGeneradaW)` para filtrado por panel/tiempo y cobertura de la consulta. No imponer unicidad de muestras: Q1 define el promedio de duplicados. Se conservan las relaciones existentes `Panel.NodoId` y `Nodo.ParqueId`; no se agregan claves redundantes de parque/nodo en cada lectura.

### I2. Repositorio y agregaciones en base de datos

Implementar `HistoricoGeneracionRepository` con `IDbContextFactory<ApplicationDbContext>` inyectado y un contexto de duración limitada por operación, siguiendo el patrón existente de `PanelRepository`. Registrar el puerto en `AddSolarInfrastructure`. La consulta usa parámetros, `CancellationToken` y operaciones de lectura sin seguimiento.

Filtrar desde `LecturasPanel` por objetivo y límites UTC: un panel solo aporta sus lecturas; un nodo aporta las de sus paneles; un parque aporta las de los paneles de sus nodos. Resolver pertenencia mediante relaciones y filtros en SQL, sin cargar listas completas de paneles o lecturas para filtrarlas en memoria. No excluir históricos por el estado operativo actual de un panel.

Traducir a SQL las agrupaciones por día/mes/año o granularidad aprobada, la conversión temporal de potencia a energía definida en Q1, los totales y los máximos/mínimos definidos en Q4. Cuando los extremos correspondan a energía de subperíodos, calcularlos sobre esos agregados, no sobre muestras individuales. Para parque/nodo, sumar aportes de paneles en el mismo intervalo antes de determinar extremos de generación conjunta.

Materializar únicamente resultados agregados y metadatos necesarios. No utilizar `ToList`/`AsEnumerable` sobre las lecturas antes de los cálculos ni recurrir a evaluación en cliente si una expresión no se traduce. Las lecturas auxiliares que requiera Q1 se procesan también en el motor y con alcance temporal acotado. La cantidad de consultas de comparación debe depender de los rangos solicitados, no de cada panel o lectura.

Usar primero las capacidades de EF Core/Pomelo existentes. Si la fórmula aprobada requiere SQL no expresable mediante LINQ traducible, encapsular SQL parametrizado compatible con MariaDB 11.8 en este repositorio, sin nuevos proveedores. Verificar la traducción real antes de dar el cálculo por terminado. No añadir tablas de resúmenes, caché, tareas programadas ni nuevos índices sin evidencia de necesidad y especificación previa.

### I3. Fuente y preservación de datos

La consulta no invoca `SaveChanges`, no modifica lecturas ni las recrea a partir del simulador. Los datos conocidos para validación se cargan únicamente en una base aislada de pruebas; su preparación no constituye un recolector productivo. La fuente operativa queda sujeta a Q8.

## 4. Presentation

### P1. Página y navegación

Crear `HistoricoGeneracion.razor` en `/historico-generacion` con Blazor Server y Radzen, e incorporar su acceso al menú según Q6. Registrar `IHistoricoGeneracionService` en el punto de composición existente del host. La protección de página y menú debe coincidir con la autorización del servicio.

Permitir seleccionar parque, nodo o panel del inventario persistido, período diario/mensual/anual, rangos y períodos a comparar. Obtener opciones mediante `ListarOpcionesAsync`; no reutilizar el servicio administrativo de parques para conceder acceso a otros roles ni utilizar la topología del simulador.

### P2. Resultados y estados

Mostrar total de energía, serie agregada en una tabla Radzen y comparación de períodos identificados, con máximos y mínimos, sus unidades y referencias temporales según Q4. Indicar los límites y zona horaria de la consulta según Q3 y la disponibilidad según Q5. No presentar W como kWh ni ausencia de datos como generación nula por defecto.

La vista solo adapta entradas, captura eventos e invoca el servicio; no suma lecturas, integra potencia, calcula extremos ni decide permisos de negocio. Presentar carga, entrada inválida, ausencia de datos, acceso denegado, objetivo inexistente y error controlado; impedir envíos repetidos durante una consulta. No se requieren gráficos, exportaciones ni actualización automática para cumplir este alcance.

## 5. Verificación

### V1. Dominio y arquitectura

Ejecutar `dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` después de incorporar código. Corregir el código ante fallos sin modificar los guardarraíles arquitectónicos. No crear pruebas unitarias de Application, Infrastructure ni Presentation.

### V2. Integración funcional con datos conocidos

Incorporar pruebas xUnit en `SolarUY.Integration.Tests` con el servicio, repositorio y MariaDB 11.8 reales, usando el proveedor existente. La conexión se configura externamente y apunta a una base exclusiva de pruebas; no se usa la base habitual ni se versionan secretos. La suite existente del simulador no sustituye esta verificación de SQL y persistencia.

Preparar un conjunto conocido de al menos dos parques, con nodos y paneles distintos y lecturas UTC en varios días, meses y años. Tras Q1–Q6, fijar resultados esperados calculados independientemente de la consulta para comprobar:

- Consulta diaria, mensual y anual por panel, nodo y parque; aislamiento entre objetivos y suma jerárquica correcta.
- Comparación de rangos con sus resultados individuales, límites de fechas, cambio de mes/año y zona horaria acordada.
- Conversión de potencia a energía con muestras conocidas; inclusión de las muestras de borde y casos de huecos/duplicados según Q1.
- Máximos/mínimos y empates, cobertura parcial, ausencia de lecturas y generación medida igual a cero según Q4/Q5.
- Rechazo de rangos invertidos, identificadores inválidos/inexistentes y accesos no permitidos, incluidas opciones y comparación según Q6.
- Conservación de las lecturas tras consultas y compatibilidad de migración/FK con la baja de un parque temporal de req006, preservando las lecturas del otro parque.

### V3. Volumen y SQL

Con el escenario y umbrales acordados en Q7, poblar una base aislada con un volumen grande de lecturas y medir consultas diarias/mensuales/anuales, comparaciones y objetivos de distinto tamaño. Registrar cantidad de filas, rangos, concurrencia, entorno, latencia y memoria, junto con resultado frente a los umbrales. No declarar rendimiento adecuado sin esa evidencia.

Inspeccionar SQL y planes de ejecución para comprobar filtrado por objetivo/tiempo, agregaciones en servidor y uso de índices según el plan. Verificar que no se materializan lecturas completas ni se generan consultas por cada panel. No sustituir MariaDB por un proveedor en memoria para estas comprobaciones.

### V4. Interfaz y regresión

Recorrer la página con los roles acordados: selección de los tres tipos de objetivo, vistas temporales, rangos comparados, extremos y mensajes de carga/error/sin datos. Comprobar denegación tanto en navegación como al invocar el servicio y que todas las etiquetas reflejan unidades y zona acordadas.

Ejecutar `dotnet test SolarUY.sln`, con la configuración de integración necesaria, para comprobar las pruebas existentes y nuevas. Documentar resultados, fallos y comprobaciones no ejecutadas; no considerar completada una validación que dependa de una decisión pendiente o de un entorno no disponible.

## 6. Trazabilidad y cierre

### R1. Ampliación: recolección periódica de paneles registrados

Por solicitud explícita del usuario del 22/10/2026 se amplía Q8 para incorporar la recolección automática. Se conserva la integración simulada existente y se agrega una operación específica para medir el inventario persistido, identificado por `Guid`; no se asignan arbitrariamente los equipos ficticios `panel-1` a paneles de la base. El panel de simulación previo conserva su topología y controles propios.

- **Domain:** sin cambios. Reutilizar `LecturaPanel`, `Panel`, los estados y las validaciones ya definidos.
- **Application:** incorporar `IRecoleccionLecturasService`/`RecoleccionLecturasService`, el puerto `IRecoleccionLecturasRepository` con una sesión exclusiva descartable, DTO de medición del inventario y resultado del ciclo. El servicio abre una sesión, obtiene paneles instalados pendientes del minuto UTC actual, obtiene mediciones mediante `IInversorClient`, valida identidad/conjunto de resultados y construye `LecturaPanel`. Se validan todos los resultados antes de guardar el lote. Es un caso de uso interno del sistema, sin endpoint de escritura para usuarios o chatbot.
- **Infrastructure:** implementar sesión sobre una única conexión física no agrupada de MariaDB, protegida con `GET_LOCK` sin espera y liberada con `RELEASE_LOCK`/cierre de conexión, incluso al fallar o cancelar. Usar un nombre de bloqueo que incluya la base de datos. Solo su propietario lee el simulador y guarda el lote. Consultar paneles desde persistencia en cada ciclo; no guardar una segunda muestra cuando ya existe una lectura de ese panel dentro del mismo minuto UTC. La exclusión dura hasta terminar el guardado, que es atómico. No se eliminan lecturas ni se impone unicidad a datos históricos importados; no se requiere cambio de esquema.
- **Simulador del inventario:** medir usando el `Guid`, número de serie, estado y potencia nominal de cada panel persistido. Generar potencia aleatoria independiente de la hora real, limitada a la potencia nominal, y redondear la medición a seis decimales. Paneles inactivos generan cero; los que tienen falla generan cero y salud `Fallado`. Admitir la configuración existente de fallas por número de serie o `Guid`. Mapear salud a los estados de `LecturaPanel`. El instante UTC es el del ciclo real; no se genera historia previa ni se simulan muestras omitidas durante una caída.
- **Presentation/host:** alojar `RecoleccionLecturasWorker` como `BackgroundService`, con un ámbito DI por ciclo y dependencia exclusiva del caso de uso de Application. Esperar `ApplicationStarted` antes de la primera recolección; si falla el inicio de la web, cancelar la espera al desechar el host sin abrir ámbitos ni usar servicios descartados. Después, recoger con `PeriodicTimer`, sin solapar ciclos locales. Configuración `RecoleccionLecturas:Habilitada` (predeterminado `true`) e `Intervalo` (predeterminado `00:01:00`, configurable entre un segundo y cinco minutos). Validar al iniciar. Registrar resultados y errores; un error no detiene la web y se reintenta en el próximo ciclo. Al apagar, cancelar y liberar la sesión. No cambiar las páginas para efectuar cálculos o escrituras.
- **Verificación:** integración permanente con MariaDB aislada y reloj controlado para colección del inventario, potencia independiente de la hora real, estado/salud, incorporación de nuevos paneles, idempotencia/reinicio, exclusión entre dos recolectores, liberación ante error/cancelación y consulta de energía sobre lecturas recolectadas. Verificar el worker real y dos hosts concurrentes, repetir guardarraíles y la solución. Comprobar al menos un ciclo real en la base habitual sin modificar sus otras entidades; la recolección desde ahora está autorizada por esta ampliación. Registrar explícitamente cualquier prueba existente que no cumpla sus criterios, sin modificar esos criterios.

Referencias: [GET_LOCK de MariaDB](https://mariadb.com/docs/server/reference/sql-functions/secondary-functions/miscellaneous-functions/get_lock) y [servicios hospedados de ASP.NET Core 8](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-8.0).

| Criterio de `requirements.md` | Especificación |
| --- | --- |
| 1. Consultas por día, mes, año y objetivo | A1, A2, I2, P1, V2 |
| 2. Rangos y comparación | Q3, A1, A2, I2, P2, V2 |
| 3. Máximos y mínimos | Q4, A1, I2, P2, V2 |
| 4. Agregación eficiente en base de datos | Q1, Q7, I1, I2, V3 |
| 5. Servicios reutilizables | A1–A3 |
| 6. Visualización y selección | P1, P2, V4 |
| 7. Datos conocidos y volumen | V1–V4 |

### C1. Documentación de la implementación

La última tarea crea `implementations.md` en esta carpeta con los cambios realizados, archivos tocados, decisiones aclaradas y resultados de pruebas, incluidos límites operativos. El estado de ejecución se registra en `tasks.md` únicamente a partir de la evidencia obtenida.
