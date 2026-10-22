# Verificación final del entorno habitual

Fecha: 22/10/2026. Continuación de las dos auditorías, sin nueva refactorización ni cambios del producto.

**Veredicto:** el código actual compila, arranca y funciona en las consultas y pantallas comprobadas con tus servicios y configuración reales. Se puede utilizar para una presentación local de ese alcance. Antes de presentar las últimas correcciones desde HTTP 5219, hay que detener y volver a ejecutar esa instancia: sus binarios son anteriores a la corrección de autorización de parques. No la reinicié automáticamente.

No se declara una nueva comprobación de escrituras sobre datos reales. Altas, guardados, bajas, transiciones y reasignaciones conservaron la evidencia aislada de las auditorías; esta vez no se ejecutaron para respetar la instrucción de preservar tus datos.

## Compilación, pruebas y arranque

Comando ejecutado desde la raíz:

```powershell
dotnet test SolarUY.sln --no-restore -m:1 -p:BuildInParallel=false -p:BaseOutputPath=bin/VerificacionLocal/
```

| Proyecto | Aprobadas | Fallidas | Omitidas |
| --- | ---: | ---: | ---: |
| SolarUY.Domain.Tests | 42 | 0 | 0 |
| SolarUY.Application.Tests | 8 | 0 | 0 |
| CodeQuality.Tests | 9 | 0 | 0 |
| SolarUY.Integration.Tests | 19 | 0 | 0 |
| **Total** | **78** | **0** | **0** |

La ejecución reconstruyó los cuatro proyectos del producto. No se detectaron errores de compilación. Se repitieron también los nueve guardarraíles después de preparar el diagnóstico auxiliar, sin modificar pruebas ni código del producto.

MariaDB habitual está healthy en 3306; Keycloak 26.8.1 y phpMyAdmin siguen activos en 8080/8081. Se usaron `solaruy`, el realm `solaruy`, el cliente OIDC `solaruy-web` y el directorio `solaruy-tecnico-directory` existentes. Los secretos se leyeron desde la configuración local, sin mostrar ni modificar sus valores. El directorio real respondió y devolvió a `tecnico.test` como técnico habilitado.

Se conservó la instancia existente en HTTP 5219, PID 19720. La compilación actual se inició aparte en HTTPS 7271 —puerto ya definido en el perfil local— con el mismo contenido de configuración y los mismos servicios; no se usaron los contenedores temporales de las auditorías. El host arrancó correctamente, atendió login y los recorridos de las tres identidades. Al terminar se cerró únicamente esa instancia adicional y el navegador de verificación.

El arranque HTTPS registró el aviso de certificado local no confiable. El navegador de prueba aceptó ese certificado de desarrollo. No se detectaron excepciones del servidor en su salida durante los recorridos. No se intervino ni capturó la consola original de HTTP; para esa instancia la evidencia es su respuesta observada en el navegador.

## Instancia habitual y código actual

| Evidencia | Fecha local |
| --- | --- |
| Inicio del proceso habitual HTTP 5219 | 22/10/2026 01:47:40 |
| Última escritura de su SolarUY.Presentation.dll | 22/10/2026 01:47:40 |
| Última escritura de ParqueSolarService.cs con autorización | 22/10/2026 02:33:30 |

Las páginas de la instancia habitual también respondieron correctamente en consultas. Eso no prueba que haya incorporado código escrito después de su arranque. La nueva autorización y la base de componentes se comprobaron en la compilación actual de HTTPS 7271: listado, búsqueda, carga de edición y formulario de alta funcionaron con identidad real del circuito, sin errores de DI o AuthenticationStateProvider.

Para incorporar las últimas correcciones en la instancia habitual, detener su ejecución con Ctrl+C en la terminal que la inició y ejecutar desde la raíz:

```powershell
dotnet run --project .\SolarUY.Presentation\SolarUY.Presentation.csproj --launch-profile http
```

No se necesita recrear Docker, eliminar volúmenes, cambiar secretos ni aplicar migraciones: el historial de la base tiene las dos migraciones existentes y ninguna pendiente.

## Páginas y permisos comprobados personalmente

