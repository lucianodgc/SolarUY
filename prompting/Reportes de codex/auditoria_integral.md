# Auditoría integral de SolarUY

> Este documento conserva el corte original. El estado vigente de H-05, H-06, H-07, H-09 y H-10 está en [seguimiento_auditoria.md](seguimiento_auditoria.md) y en `followup` del anexo JSON. H-06/H-07/H-10 se corrigieron después de esta auditoría; H-05/H-09 mantienen acciones operativas pendientes.

Fecha de referencia: 22/10/2026. Base de comparación: HEAD `40e5cb6` y el estado local capturado antes de esta auditoría.

**Resultado: las funciones principales están integradas y se comprobaron con datos y autenticación reales en un entorno aislado. No corresponde declarar los seis requerimientos cerrados al 100 %.** Hay pendientes de entrega y configuración, y el volcado conserva asignaciones que no corresponden necesariamente a usuarios del realm utilizado.

Se corrigieron cuatro defectos de implementación o presentación: nombres de tablas del volcado, recuperación UTC de la fecha del parque, respuesta de logout inválido y formato decimal de capacidad. También se corrigieron rutas de documentación y el estado de una tarea efectivamente verificada. No se cambiaron reglas de negocio, contratos públicos, migraciones ni dependencias.

## Alcance, método y evidencia

Se inventariaron **147 archivos** al inicio: cuatro proyectos de producto, tres proyectos de pruebas, documentos REQ001–REQ006, instrucciones, configuración y activos. El [inventario por archivo](auditoria_integral_inventario.md) conserva hashes iniciales, cambios y método de revisión. Los [resultados estructurados](auditoria_integral_resultados.json) conservan casos y recuentos.

Se leyeron los requirements —incluidos los nombres `requierements.md` y `requeriments.md`—, especificaciones, tareas e informes de implementación. Se contrastaron con Git, incluidas las entregas anteriores a la integración del frontend. Las casillas completadas no se tomaron como prueba de cumplimiento.

Se distinguen cuatro estados:

| Estado | Significado |
| --- | --- |
| VERIFICADO | Se obtuvo evidencia concreta para el criterio y el entorno indicados. |
| PARCIAL | Existe implementación o evidencia relevante, pero falta parte de la entrega o una garantía. |
| FALLA | Se reprodujo un comportamiento incompatible con lo esperado. Puede figurar como corregido después de repetir la prueba. |
| NO COMPROBADO | No se ejecutó la comprobación o depende de un entorno/decisión no disponible. |

Entorno de ejecución: SDK .NET 8.0.425; MariaDB 11.8 temporal en puerto 3310; Keycloak 26.8.1 temporal en 8180; compilación separada `bin/AuditoriaIntegral/`; aplicación HTTPS 7271 y Edge headless con CDP 9230. Se clonó el realm y se usaron credenciales aleatorias exclusivas de auditoría.

El ejecutable de integración rechaza conexiones cuyo puerto no sea 3310 o cuya base no empiece con `solaruy_audit`. Las altas, cambios, bajas, pruebas de claves foráneas y tablas auxiliares se realizaron exclusivamente allí. La aplicación del usuario en HTTP 5219 y sus servicios originales no se reemplazaron. Una consulta final de solo lectura confirmó **2 parques, 3 nodos, 6 paneles y 6 tareas** en la base original. Los recuentos por sí solos no son una prueba criptográfica de igualdad de cada registro; la preservación se apoya además en que ninguna operación de escritura de la auditoría apuntó a esa base.

Los binarios, cachés, historial interno de Git y credenciales privadas no se presentan como archivos fuente auditados. Bootstrap, su mapa y el favicon se trataron como activos suministrados: versión/licencia, validez del mapa, inspección de imagen y carga en navegador. No se afirma una revisión manual de cada regla minificada ni una auditoría de seguridad de sus proveedores.

## A. Estado por requerimiento

### REQ001 — Alta de parque solar

Exige nombre obligatorio y único, ubicación obligatoria, capacidad opcional, alta exclusiva de Administrador y parque inicialmente sin equipos.

