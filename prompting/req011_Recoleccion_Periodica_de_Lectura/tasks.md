# Tareas: Recolección periódica de lecturas

Fuente de alcance técnico: `specs.md` de esta carpeta. Tareas completadas según la implementación y las verificaciones registradas en `implementations.md`. Las referencias entre paréntesis corresponden a secciones de la especificación.

## 1. Clarificaciones previas

- [x] 1. Resolver y registrar las decisiones necesarias antes de implementar los cambios dependientes. (Sección 2)
  - [x] 1.1. Confirmar si se conservan `LecturaPanel.FechaHora` o se requiere renombrar entidad y fecha; registrar el impacto sobre consumidores sin duplicar tablas. (D1)
  - [x] 1.2. Acordar cómo conservar el nodo histórico y, si corresponde, autorizar la relación `Nodo`/`NodoId`; definir cómo tratar filas históricas cuya asociación no pueda reconstruirse con certeza. (D2, INF1)
  - [x] 1.3. Definir origen o asignación del instante estable, precisión temporal y normalización para consultas, reintentos y persistencia entre instancias. (D3)
  - [x] 1.4. Acordar el tratamiento de duplicados preexistentes y el diagnóstico que debe detener una migración sin borrar datos. (D4)
  - [x] 1.5. Actualizar `specs.md` con las decisiones resueltas antes de ejecutar las tareas afectadas. (Sección 2)

## 2. Domain

- [x] 2. Adaptar el modelo de medición y sus pruebas de dominio. (DOM1, DOM2, V1)
  - [x] 2.1. Ajustar la entidad existente según D1/D2, manteniendo Guid generado en el constructor, navegación, propiedades encapsuladas y coherencia panel/nodo; incorporar simultáneamente las pruebas xUnit correspondientes. (DOM1, V1)
  - [x] 2.2. Conservar las validaciones de fecha UTC obligatoria, potencia en W representable en `decimal(18,6)` y salud válida, aplicando D3 y cubriendo los casos inválidos con pruebas de dominio. (DOM1, V1)
  - [x] 2.3. Garantizar que cambios posteriores del estado y topología no reescriban los datos históricos según D2 y comprobar la conservación del estado medido con pruebas de dominio. (DOM2, V1)

## 3. Application

- [x] 3. Adaptar contratos y orquestación sin dependencias de infraestructura. (APP1, APP2)
  - [x] 3.1. Ajustar los contratos de sesión, `IInversorClient` y DTO únicamente para transportar el instante estable y la asociación aprobados. (APP1)
  - [x] 3.2. Adaptar `RecoleccionLecturasService` para consultar solo con exclusión adquirida, validar paneles de la topología persistida y construir el lote sin crear equipos ni sustituir respuestas inválidas. (APP1)
  - [x] 3.3. Usar la identidad panel/instante aprobada para reintentos, eliminando la dependencia de una lectura por minuto y conservando mediciones distintas dentro del minuto. (APP2)
  - [x] 3.4. Propagar cancelación, liberar siempre la sesión y conservar validación completa y guardado atómico del lote, sin sobrescrituras del histórico ni reintentos que cambien la identidad. (APP2)

## 4. Infrastructure