Los recorridos se ejecutaron en **ambas instancias**, con los tres usuarios de prueba reales. Finalizaron **54 aserciones por instancia, 108 en total**, sin excepciones JavaScript, console.error, alertas de error de aplicación o avisos visibles de fallo de circuito en las rutas examinadas. Las aserciones incluyen comprobaciones de carga; no equivalen a 108 escenarios independientes.

| Pantalla | Administrador | Técnico | Consulta |
| --- | --- | --- | --- |
| Perfil | Identidad, rol y claims reales | Identidad, rol y claims reales | Identidad, rol y claims reales |
| Parques | Dos parques, búsqueda sin coincidencias, limpieza, carga de edición y formulario de alta | Menú oculto; acceso directo a listado/alta/edición denegado | Menú oculto; acceso directo a listado/alta/edición denegado |
| Mantenimiento | Seis tareas, consulta/actualización, apertura y cierre de edición sin guardar; formulario de creación y opciones reales de paneles/técnicos | Una tarea correspondiente a su sub; sin edición/reasignación ni creación | Acceso directo a listado/creación denegado |
| Simulador | Consulta de cuatro paneles y presencia de controles administrativos | Consulta, sin controles de operación | Consulta, sin controles de operación |

La tarea visible para el técnico es la limpieza de `SOL-ROC-001`, EnCurso. Su sub real es `48a7200a-b030-4818-8979-41116e05e29f`, igual a su ResponsableId en MariaDB. El técnico no vio tareas con IDs de ejemplo ni pudo abrir creación de mantenimiento.

La simulación conserva su topología independiente y la advertencia visible correspondiente. No representa los seis paneles de MariaDB. La potencia observada fue cero en horario local nocturno; la variación diurna se comprueba en los tests permanentes con reloj controlado. No se cambiaron paneles ni volcado en ninguna instancia.

Una aserción inicial de acceso denegado confundía el formulario de cerrar sesión con un formulario de negocio; se corrigió únicamente el selector del script auxiliar. El servidor ya estaba mostrando Acceso denegado. Se repitió el recorrido completo y se conservaron tanto el resultado inicial como los resultados finales en artifacts.

## Conexión de interfaz y servicios

Se ejecutaron desde la interfaz la búsqueda/limpieza de parques y la actualización de mantenimiento/simulación. Se compararon las filas de mantenimiento con los resultados de EF y el sub autenticado. Los formularios de alta/edición de parques y de mantenimiento cargaron sus datos y opciones desde la configuración real.

También se revisó la vinculación de eventos existente:

| Acción | Caso de uso conectado | Ejecución en esta revisión |
| --- | --- | --- |
| Buscar/limpiar parques | IParqueSolarService.BuscarParquesSolaresAsync | Sí |
| Cargar edición de parque | ObtenerParqueSolarPorIdAsync | Sí |
| Guardar alta/edición o confirmar baja de parque | CrearParqueSolarAsync / ActualizarParqueSolarAsync / EliminarParqueSolarAsync | No; vinculación revisada y evidencia aislada anterior |
| Consultar tareas/paneles/técnicos | ITareaMantenimientoService.ListarAsync / ListarPanelesAsync / ListarTecnicosAsync | Sí |
| Guardar mantenimiento, reasignar o cambiar estado | CrearAsync / EditarAsync / IniciarAsync / CompletarAsync / CancelarAsync | No; vinculación revisada y evidencia aislada anterior |
| Consultar simulación | IInversorService.ListarNodosAsync / ConsultarAsync | Sí |
| Cambiar panel o volcado | EstablecerPanelActivoAsync / EstablecerVolcadoRedAsync | No; vinculación revisada y 19 pruebas permanentes aprobadas |

La apertura de “Editar / Reasignar” solo cargó el formulario y se cerró sin guardar. No se enviaron datos ni se hicieron pruebas de escritura simulando errores sobre la base real.

## Responsables existentes contrastados con Keycloak

El directorio real confirmó un técnico habilitado: `tecnico.test`, con el sub indicado arriba. **Cinco tareas no corresponden a un técnico habilitado de ese directorio**:

