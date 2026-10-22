# Revisión de alcance e implementación del frontend

> Registro de la revisión anterior. La auditoría integral posterior, sus correcciones y sus resultados vigentes están en [auditoria_integral.md](auditoria_integral.md). En ella se repitió el aprovisionamiento desde cero y se corrigió `solaruy.sql`; los pendientes mencionados abajo describen el estado de aquella revisión.

## Resultado de la auditoría

Se comparó el código modificado con los requisitos anteriores a esta integración (`git show HEAD:...`), el pedido general de implementar el frontend y la respuesta explícita del usuario sobre permisos del simulador. Esta revisión corrige la entrega anterior: no todos los requerimientos necesitan una pantalla y aprobar pruebas no convierte decisiones pendientes en aprobadas.

| Requerimiento | Respaldo y resultado |
| --- | --- |
| req001: alta de parque | Formulario existente, restringido a Administrador. Se conservan sus campos y validaciones; se agregan accesos desde inicio/listado y regreso/cancelación. |
| req002: autenticación | El requisito pide identidad, roles, claims y logout visibles. `/mi-cuenta` organiza esos datos; el login sigue en Keycloak y no se agrega gestión de usuarios. |
| req003: infraestructura | El original no pide una pantalla: MariaDB, contexto y migraciones respaldan parques y mantenimiento. Se retira `/inventario` junto con sus DTO, servicio, repositorio y registros de DI, porque agregaba una funcionalidad no definida. |
| req004: mantenimiento | Se conservan páginas, filtros, acciones y permisos ya especificados. Se añaden accesos, presentación compartida y notificación de creación; no se cambian transiciones ni asignaciones. |
| req005: simulador | La pantalla corresponde al pedido posterior de frontend. Administrador opera y Técnico/Consulta solo consultan, según confirmación explícita del usuario. El servicio de Application verifica permisos y evita cálculos de negocio en la vista. Enumerar nodos por el puerto permite mostrar la topología existente sin inventar identificadores. |
| req006: multiparque | Se conectan listado, alta, búsqueda, edición y baja existentes. `RadzenDialog` permite mostrar la confirmación ya requerida. Se conserva el formulario al mostrar errores de validación y se bloquean envíos repetidos. |

## Correcciones realizadas

- Eliminación del módulo de inventario y de los textos que lo presentaban como una ampliación solicitada de req003.
- Inicio simplificado: accesos funcionales sin eslóganes, bloque promocional ni indicadores de conexión sin una comprobación real.
- Corrección de tareas que se habían dado por cerradas con evidencia insuficiente: distribuir la configuración al equipo, reproducir el aprovisionamiento en una base vacía y entregar una verificación permanente del simulador.
- CSS global versionado por su contenido en `App.razor` y nueva versión del CSS aislado. El servidor entregaba el CSS actual, pero la captura del usuario no mostraba sus reglas; cambiar la URL evita reutilizar una respuesta anterior. Se comprobó el resultado mediante estilos calculados y recarga con caché habilitada.

No se modifican entidades de dominio, migraciones, paquetes, versiones, credenciales ni pruebas arquitectónicas. No se agregan campos de negocio, datos de demostración permanentes ni nuevas integraciones.

## Verificación de esta revisión

- `dotnet test SolarUY.sln --no-restore -m:1 -p:BuildInParallel=false -p:BaseOutputPath=bin/AuditVerification/`: 59 pruebas existentes aprobadas (42 Domain, 8 Application y 9 CodeQuality). No se crearon pruebas unitarias de Application/Infrastructure/Presentation ni se editaron las existentes.
- Ejecutable temporal de integración: 21 comprobaciones aprobadas de topología, curva solar, fallas, cambios de panel, volcado, permisos, rechazo de nodos desconocidos, historial aplicado y conservación de los datos originales. No es una suite versionada ni reemplaza el pendiente de entrega de pruebas de req005.
- Edge con Administrador: estilos calculados de cuadrícula/tarjetas, recarga con caché, navegación, tareas y opciones de mantenimiento, controles de simulación, perfil y ABM de un único parque temporal por recorrido. Se verificaron búsqueda, cancelación de baja, error de nombre duplicado y corrección posterior. El parque temporal se eliminó y los controles simulados se restauraron.
- Inicio móvil de 390 px sin desbordamiento y menú desplegable. Capturas inspeccionadas en `artifacts/frontend/`, ignorado por Git.
- Se verificó con Técnico y Consulta la lectura del simulador, los enlaces visibles y la denegación de acceso directo a gestión de parques. No se muestran controles administrativos del simulador para esos roles.

## Pendientes que no se presentan como completados

- req003: acuerdo sobre distribución de configuración al equipo y reproducción de `dotnet ef database update` en una base vacía. Se verificó el estado aplicado de la base existente; las comprobaciones previas de req004 son evidencia histórica y no una ejecución nueva de ese procedimiento.
- req005: decidir con el equipo cómo entregar las pruebas solicitadas respetando las reglas del proyecto. El ejecutable temporal es una comprobación local, no una suite incluida en el repositorio.
- req002: URLs y configuración de producción, que ya figuraban pendientes y no se definen en este trabajo.
- Hallazgo previo ajeno al frontend: `solaruy.sql` aún contiene nombres de tablas en minúsculas, distintos de los nombres de las migraciones. Importarlo en un servidor que distingue mayúsculas reproduciría el problema de conexión ya diagnosticado. La base utilizada en las comprobaciones tiene los nombres corregidos; el volcado no se utiliza para levantar la aplicación automáticamente.
- Esta revisión no cambia estados de las tareas reales ni verifica borrado en cascada sobre un parque real con dependencias. Esos flujos conservan la implementación y la evidencia previa de sus requerimientos; no se presentan como repetidos aquí.

Las comprobaciones usan una compilación separada y una instancia temporal en HTTPS 7271. La aplicación del usuario en HTTP 5219 y sus contenedores se conservan.