- [x] 4. Completar persistencia e integración reutilizando los componentes existentes. (INF1–INF3)
  - [x] 4.1. Ajustar el mapeo y contexto según D1/D2, con panel obligatorio, nodo exigido en mediciones nuevas y nullable en legado sin evidencia, borrado restrictivo y precisión/materialización UTC de D3. (INF1; reparación del 29/10/2026)
  - [x] 4.2. Configurar unicidad por panel/instante e índices para panel y nodo con rango temporal conforme a la alternativa aprobada en D2. (INF1)
  - [x] 4.3. Crear una migración incremental compatible con instalaciones nuevas y bases con datos, incluyendo la comprobación de duplicados y los bloqueos de migración definidos en D2/D4. (INF1)
  - [x] 4.4. Adaptar la selección de pendientes para eliminar la omisión por minuto y distinguir conflictos de unicidad de otros errores sin sobrescribir lecturas. (APP2, INF1)
  - [x] 4.5. Asegurar que `RecoleccionLecturasRepository` mantiene una conexión dedicada sin pooling y el bloqueo compartido desde antes de la consulta hasta terminar el guardado; omitir el ciclo cuando otra instancia posee el bloqueo. (INF2)
  - [x] 4.6. Comprobar propiedad antes del guardado, abortar la sesión ante pérdida de coordinación sin reconectar silenciosamente y liberar bloqueo/conexión ante cierre, error o cancelación. (INF2)
  - [x] 4.7. Verificar que `InversorSimulador` respeta el instante aprobado mediante el contrato existente, preservando W y la topología persistida como origen de los paneles a medir; no requiere cambios internos. (INF3)

## 5. Presentation: hospedaje y configuración

- [x] 5. Ajustar el proceso alojado sin modificar la interfaz Blazor. (PRE1, PRE2)
  - [x] 5.1. Incorporar `IntervaloSegundos` obligatorio y positivo en opciones y `appsettings.json`, permitiendo sobrescritura con `RecoleccionLecturas__IntervaloSegundos` y rechazando valores inválidos al arrancar con `ValidateOnStart`. (PRE1)
  - [x] 5.2. Sustituir la configuración `Intervalo`, retirar el máximo de cinco minutos y convertir el valor validado a `TimeSpan`, conservando `Habilitada`. (PRE1)
  - [x] 5.3. Mantener los registros de DI y `AddHostedService`, el inicio automático y un alcance asíncrono por ciclo, con ejecución secuencial sin capturar dependencias scoped en el singleton. (PRE1)
  - [x] 5.4. Asegurar cancelación de espera y operaciones, cierre normal sin error y registro de fallos que permita continuar ciclos posteriores. (PRE1)

## 6. Verificación

- [x] 6. Ejecutar las verificaciones previstas y registrar evidencias o impedimentos. (Sección 7)
  - [x] 6.1. Ejecutar `dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj`; corregir el código sin relajar las pruebas arquitectónicas. (V1, V2)
  - [x] 6.2. Aplicar la migración en MariaDB 11.8 sobre bases nueva y con datos previos; verificar filas conservadas, relaciones, precisión, índices y diagnóstico ante duplicados existentes. (V3)
  - [x] 6.3. Ejecutar consultas por panel y nodo en rangos temporales y comprobar el uso de los índices definidos. (V3)
  - [x] 6.4. Reutilizar las verificaciones de integración para comprobar inserciones concurrentes, reintentos de la misma medición y mediciones distintas dentro de un minuto con la precisión acordada. (V4)
  - [x] 6.5. Ejecutar dos procesos de la aplicación sobre la misma base y demostrar exclusión previa a consultar y ausencia de duplicados. (V5)
  - [x] 6.6. Detener el proceso propietario y forzar una desconexión/pérdida de bloqueo en una ejecución controlada; comprobar el relevo, el rechazo de escrituras de la sesión inválida y documentar los tiempos observados. (V5, INF2)
  - [x] 6.7. Verificar inicio automático, cadencia N, sobrescritura por entorno, rechazo de configuración inválida y cierre con cancelación y liberación de recursos. (V6)
  - [x] 6.8. Provocar errores transitorios de consulta y persistencia y comprobar su registro y recuperación posterior sin datos ficticios ni escrituras parciales del lote fallido. (V6, APP2)

## 7. Cierre/Documentación

- [x] 7. Crear el archivo implementations.md en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.
  - [x] 7.1. Incluir las decisiones D1–D4 resueltas, la nueva configuración del intervalo y la forma de ejecutar el servicio. (Sección 8)
  - [x] 7.2. Documentar el mecanismo de coordinación, sus límites de recuperación y los pasos reproducibles de concurrencia, fallos y relevo; identificar pruebas no ejecutadas y sus motivos. (Sección 8)