| Tarea / ID | Panel | Tipo | Estado | ResponsableId | Situación |
| --- | --- | --- | --- | --- | --- |
| `73cc2073-c854-11f1-939e-18c04de89d5b` | SOL-MAL-001 | Limpieza | Cancelada | `TECNICO-PRUEBA-001` | Terminal: conservar el responsable histórico |
| `73cc2270-c854-11f1-939e-18c04de89d5b` | SOL-MAL-002 | Inspección | Completada | `TECNICO-PRUEBA-001` | Terminal: conservar el responsable histórico |
| `73cc2304-c854-11f1-939e-18c04de89d5b` | SOL-MAL-003 | Diagnóstico | EnCurso | `TECNICO-PRUEBA-002` | Abierta: requiere decidir el técnico correcto y reasignar con Administrador |
| `73cc2351-c854-11f1-939e-18c04de89d5b` | SOL-MAL-004 | Limpieza | Completada | `TECNICO-PRUEBA-001` | Terminal: conservar el responsable histórico |
| `73cc2397-c854-11f1-939e-18c04de89d5b` | SOL-MAL-005 | Inspección | Cancelada | `TECNICO-PRUEBA-002` | Terminal: conservar el responsable histórico |

La sexta tarea, `73cc23dd-c854-11f1-939e-18c04de89d5b`, de `SOL-ROC-001`, está correctamente asignada a `tecnico.test`. El GUID que en el ensayo aislado no pertenecía al realm temporal **sí es válido en tu realm habitual**. La comprobación actual reemplaza cualquier suposición sobre su validez en este entorno.

El problema de datos abierto se concentra en el diagnóstico de `SOL-MAL-003`: tarea `73cc2304-c854-11f1-939e-18c04de89d5b`, EnCurso, con `TECNICO-PRUEBA-002`. Corresponde que el Administrador/equipo decida quién debe recibirla y la reasigne mediante el formulario existente. No se eligió un responsable automáticamente. Las otras cuatro tareas inválidas son terminales y no deben reescribirse para preparar la presentación.

## Conservación y evidencia

Se calcularon instantáneas ordenadas de todas las columnas del modelo de ParquesSolares, Nodos, Paneles y TareasMantenimiento antes y después de los recorridos. Coincidieron los registros y su SHA-256:

```text
AB0D800F4D9B676A774A8E6B466E8313EEBD3ED773C460AFE0F622813389107C
```

Recuentos conservados: **2 parques, 3 nodos, 6 paneles y 6 tareas**. No se ejecutaron SaveChanges, Migrate, SQL de escritura, bajas o reasignaciones en el diagnóstico.

No se modificó código del producto, pruebas, .env, user-secrets, Compose, realm compartido, contenedores o volúmenes. La instancia HTTP habitual y sus servicios continúan activos. Los únicos archivos de trabajo nuevos son este informe y los auxiliares/resultados/capturas ignorados de `artifacts/verificacion-local/`.

Evidencia local:

- `artifacts/verificacion-local/resultados.json`: resumen, dos resultados de navegador, snapshot y responsables.
- `browser-7271.json` y `browser-5219.json`: resultados finales por perfil/ruta.
- `before.json`: estado inicial sin secretos.
- `ReadOnly/Program.cs`: diagnóstico limitado a consultas EF y directorio.
- [Tareas propias del técnico, compilación actual](../artifacts/verificacion-local/captures/7271-tecnico.test-mantenimiento.png).
- [Simulador del Administrador, instancia habitual](../artifacts/verificacion-local/captures/5219-admin.test-simulador.png).

## Acciones pendientes y conclusión

1. Reiniciar/recompilar la aplicación de HTTP 5219 para presentar las últimas correcciones; la compilación actual ya arrancó correctamente con tus servicios reales en esta verificación.
2. Decidir y reasignar únicamente el diagnóstico abierto de SOL-MAL-003. Conservar las cuatro tareas terminales con responsables históricos.
3. Si la evaluación exige los pendientes operativos anteriores, aún falta que el equipo registre sus propios arranques y que se defina/configure/verifique un despliegue público. Esta ejecución confirma este equipo y entorno local, no los de otras personas.

**Podés utilizar y presentar el alcance local comprobado**, incorporando primero el binario actual en tu instancia habitual y explicando la asignación abierta pendiente. No se encontraron nuevas fallas de compilación, DI, autenticación o Blazor en los recorridos verificados. No se afirma una nueva validación de escrituras en datos reales ni un despliegue de producción.
