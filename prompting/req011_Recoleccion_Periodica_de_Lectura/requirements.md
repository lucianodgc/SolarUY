# Requerimientos: Recolección periódica de lecturas (BackgroundService) y almacenamiento

## Descripción General

Como usuario del sistema SolarUY, quiero que las lecturas de generación y estado de salud de los paneles solares se registren automáticamente y de forma periódica, para disponer de un histórico confiable que permita implementar futuras funcionalidades de consulta, monitoreo y análisis.

Esta funcionalidad corresponde a la **Entrega 2** y debe integrarse con la topología existente de parques, nodos, paneles e inversores, respetando el modelo de dominio y la arquitectura del proyecto.

## Criterios de Aceptación

### 1. Entidad Lectura y persistencia

- Crear la entidad `Lectura` con, como mínimo, los siguientes datos:
  - Identificador único de la lectura.
  - Panel asociado (obligatorio).
  - Nodo asociado (obligatorio, coherente con el panel).
  - Fecha y hora de la medición en UTC (obligatoria).
  - Potencia generada en el momento de la lectura (obligatoria, con una unidad definida y consistente).
  - Estado de salud del panel registrado al momento de la lectura (obligatorio).
- Cada lectura debe representar una medición puntual, no un valor agregado diario o mensual.
- Las lecturas deben almacenarse de forma persistente en MariaDB mediante la infraestructura de acceso a datos existente (EF Core).
- Debe incorporarse una migración incremental que cree la tabla de lecturas y sus relaciones, sin eliminar datos existentes.

### 2. Recolección automática mediante BackgroundService

- Implementar un `BackgroundService` que se ejecute automáticamente mientras la aplicación esté en funcionamiento.
- El servicio debe consultar periódicamente los inversores para obtener las mediciones de los paneles correspondientes y guardar las lecturas obtenidas.
- El intervalo de recolección debe ser configurable en segundos (`N`) mediante configuración externa, sin modificar el código fuente.
- El intervalo configurado debe validarse para impedir valores nulos, cero o negativos.
- Las operaciones de consulta y persistencia deben respetar la cancelación del servicio para permitir un apagado ordenado.
- Los fallos de un ciclo de recolección deben registrarse y no impedir que el servicio continúe funcionando en ciclos posteriores.

### 3. Prevención de duplicados con múltiples instancias

- Al ejecutar dos o más instancias de la aplicación sobre la misma base de datos, **solo una instancia debe realizar la recolección en un momento dado**.
- Implementar un mecanismo de coordinación compartido entre instancias (por ejemplo, bloqueo o arrendamiento/lease en MariaDB). Un bloqueo únicamente en memoria no es suficiente.
- Si la instancia que recolecta se detiene o pierde la coordinación, otra instancia debe poder retomar la tarea sin intervención manual.
- La coordinación debe evitar que dos instancias registren la misma medición simultáneamente.
- Incorporar una protección de unicidad en persistencia para impedir lecturas duplicadas de un mismo panel en el mismo instante de medición, incluso ante concurrencia o reintentos.

### 4. Consultas históricas eficientes

- Crear índices adecuados para las búsquedas de lecturas por panel y fecha/hora.
- Contemplar también las consultas por nodo y rango de fechas, de acuerdo con las relaciones del modelo existente.
- La información almacenada debe conservar la granularidad temporal necesaria para futuros requerimientos de histórico y monitoreo.
- No es necesario implementar nuevas pantallas, gráficos ni reportes históricos dentro de este alcance.

## Reglas de Negocio

- Toda lectura debe estar asociada a un panel y a su nodo correspondiente, existentes en el sistema.
- La fecha y hora deben representar el instante de la medición y almacenarse en **UTC**; no debe sustituirse por la hora local del servidor.
- La potencia generada debe registrarse con una unidad definida de forma consistente en el sistema, sin conversiones implícitas entre unidades distintas.
- El estado de salud debe conservar el valor correspondiente al instante de la medición, sin sobrescribirse cuando cambie el estado actual del panel.
- Las lecturas históricas no deben modificarse automáticamente a partir de cambios posteriores en los paneles, nodos o inversores.
- No se permite registrar dos lecturas del mismo panel con idéntica fecha y hora de medición.
- Si falla la consulta de un inversor o el guardado de una lectura, el error debe registrarse sin producir datos ficticios ni detener permanentemente el proceso de recolección.
- La recolección automática no debe crear paneles, nodos o inversores inexistentes.

## Restricciones y Decisiones Técnicas

- Usar `.NET BackgroundService` / `IHostedService` para el proceso periódico.
- Mantener la lógica de negocio y las abstracciones necesarias desacopladas de la implementación de infraestructura y del servicio alojado, siguiendo la arquitectura existente de SolarUY.
- Utilizar EF Core con el proveedor MariaDB ya adoptado por el proyecto.
- Obtener el intervalo desde `appsettings.json`, permitiendo su modificación por variables de entorno. No guardar credenciales ni secretos en el repositorio.
- Gestionar correctamente el alcance de las dependencias de EF Core (`DbContext`) dentro del servicio de larga duración; no inyectar directamente un `DbContext` de alcance *scoped* en un servicio *singleton*.
- Para la exclusión entre instancias, emplear un mecanismo de coordinación basado en un recurso compartido y con recuperación ante fallos; documentar el mecanismo elegido y su comportamiento ante caída de una instancia.
- Utilizar una restricción/índice único sobre la clave que identifique una medición (`PanelId`, `FechaHoraUtc`), siempre que el origen de datos proporcione un instante de medición estable. Si el inversor no lo proporciona, documentar y definir explícitamente cómo se asigna ese instante para garantizar la idempotencia.
- No agregar funcionalidades de alertas, paneles de monitoreo ni visualizaciones históricas que no formen parte de este requerimiento.

## Pruebas y Entrega

- Incluir pruebas de dominio para validar los datos obligatorios de `Lectura`, su relación con panel/nodo, fecha UTC y reglas de unicidad aplicables.
- Verificar que el servicio inicia, consulta los inversores según el intervalo configurado y persiste lecturas automáticamente mientras la aplicación corre.
- Verificar que errores transitorios de consulta o persistencia no interrumpen definitivamente las siguientes ejecuciones.
- Ejecutar una prueba con **dos instancias** conectadas a la misma base de datos y confirmar que no se duplican las lecturas.
- Verificar la recuperación de la recolección cuando se detiene la instancia que estaba activa.
- Comprobar que la migración y los índices se aplican correctamente a MariaDB y permiten consultas por panel y rango temporal.
- Mantener todos los **tests de dominio en verde**.
- Documentar la configuración del intervalo, cómo ejecutar el servicio y cómo reproducir las verificaciones de concurrencia; registrar las pruebas que no se hayan podido ejecutar.