| Criterio | Implementación y evidencia | Estado |
| --- | --- | --- |
| Entidad, identidad y fecha de creación | `ParqueSolar`, constructor y pruebas de dominio. P-01/P-02 recuperan el registro desde un contexto nuevo. Se corrigió la marca UTC al leer MariaDB. | VERIFICADO |
| Campos requeridos y máximos previstos en especificación | Validadores de Application e invariantes de Domain. Cinco entradas inválidas rechazadas; P-04 prueba límites de 100/200 caracteres y capacidad decimal. | VERIFICADO |
| Capacidad opcional | Alta real sin capacidad y recuperación con null en P-01. No se inventó un mínimo de capacidad que el requisito no define. | VERIFICADO |
| Unicidad del nombre | Comprobación del caso de uso e índice único. P-03 y edición duplicada en navegador; se conserva el formulario para corregir. | VERIFICADO |
| Persistencia y alta sin nodos/paneles | Alta por servicio y por formulario, identificación por GUID y ausencia de creación automática de equipos. Fixtures de nodos/paneles pertenecen exclusivamente al ensayo. | VERIFICADO |
| Administrador y denegación a otros perfiles | Alta/listado/edición tienen autorización del servidor. Se probaron GET y POST directos con Técnico/Consulta. Véase limitación H-06 del servicio. | VERIFICADO en la entrada web actual |
| Formulario, mensajes y regreso al listado | Recorrido administrativo crea un parque, comprueba su aparición, edita, maneja duplicidad y elimina solo ese registro temporal. | VERIFICADO |

**Balance:** funcionalidad web verificada. No se detectaron campos o pantallas legítimas eliminados. La observación de autorización del servicio compartido se mantiene explícita.

### REQ002 — Autenticación y autorización con Keycloak

Exige realm `solaruy`, tres roles, usuarios de prueba, desafío OIDC de rutas protegidas, logout local/remoto, identidad/roles/claims visibles y secretos externos.

| Criterio | Implementación y evidencia | Estado |
| --- | --- | --- |
| Realm, cliente y usuarios de tres perfiles | JSON del realm contrastado y clonado en Keycloak temporal; login OIDC real de Administrador, Técnico y Consulta. | VERIFICADO localmente |
| Redirección del anónimo | Cada login inicia sin cookies y llega al formulario de Keycloak antes de entrar en SolarUY. | VERIFICADO |
| Interpretación de roles | `realm_access.roles` se convierte en claims de rol .NET; menús, controles y rutas difieren según perfil. | VERIFICADO |
| Nombre, roles y claims | Cabecera y `/mi-cuenta`; se abrió el detalle de claims también en móvil. No se incorporó gestión paralela de usuarios o contraseñas. | VERIFICADO |
| Logout de aplicación y proveedor | El formulario válido termina de nuevo en la pantalla que solicita credenciales. No bastó con ocultar el usuario en pantalla. | VERIFICADO |
| Solicitud de logout sin antiforgery | Antes devolvía HTTP 500; después de H-03 devuelve HTTP 400 sin cerrar la sesión. La solicitud válida sigue funcionando. | VERIFICADO tras corrección |
| Configuración externa | Authority/ClientId en configuración; secretos ausentes de los archivos de configuración del producto. Los ensayos usaron variables de entorno, sin reemplazar user-secrets reales. | VERIFICADO para la ejecución auditada |
| URLs públicas y despliegue de producción | No hay URL HTTPS pública definida ni entorno de producción ensayado. Tarea 5.3 sigue abierta. | NO COMPROBADO |

**Balance:** flujo local verificado; no constituye certificación de un despliegue de producción. El cliente confidencial de directorio de mantenimiento es distinto del cliente de login.

### REQ003 — Infraestructura de base de datos

Exige MariaDB en Compose, conexión externa, DbContext en Infrastructure, entidades/migraciones y procedimiento para crear tablas y compartir el arranque.

| Criterio | Implementación y evidencia | Estado |
| --- | --- | --- |
| MariaDB, volumen y phpMyAdmin | Compose revisado; MariaDB 11.8 ejecutada en contenedor y servicios originales presentes. Las pruebas no reinicializaron el volumen real. | VERIFICADO |
| DbContext, Pomelo, DI y conexión externa | Infraestructura implementa puertos; contextos y repositorios resueltos por DI en pruebas y host. .NET no lee automáticamente el .env de Compose. | VERIFICADO |
| Creación desde una base vacía | `dotnet ef database update --project SolarUY.Infrastructure --startup-project SolarUY.Presentation --no-build` aplicó las dos migraciones a `solaruy_audit_cli`, inicialmente vacía. | VERIFICADO |
| Idempotencia y modelo | DB-01 aplica migraciones dos veces, comprueba dos aplicadas, ninguna pendiente y `HasPendingModelChanges() == false`. | VERIFICADO |
| Relaciones y restricciones | FK huérfana y serie duplicada rechazadas por MariaDB; navegación y fechas recuperadas con EF. | VERIFICADO |
| Compatibilidad del volcado | Original importado: falla por nombres en minúsculas. Corregido: EF recupera seis paneles/seis tareas y reconoce el historial. | VERIFICADO tras corrección de nombres |
| Distribución al equipo | Existen .env.example y documentación, pero no evidencia de que todos los compañeros hayan recibido/configurado los secretos y arrancado el entorno. Tarea 1.3 abierta. | PARCIAL |

