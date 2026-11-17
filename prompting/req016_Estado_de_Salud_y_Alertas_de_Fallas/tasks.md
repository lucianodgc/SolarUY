# Tareas: Estado de salud y alertas de fallas

Fuente única de alcance técnico: `specs.md` de esta carpeta. D1–D5 confirmadas; D6 implementada y verificada. Coordinación cerrada el 17/11/2026 mediante la validación y delegación de Luciano comunicadas por el usuario: ambas instancias candidatas a recolectar, exclusión MariaDB existente y relevo sin promoción manual. Ver `coordinacion_despliegue.md`; no se atribuye una fecha original de aprobación ni un despliegue real ejecutado. Las referencias DOM/APP/INF/PRE/D/V corresponden a esa especificación.

## 1. Clarificaciones y compatibilidad

- [x] 1. Cerrar y documentar las decisiones de reglas e integración. (Secciones 1–2)
  - [x] 1.1. Acordar fuente y fórmula de potencia esperada, unidades y condiciones en las que la baja potencia es normal; comprobar la disponibilidad real de los datos necesarios. (D1, INF3)
  - [x] 1.2. Acordar umbrales, límites inclusivos, precedencia y persistencia de condiciones; definir plazo sin datos, primera muestra y evidencia incompleta/desconocida. (D2)
  - [x] 1.3. Acordar la agregación por nodo, cobertura insuficiente, exclusiones y nodo sin paneles. (D3)
  - [x] 1.4. Definir correspondencia con `EstadoSaludLectura`, `EstadoPanel` y la señal del inversor, preservando los valores y contratos existentes. (D4)
  - [x] 1.5. Definir primera evaluación, estado anterior inicial, fechas de evidencia/cambio/detección y tratamiento de muestras de nodo anterior o panel trasladado. (D4)
  - [x] 1.6. Acordar apertura, prioridad/coexistencia, cierre, recurrencia e intermitencia de alertas y efecto de cambios de política. (D5)
  - [x] 1.7. Validar con el responsable el mecanismo vigente de distribución y recuperación entre instancias, considerando `RemoveDistribucionMonitoreo`; documentar coordinación de evaluadores y despliegue sin elegir un transporte nuevo unilateralmente. (D6)
    - [x] 1.7.1. Registrar la autorización posterior del usuario, implementar la propuesta MariaDB y documentar la verificación con dos procesos y navegador.
    - [x] 1.7.2. Registrar la validación y la delegación del responsable comunicadas por el usuario; concretar la decisión delegada y documentar relevo y despliegue.
      - Cierre registrado el 17/11/2026 en [coordinacion_despliegue.md](coordinacion_despliegue.md). A/B quedan candidatas con exclusión compartida; solo una adquiere por ciclo. B no depende de que A esté sana. La verificación del entorno real y sus certificados sigue siendo una actividad operativa separada.
  - [x] 1.8. Actualizar `specs.md` con los acuerdos y ajustar las tareas afectadas antes de cerrar sus contratos, reglas o migración. (Secciones 2 y 8)

## 2. Domain

- [x] 2. Implementar clasificación, estado, transiciones y alertas con sus pruebas simultáneas. (DOM1–DOM3)
  - [x] 2.1. Definir los cuatro valores de `EstadoSaludPanel` de Domain y distinguir el enum homónimo del contrato de inversor sin alterar ordinales ni significados existentes. (DOM1, D4)
  - [x] 2.2. Crear `PoliticaSalud` y validar los parámetros aprobados, sus unidades y coherencia; cubrir sus límites con xUnit. (DOM1, DOM3, D1–D2)
  - [x] 2.3. Implementar `EvaluacionSaludService` con referencia UTC y evidencia explícitas; cubrir normalidad, degradación, falla, desconexión, potencia baja justificada y evidencia insuficiente. (DOM1, DOM3)
  - [x] 2.4. Implementar `SaludNodoService` con estado, cobertura y suficiencia; probar combinaciones, exclusiones y nodo vacío según D3. (DOM1, DOM3)
  - [x] 2.5. Modelar `SaludPanel` y encapsular aplicación de evaluaciones, versión, evidencia y rechazo de resultados atrasados; probar repetición y recuperación posterior a desconexión. (DOM2–DOM3, D4)
  - [x] 2.6. Modelar `CambioEstadoSaludPanel` con anterior/nuevo, fecha UTC y orden por versión; probar primera evaluación y ausencia de transiciones cuando no cambia el estado. (DOM2–DOM3)
  - [x] 2.7. Modelar `AlertaPanel` y encapsular apertura, cierre y recurrencia aprobados; probar condición sostenida sin duplicación y conservación del historial. (DOM2–DOM3, D5)