**Balance: PARCIAL por el criterio operativo del equipo.** La comprobación desde cero sí quedó cerrada y se actualizó la tarea 5.2.

El texto original pide una “primera migración” con Parque/Nodo/Panel. El historial entregado contiene `AddParqueSolar` y `AddMantenimientoPaneles`, y las especificaciones posteriores disponen conservarlo. Ejecutar ambas crea el modelo requerido; no se reescribieron migraciones aplicadas para hacer coincidir literalmente aquella frase.

REQ003 **no exige una pantalla propia de inventario**. Parques y mantenimiento son sus consumidores visuales legítimos.

### REQ004 — Mantenimiento de paneles

Exige programación sobre panel existente, responsable habilitado de Keycloak, estados y transiciones definidos, fechas UTC, filtros combinables, permisos por responsable y rechazo de escrituras antiguas.

| Criterio | Implementación y evidencia | Estado |
| --- | --- | --- |
| Dependencias Nodo/Panel y tarea Pendiente | Entidades, configuraciones y migración incremental; M-01 persiste y recupera panel, responsable y fechas. | VERIFICADO |
| Entradas inválidas | M-02 rechaza panel inexistente, responsable inválido, datos vacíos y fecha inválida. Formulario vacío muestra validaciones sin navegar. | VERIFICADO |
| Técnicos desde Keycloak | KC-01 fuerza paginación con más de 100 usuarios y comprueba roles directos, compuestos y de grupo; devuelve cinco técnicos habilitados en el fixture. | VERIFICADO |
| Cuenta de servicio de lectura | Token client_credentials; KC-03 intenta crear un usuario y recibe 403. Secreto incorrecto produce error controlado. | VERIFICADO |
| Crear, editar y reasignar | Servicio y formulario de Administrador; E2E-A cambia datos/responsable, recarga y conserva cambios. El nuevo técnico recibe la tarea. | VERIFICADO |
| Inicio y finalización | Pendiente no pasa directamente a Completada. El asignado inicia y completa; WEB-DB-01 recupera estado y realización UTC de la base después del navegador. | VERIFICADO |
| Cancelación e inmutabilidad | Técnico cancela E2E-B en curso; Administrador cancela E2E-C pendiente. Ambas se recuperan canceladas, sin fecha de realización. Terminales no permiten nuevas acciones ni escrituras. | VERIFICADO |
| Alcance por técnico | M-03 prueba filtros manipulados y mutaciones por ID ajeno. En navegador el parámetro ResponsableId de otro usuario no amplía el listado. | VERIFICADO |
| Permisos de creación/edición/asignación | M-04 rechaza operaciones directas del Técnico; M-DEN rechaza Consulta/anónimo. Rutas y POST restringidos también se comprobaron. | VERIFICADO |
| Filtros por estado, panel, técnico y vencimiento | Combinaciones comprobadas en servicio y navegador; terminales excluidas del vencimiento. Domain cubre igualdad estricta con referencia UTC. | VERIFICADO |
| Concurrencia | M-07/M-09 rechazan instantáneas o escrituras antiguas tras reasignación/finalización; no sobrescriben la versión actual. | VERIFICADO |
| Datos importados con responsables correctos | El volcado contiene identificadores de ejemplo que no corresponden al técnico de prueba. No permite demostrar sus tareas propias sin asignaciones válidas. H-05 permanece abierto. | FALLA en los datos del volcado, no en el filtro |
| Preparación del realm del usuario | La configuración aislada funciona; no se aprovisionó ni alteró el realm real durante esta auditoría. | NO COMPROBADO para ese entorno |

**Balance:** funcionalidad verificada con asignaciones válidas; **PARCIAL para usar el volcado como demostración de todos los perfiles**. No se relajó el filtro para que un técnico vea tareas ajenas.

### REQ005 — Simulador de inversor

Exige puerto sustituible IInversorClient, simulación coherente de potencia/salud, fallas configurables, órdenes por panel y estado del volcado. El frontend fue pedido posteriormente y el usuario confirmó Administrador opera; Técnico/Consulta leen.