## 3. Application

- [x] 3. Incorporar orquestación y consultas independientes de EF Core, Blazor y SignalR. (APP1–APP3)
  - [x] 3.1. Definir `ISaludPanelRepository`, `ISaludPanelService` y resultados de evaluación con evidencia, versión y cancelación. (APP1)
  - [x] 3.2. Implementar `SaludPanelService` delegando reglas a Domain, suministrando UTC mediante `TimeProvider` y coordinando confirmación/reintento con evidencia vigente. (APP1, INF2)
  - [x] 3.3. Integrar evaluación después del guardado confirmado de lecturas, separando fallos de adquisición, evaluación y publicación; permitir recuperación sin repetir la adquisición. (APP1)
  - [x] 3.4. Incorporar evaluación inicial, reevaluación sin nuevos eventos y reconciliación de trabajo pendiente; utilizar solo evidencia persistida y la coordinación D6. (APP1)
  - [x] 3.5. Definir `IConsultaSaludService`, `ConsultaSaludService` y DTO de panel, nodo, alerta activa y cambio histórico con suficiencia, motivos y fechas UTC. (APP2)
  - [x] 3.6. Implementar consultas de salud/alertas por parque e historial paginado por panel con orden estable y límites acotados; validar usuario, roles, identificadores, existencia y pertenencia. (APP2)
  - [x] 3.7. Completar la conexión a D6 de `INotificadorSaludConfirmada`; el contrato, el aviso y la publicación posterior al commit con recuperación local están implementados. (APP3)

## 4. Infrastructure

- [x] 4. Implementar almacenamiento y acceso consistente con MariaDB. (INF1–INF3)
  - [x] 4.1. Crear mapeos EF de salud, cambios y alertas, con relaciones sin cascada destructiva, precisión UTC y representación explícita de enums. (INF1)
  - [x] 4.2. Configurar una fila de salud por panel, versión única de transición por panel e índices para consultas de alertas e historial. (INF1)
  - [x] 4.3. Imponer unicidad de condición activa en MariaDB conforme a D5, permitiendo conservar episodios cerrados de la misma condición. (INF1)
  - [x] 4.4. Generar `AddEstadoSaludAlertas` como migración incremental sin modificar lecturas, estados administrativos ni inventar transiciones retrospectivas. (INF1)
  - [x] 4.5. Implementar confirmación transaccional de salud, transición y alertas en `SaludPanelRepository`, incluida primera creación concurrente, validación de versión y reintento acotado. (INF2)
  - [x] 4.6. Obtener evidencia, salud y alertas con consultas acotadas y coherentes; preservar paneles sin datos y ejecutar paginación del historial en el servidor. (INF3)
  - [x] 4.7. Integrar recuperación del procesamiento y de la publicación/recepción mediante el mecanismo aprobado en D6, sin mantener transacciones durante envíos ni consultas al inversor. (INF2, APP3)
  - [x] 4.8. Preparar los escenarios de falla/recuperación con las capacidades existentes de `InversorSimulador`; documentar cualquier carencia de evidencia antes de cambiar contratos. (INF3)

## 5. Presentation y configuración