| Criterio | Implementación y evidencia | Estado |
| --- | --- | --- |
| Puerto y desacoplamiento | Interfaz/DTO en Application; adaptador y DI en Infrastructure. UI usa IInversorService. Guardarraíles y lectura de dependencias. | VERIFICADO |
| Enumeración y consulta por nodo | Topología configurable comprobada; nodo desconocido rechazado. La pantalla muestra el nodo predeterminado y cuatro paneles. | VERIFICADO |
| Potencia diaria y límites | Reloj controlado: 400 W al mediodía para panel sano, mañana menor y noche cero. La prueba no depende de la hora del equipo. | VERIFICADO |
| Fallas | Panel configurado Fallado conserva salud y genera cero. | VERIFICADO |
| Activar/desactivar | Servicio y navegador comprueban estado y cero potencia al desactivar; otro panel no cambia; reactivación recupera generación con reloj diurno. | VERIFICADO |
| Volcado a red | Activación/desactivación y lectura del último estado; recorrido web restaura el estado inicial. | VERIFICADO |
| Permisos de operación | Técnico/Consulta carecen de controles y reciben UnauthorizedAccessException al invocar órdenes directamente sobre el servicio. | VERIFICADO |
| Presentación honesta | Nota visible indica simulación independiente de los paneles de MariaDB y estado en memoria hasta reiniciar. No se muestran estos datos como telemetría real. | VERIFICADO |
| Pruebas incluidas en la entrega | Se ejecutaron 19 comprobaciones temporales actuales, pero no hay suite permanente entregada que cierre las tareas 1.6/3.5. | PARCIAL |

**Balance: PARCIAL por la entrega de pruebas**, aunque el comportamiento auditado funciona.

Los identificadores de texto, máximo de 400 W, curva 06:00–18:00 y singleton ya estaban en la implementación anterior al frontend. Son decisiones técnicas del adaptador registradas en las especificaciones; no se presentan como nuevos requisitos del negocio ni como parámetros medidos de equipos reales. No se inventaron inversores físicos, históricos o persistencia del simulador.

### REQ006 — Gestión multiparque

Exige listado/búsqueda, alta reutilizada, edición, baja con integridad, configuraciones independientes, validación, mensajes y acceso administrativo.

| Criterio | Implementación y evidencia | Estado |
| --- | --- | --- |
| Consulta y búsqueda | P-05 prueba nombre, ubicación, espacios y sin coincidencias; navegador prueba buscar/limpiar. | VERIFICADO |
| Alta reutilizada | Mismo formulario/caso de uso de REQ001, accesible desde listado e inicio administrativo. | VERIFICADO |
| Edición e independencia | P-06 conserva Id/FechaCreacion y datos del segundo parque; maneja duplicidad. Navegador corrige un nombre duplicado sin perder el formulario. | VERIFICADO |
| Configuración | Se gestionan Nombre, Ubicacion y CapacidadEstimadaMW, únicos parámetros definidos por el modelo/especificación. No se agregaron otros campos. | VERIFICADO |
| Baja y confirmación | Diálogo real permite cancelar o confirmar; el parque temporal desaparece tras confirmar. | VERIFICADO |
| Cascada | P-08 elimina dependencias propias y conserva el otro parque; P-LEGACY prueba además LecturasPanel cuando existe. | VERIFICADO |
| Atomicidad | P-09 introduce un bloqueo FK temporal: falla la baja y la transacción revierte las eliminaciones previas. | VERIFICADO |
| Roles | GET y POST de listado/alta/edición denegados al Técnico y Consulta por el servidor. H-06 delimita la protección actual. | VERIFICADO en la entrada web actual |
| Mensajes y legibilidad | Resultados reales, errores controlados y capacidad sin ceros de escala; tablas desplazables en móvil. | VERIFICADO |

**Balance:** ABM web e integración verificados. La ausencia de autorización propia en el servicio debe tenerse presente antes de exponer otro punto de entrada.

## B. Hallazgos y problemas

| ID / gravedad | Archivo y ubicación | Evidencia y consecuencia | Estado |
| --- | --- | --- | --- |
| H-01 / alta | `solaruy.sql:30`, 53, 82, 104 y 133; INSERT/ALTER/FK asociados | El volcado creaba nombres en minúsculas y EF consultaba PascalCase. La importación original en MariaDB Linux produjo “Table ...Paneles doesn't exist”. Afectaba REQ003/004 y consumidores de persistencia. | CORREGIDO. Nueva importación consultable; no se renombraron tablas de la base real. |
| H-02 / media | `SolarUY.Infrastructure/Persistence/ParqueSolarConfiguration.cs:14` | P-02 recuperaba FechaCreacion.Kind=Unspecified desde un contexto nuevo. La entidad se crea con UTC; la persistencia perdía esa marca al rehidratar. REQ001/006. | CORREGIDO con conversión al leer. P-02 pasa y no hay cambios de modelo pendientes. No recalcula horas ni modifica registros. |
| H-03 / media | `SolarUY.Presentation/Program.cs:105` | POST /Account/Logout sin token: HTTP 500 y AntiforgeryValidationException sin controlar. REQ002. | CORREGIDO: IsRequestValidAsync devuelve una respuesta 400 controlada; logout válido vuelve a solicitar credenciales. |
| H-04 / baja | `SolarUY.Presentation/Components/Pages/ParquesSolares.razor:47` | Capturas mostraban 12,500000... por la escala decimal(65,30) recuperada de MariaDB. Perjudicaba legibilidad y ancho de tabla. REQ001/006. | CORREGIDO: formato decimal elimina ceros finales sin modificar valor, capacidad ni esquema. Captura posterior muestra 12,5 y 8. |
| H-05 / media | `solaruy.sql:120`–125; `TareaMantenimientoService`, alcance por usuario | Cinco filas usan TECNICO-PRUEBA-001/002 y otra un GUID de otro entorno. El realm versionado no fija los IDs de los usuarios. En la importación aislada Administrador ve seis tareas y el técnico de prueba cero. REQ004 y preparación de demo. | PENDIENTE. Las asignaciones abiertas deben resolverlas un Administrador con técnicos reales; no se reescribe historial de terminales ni se modifican datos reales automáticamente. |
| H-06 / media, limitación | `SolarUY.Application/ParquesSolares/ParqueSolarService.cs:8`; páginas administrativas | El servicio no recibe IUsuarioActual ni comprueba roles. Una invocación directa desde el proceso consulta sin autorización propia. Las rutas GET/POST actuales sí rechazan otros perfiles; no se demostró un bypass HTTP. REQ001/006. | PARCIAL. No se cambia automáticamente el constructor compartido ni su composición. Toda entrada nueva debe autorizar; decidir una defensa uniforme del servicio requiere revisar ese contrato y sus consumidores. |
| H-07 / media, entrega | `prompting/req005_Simulador del inversor/tasks.md`, 1.6 y 3.5 | Los requisitos piden tests incluidos. Los 19 checks locales y el navegador no son una suite permanente del repositorio. | PENDIENTE. Se conserva la aclaración respecto a las reglas del proyecto; no se marca terminado por tener un ejecutable ignorado. |
| H-08 / baja, documentación | `keycloak/README.md` y `prompting/req002_login_keycloak/specs.md` | Indicaban keycloak/.env.example y keycloak/.env, archivos inexistentes; Compose usa los de la raíz. | CORREGIDO. También se retiró BOM de los dos documentos tocados. |
| H-09 / baja, operación | REQ003 tarea 1.3; REQ002 tarea 5.3 | Falta evidencia de distribución al equipo y no existe URL pública para configurar producción. | PENDIENTE / NO COMPROBADO. No impide el ensayo local, sí impide afirmar entrega operativa en esos entornos. |
| H-10 / baja, documentación heredada | Documentos de REQ002, incluido requirements.md | Algunas tildes ya están representadas como texto mal decodificado. El escaneo confirma UTF-8 válido, lo cual no corrige automáticamente ese contenido. Hay BOM heredados en seis archivos no tocados. | PENDIENTE de limpieza documental si se solicita. No afecta los recorridos comprobados ni justifica reescribir requisitos durante la auditoría. |

No se encontró una falla crítica reproducible en los recorridos ejecutados. Esto no convierte la revisión en una prueba de ausencia de cualquier defecto posible.

### Cambios anteriores y alcance inventado

Git confirma que el ABM, baja con dependencias, mantenimiento y su lógica existían antes de la integración del frontend. Se conservaron sus servicios, campos y transiciones. No hay eliminaciones de archivos rastreados en el diff actual, ni cambios a Domain, migraciones o pruebas durante esta auditoría.

El inventario que se había añadido en la intervención anterior excedía REQ003; ya se había retirado al comenzar esta auditoría. Se verificó su ausencia en código, registros y navegación. También se había simplificado el inicio para retirar promoción e indicadores sin mediciones. El resultado actual conserva accesos funcionales, perfil y simulador respaldados por el pedido del usuario.