- [x] 5. Extender el monitoreo con salud, alertas e historial de solo lectura. (PRE1–PRE3)
  - [x] 5.1. Mostrar estado de salud por panel y nodo con indicador visual, texto, motivo y UTC, diferenciando salud, disponibilidad, potencia y estado administrativo. (PRE1)
  - [x] 5.2. Mostrar evidencia insuficiente/vencida y último estado conocido sin presentarlo como actual. (PRE1)
  - [x] 5.3. Incorporar la lista Radzen de alertas activas del parque, con panel, nodo/parque actuales, condición y detección UTC. (PRE1)
  - [x] 5.4. Incorporar acceso por panel al historial paginado, con estados anterior/nuevo y fecha UTC; resolver carga, vacío y error. (PRE1–PRE2)
  - [x] 5.5. Conectar a D6 el adaptador local ya implementado de `INotificadorSaludConfirmada`; el hub, la autorización y la suscripción por parque están integrados. (PRE2)
  - [x] 5.6. Actualizar salud, alertas e historial abierto mediante avisos confirmados; reconciliar al cargar, suscribir y reconectar sin modificar indebidamente el gráfico de potencia. (PRE2)
  - [x] 5.7. Descartar respuestas de selecciones anteriores, gestionar avisos concurrentes/repetidos y liberar handlers, cancelaciones y recursos al salir. (PRE2)
  - [x] 5.8. Mantener acceso de los tres roles lectores en servicios, componentes y hub, sin exponer comandos manuales de estados o alertas. (APP2, PRE2)
  - [x] 5.9. Registrar dependencias y ejecución periódica; externalizar y validar parámetros aprobados e intervalo de evaluación sin añadir otra adquisición ni secretos. (PRE3)
  - [x] 5.10. Documentar y aplicar el procedimiento de migración y compatibilidad durante reemplazo de instancias conforme a D6. (PRE3)

## 6. Verificación

- [x] 6. Ejecutar y registrar las comprobaciones de la especificación. (Sección 7)
  - [x] 6.1. Ejecutar las pruebas de Domain, incluidas las creadas simultáneamente con las nuevas reglas. (V1)
  - [x] 6.2. Ejecutar `CodeQuality.Tests` y corregir el código ante fallos, sin modificar los guardarraíles. (V2)
  - [x] 6.3. Probar la migración en MariaDB con datos previos, preservación de valores/ordinales, relaciones, índices y consultas paginadas sin historial retrospectivo ficticio. (V3)
  - [x] 6.4. Probar persistencia atómica, primera creación concurrente, condición sostenida, repetición, recuperación y recurrencia sin alertas/transiciones duplicadas. (V4)
  - [x] 6.5. Verificar V5: desconexión sin eventos, reinicio, fallos transaccionales/aviso local y distribuido, cancelación de transporte, evidencia atrasada, traslados, parque vacío al reiniciar y recuperación sin repetir adquisición. (V5)
  - [x] 6.6. Verificar en navegador falla simulada y recuperación: panel, nodo, alertas e historial se actualizan sin recargar; registrar latencia e intervalos. (V6)
  - [x] 6.7. Verificar evidencia insuficiente, cambios de parque, reconexión y liberación de recursos en la interfaz. (V6)
  - [x] 6.8. Verificar acceso de los tres roles, rechazo de anónimos/roles ajenos e inexistencia de escrituras manuales expuestas. (V7)
  - [x] 6.9. Verificar clientes en instancias diferentes detrás de Nginx, recepción de cambios/cierres, concurrencia y reemplazo de instancia con recuperación. (V8)
  - [x] 6.10. Registrar comprobaciones omitidas y sus causas, sin marcar escenarios bloqueados como aprobados. (Sección 7)

## 7. Cierre/Documentación

- [x] 7. Crear el archivo implementations.md en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.
  - [x] 7.1. Documentar acuerdos D1–D6, reglas finales, migración y configuración externa. (Sección 8)
  - [x] 7.2. Incluir pasos reproducibles de falla, recuperación, alertas, historial y dos instancias, junto con evidencias V1–V8 y verificaciones omitidas. (Sección 8)
  - [x] 7.3. Revisar la coherencia de requisitos, especificación y tareas con el alcance finalmente aprobado. (Sección 8)