Los enlaces Counter/Weather del menú de plantilla no tenían páginas correspondientes en HEAD; su retirada no elimina una función implementada del negocio. No se añadieron altas de Nodo/Panel, telemetría real, reportes, chat ni gestión de usuarios.

La ampliación anterior de IInversorClient para enumerar nodos y el servicio de Application permiten mostrar la topología configurada y verificar permisos; tienen un uso concreto en /simulador. Esta auditoría no volvió a ampliar ese contrato. No se retiraron dependencias heredadas por parecer sobrantes sin demostrar antes que pueden eliminarse.

No existe historial Git de cada estado intermedio no guardado de las intervenciones anteriores. La comparación cubre los commits disponibles y la instantánea local inicial; no permite asegurar que se reconstruyó todo archivo temporal que hubiera existido.

## C. Cambios de esta auditoría

La comparación con los hashes iniciales identifica exactamente **nueve archivos existentes modificados**:

| Archivo | Cambio necesario |
| --- | --- |
| `SolarUY.Infrastructure/Persistence/ParqueSolarConfiguration.cs` | Conversión de rehidratación UTC de FechaCreacion; H-02. |
| `SolarUY.Presentation/Program.cs` | Respuesta controlada a antiforgery inválido en logout; H-03. Los registros del simulador ya estaban en el estado inicial. |
| `SolarUY.Presentation/Components/Pages/ParquesSolares.razor` | Solo el formato numérico de la celda de capacidad respecto al inicio de esta auditoría; H-04. |
| `solaruy.sql` | Sustitución de cinco nombres de tablas entre backticks en definiciones, datos y relaciones; H-01. Se comprobó que el resto del contenido y los registros son iguales al original. |
| `keycloak/README.md` | Rutas correctas a .env.example/.env y guardado sin BOM; H-08. |
| `prompting/req002_login_keycloak/specs.md` | Misma corrección de rutas y BOM; sin nuevos requisitos. |
| `prompting/req003_infraestructura_base_de_datos/tasks.md` | Tarea 5.2 completada con la nueva ejecución real del CLI sobre base vacía. |
| `prompting/req003_infraestructura_base_de_datos/implementations.md` | Registro de la auditoría actual, separado del antecedente histórico. |
| `prompting/frontend_integracion.md` | Aviso y enlace al informe vigente para no confundir pendientes históricos con el resultado actual. |

Se agregan este informe, `auditoria_integral_inventario.md` y `auditoria_integral_resultados.json`. No se debe interpretar todo el diff contra HEAD como cambios realizados en esta auditoría: buena parte pertenece al trabajo local anterior, preservado.

No se editaron .env, almacenes de secretos, AGENTS, reglas, skills, guardarraíles ni pruebas existentes. No se crearon nuevas migraciones ni se instalaron bibliotecas del producto. Los proyectos auxiliares de verificación se mantienen bajo artifacts ignorados.

## D. Pruebas y resultados

### Suite existente

Se ejecutó la solución completa al comenzar y después de corregir UTC y logout:

```powershell
dotnet test SolarUY.sln --no-restore -m:1 -p:BuildInParallel=false -p:BaseOutputPath=bin/AuditoriaIntegral/
```

| Proyecto | Aprobadas | Fallidas | Omitidas |
| --- | ---: | ---: | ---: |
| SolarUY.Domain.Tests | 42 | 0 | 0 |
| SolarUY.Application.Tests, existentes | 8 | 0 | 0 |
| CodeQuality.Tests | 9 | 0 | 0 |
| Total de la suite | 59 | 0 | 0 |

Después del último ajuste exclusivamente visual de capacidad se reconstruyó el host y se ejecutó CodeQuality.Tests: **9/9**. La presentación final se comprobó de nuevo en navegador. No se repitieron Domain/Application por un cambio de formato sin alteración de comportamiento. No se editaron pruebas para lograr resultados positivos ni se incorporaron nuevas pruebas unitarias.

### Integración adicional ejecutada en esta auditoría

| Grupo | Comprobaciones | Resultado |
| --- | ---: | --- |
| Persistencia, parques, mantenimiento, directorio y autorización | 35 | 35 correctas en la repetición final. El primer pase encontró H-02. |
| Simulador con reloj controlado y roles | 19 | 19 correctas. Curva, falla, estados, órdenes y denegaciones. |
| Importación del SQL corregido | 1 | Correcta. El SQL original había fallado por H-01. |
| Lectura de resultados del navegador en MariaDB | 3 | Correctas: finalización UTC/reasignación, cancelaciones y conservación de fixtures previos. |
| Cascada con tabla legada de lecturas presente | 1 | Correcta; conserva la lectura del otro parque. |
| Diagnóstico de responsables del volcado | 1 | Confirma H-05: seis tareas para Admin y cero para el técnico de prueba. Es evidencia del problema de datos, no conformidad de la demo. |

Son **59 comprobaciones adicionales de comportamiento satisfactorias y una comprobación diagnóstica**. Cada comprobación puede incluir varias condiciones. No se suman a las 59 xUnit para afirmar cobertura total ni un número de requisitos cumplidos.

Además se ejecutó EF CLI en una base inicialmente vacía: aplicó `20261008040649_AddParqueSolar` y `20261015023351_AddMantenimientoPaneles`. La integración repitió la aplicación y confirmó idempotencia. No se ejecutó Down ni se revirtió una base real.

Las pruebas de concurrencia usaron instantáneas/escrituras obsoletas reales y comprobación en almacenamiento; no fueron una prueba de carga masiva con múltiples procesos. El directorio incluyó usuarios deshabilitados, inexistentes y sin rol, además de roles heredados.

### Navegador: frontend → backend → almacenamiento → frontend

| Fase | Aserciones correctas | Recorrido |
| --- | ---: | --- |
| Administrador | 22 | OIDC, estilos y caché, tareas reales, opciones, simulador, búsqueda, confirmar/cancelar baja, alta-edición-baja temporal, perfil y menú móvil. |
| Mantenimiento | 27 | Validación, tres altas, edición/reasignación, recarga, tareas propias/ajenas, inicio/finalización/cancelación, estados terminales y filtros combinados. |
| Roles | 28 | Técnico/Consulta, menús, simulador solo lectura y nueve rutas restringidas con sus POST manipulados. |
| Visual | 83 | Diez pantallas a cuatro anchos, carga sin error, comprobación de desbordamiento y consola. |
| Detalles visuales finales | 15 | Formato decimal corregido, desplazamiento horizontal interno hasta acciones y claims abiertos en móvil. |
| Logout final | 3 | Login, rechazo HTTP 400 sin token y cierre válido de sesión local/remota. |
| Total de aserciones de navegador | 178 | Incluye comprobaciones de carga y autenticación repetidas; no equivale a 178 escenarios de negocio independientes. |

El recorrido de mantenimiento terminó inicialmente en la falla del logout sin token. Sus 27 comprobaciones anteriores fueron correctas; se corrigió el endpoint y se repitió su prueba en una fase dedicada. No se presenta aquel intento completo como exitoso. También se ajustaron esperas de interactividad y selectores de Radzen, y se completó el perfil de un usuario temporal adicional; los fallos de automatización no se contaron como nuevos defectos del producto.

Se observaron mensajes de reconexión del circuito antiguo al reiniciar intencionalmente el servidor temporal. Se repitieron las fases finales con página/consola limpias: roles, visual, detalles y logout terminaron sin excepciones JavaScript ni console.error. El servidor final registró únicamente el aviso esperado de token antiforgery ausente durante la prueba negativa, además del certificado local no confiable.

Las diez pantallas examinadas fueron Inicio, listado de parques, alta, edición, mantenimiento, creación de mantenimiento, simulador, perfil, acceso denegado y error. Anchos: **1440, 768, 390 y 320 px**. Las tablas extensas requieren desplazamiento horizontal dentro de su contenedor; se comprobó que pueden alcanzar las acciones sin ensanchar la página. No se afirmó que todas las columnas quepan simultáneamente en móvil.

El CSS versionado y sus estilos calculados se comprobaron con caché habilitada. La captura anterior del usuario sin cuadrícula/tarjetas no se reproduce en esta instancia. El ajuste de capacidad se comprobó tras recompilar; no requirió cambiar CSS ni su versión.

### Evidencia local y reproducción

Directorio: `artifacts/auditoria-integral/`, ignorado por Git.

- `Verification/Program.cs`: integración real, modos `dump`, `browser-result` y `legacy-cascade`; guardarraíl de puerto/base temporal.
- `Simulator/Program.cs`: 19 comprobaciones del simulador con reloj controlado.
- `browser.mjs`: fases admin, maintenance, roles, visual, details y auth con CDP.
- `captures/`: capturas de las diez pantallas en escritorio y móvil, perfiles, estados y acciones.
- `solaruy-realm.json`: fixture local de realm sin credenciales reales.

Capturas particularmente útiles: [inicio escritorio](../artifacts/auditoria-integral/captures/visual-1440-_.png), [capacidad corregida](../artifacts/auditoria-integral/captures/parques-formato-corregido.png), [acciones de mantenimiento en móvil](../artifacts/auditoria-integral/captures/movil-acciones-mantenimiento.png) y [acciones del simulador en móvil](../artifacts/auditoria-integral/captures/movil-acciones-simulador.png).

Se cerraron el navegador y el servidor temporales y se retiraron los dos contenedores de auditoría con sus bases y volúmenes anónimos. Los servicios originales de MariaDB, phpMyAdmin y Keycloak siguen activos. Los resultados y capturas quedan en disco. No se publica ni incluye una suite permanente del simulador por conservar estos artefactos; una clonación limpia no los obtiene. El anexo JSON versionable conserva resultados, no contraseñas ni tokens.

Comprobación final: `git diff --check` sin errores; archivos modificados válidos en UTF-8 sin BOM; ausencia de cambios en rutas protegidas, pruebas existentes y migraciones; ninguna eliminación de archivo rastreado.

### Comprobaciones no realizadas

No se comprobó el arranque en las máquinas de todos los compañeros, un despliegue público, Nginx/múltiples instancias, otro proveedor de inversor, ni dispositivos reales. Esos últimos elementos no forman parte del producto local solicitado. No se ensayó recuperación ante caída eléctrica, carga masiva, todos los navegadores o una evaluación completa de accesibilidad. No se ejecutó SonarQube ni un escaneo de vulnerabilidades de dependencias.

No se probó el borrado de datos reales, ni se alteraron responsables reales para conseguir una demo positiva. La instancia HTTP 5219 del usuario no se sustituyó por la compilación auditada. Estas limitaciones impiden extrapolar el resultado aislado a cualquier instalación sin comprobar su configuración.

## E. Veredicto final

1. **¿Están implementados los seis requerimientos originales?** Las funciones de los seis están representadas; no todos están cerrados. REQ003 conserva la aceptación operativa del equipo pendiente y REQ005 no entrega la suite permanente pedida. La demostración de REQ004 con el volcado requiere responsables válidos.

2. **¿Cada funcionalidad solicitada tiene frontend cuando corresponde?** Sí, en el alcance definido: alta/ABM de parques, identidad y logout, mantenimiento y simulador. La infraestructura de REQ003 no requiere inventar una pantalla; el alta de equipos o la gestión de usuarios no forman parte del alcance.

3. **¿Funciona la integración frontend/backend/base de datos?** Sí, en los recorridos reales ejecutados contra MariaDB y Keycloak aislados, incluida persistencia después de recargar. El simulador funciona por diseño en memoria y se identifica como tal. No se declara probada la configuración actual de cada entorno externo.

4. **¿Los tres roles funcionan según permisos?** Sí en las rutas y operaciones ensayadas. Mantenimiento y simulador también autorizan desde Application. Parques depende de la autorización del servidor en sus páginas; H-06 delimita una garantía que su servicio no ofrece por sí solo.

5. **¿Se introdujeron funciones inventadas o innecesarias?** Hubo un inventario añadido fuera de alcance en una intervención anterior y ya estaba retirado; se verificó su ausencia. No se añadieron funciones de negocio durante esta auditoría. Las pantallas actuales tienen respaldo en requisitos o pedidos explícitos del usuario.

6. **¿Se eliminó accidentalmente alguna función válida?** No se encontró evidencia de ello al contrastar HEAD, historial y estado local disponible. Se conservaron ABM, mantenimiento, autenticación y simulador. No puede reconstruirse cada archivo temporal no versionado que hubiera existido antes.

7. **¿Quedan problemas conocidos?** Sí: H-05 (datos/asignaciones), H-06 (límite del servicio), H-07 (entrega de pruebas) y H-09 (operación/producción), además de documentación heredada H-10. Los cuatro defectos de código/presentación reproducidos y las rutas documentales se corrigieron y verificaron.

8. **¿Está realmente listo para presentar y evaluar?** Está en condiciones de demostrar los recorridos locales comprobados con configuración y asignaciones válidas, declarando los pendientes. **No debe presentarse como seis requisitos completamente cerrados ni como despliegue de producción verificado.** Antes de una evaluación que exija entrega completa deben resolverse la evidencia del equipo, la verificación permanente de REQ005 y los responsables de la demo, sin alterar historiales terminales.

Para utilizar las correcciones de código en la instancia del usuario es necesario recompilar/reiniciar la aplicación .NET. Cambiar el archivo SQL no modifica una base ya importada; no se requiere recrear contenedores ni borrar volúmenes para estas correcciones.
