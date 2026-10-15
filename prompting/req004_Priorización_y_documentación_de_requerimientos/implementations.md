# Implementación: Mantenimiento de paneles

## Resultado

Implementadas las tareas de req004, con las decisiones confirmadas por el usuario. La funcionalidad está disponible en /mantenimiento y /mantenimiento/crear cuando el entorno tiene MariaDB, paneles existentes y el cliente de directorio configurados.

Se completaron el modelo, persistencia incremental, directorio de técnicos, permisos, casos de uso, interfaz y verificaciones. Las tareas están marcadas según resultados efectivos. No quedan aclaraciones funcionales pendientes de C1–C6.

## Comportamiento entregado

- Creación Pendiente con panel existente, Tipo texto, Descripcion y responsable obligatorios.
- ResponsableId corresponde al sub de Keycloak; no hay una tabla de usuarios/técnicos.
- Administrador crea, consulta, edita y reasigna tareas abiertas. Técnicos solo consultan y cambian estado de tareas propias.
- Inicio, finalización con FechaRealizacion UTC y cancelación según las transiciones aprobadas.
- Completadas y canceladas inmutables, sin reapertura ni cambios de panel.
- Consultas por estado, vencimiento, técnico y panel; instantes UTC y vencimiento estrictamente anterior a la referencia.
- Control de concurrencia sobre estado, responsable y datos originales, incluida la instantánea del formulario y comparación binaria.
- Directorio de Keycloak paginado con usuarios habilitados y roles efectivos directos, compuestos o de grupos; validación nueva del responsable al crear/asignar/editar.
- Errores de configuración, autenticación o conexión del directorio controlados y sin habilitar asignaciones no verificadas.
- Pantalla de acceso denegado para rutas administrativas en lugar de un 404.

## Archivos nuevos

| Ubicación | Archivos y propósito |
| --- | --- |
| SolarUY.Domain/Entities | Nodo.cs, Panel.cs y TareaMantenimiento.cs: relaciones y atributos del MER, invariantes y comportamiento. |
| SolarUY.Domain/Enums | EstadoPanel.cs y EstadoTareaMantenimiento.cs. |
| SolarUY.Domain.Tests | PanelNodoTests.cs y TareaMantenimientoTests.cs, creados junto con el comportamiento nuevo. |
| SolarUY.Application/Abstractions | IUsuarioActual.cs y UsuarioActualDto.cs: identidad y roles sin frameworks de presentación. |
| SolarUY.Application/Mantenimiento | ITareaMantenimientoService.cs, TareaMantenimientoService.cs, ITareaMantenimientoRepository.cs, IPanelRepository.cs e ITecnicoDirectory.cs: casos de uso y puertos. |
| SolarUY.Application/Mantenimiento | CrearTareaMantenimientoDto.cs, EditarTareaMantenimientoDto.cs, FiltroTareasDto.cs, TareaMantenimientoDto.cs, TareaMantenimientoOriginal.cs, PanelDto.cs y TecnicoDto.cs: entradas, resultados y concurrencia. |
| SolarUY.Application/Exceptions | MantenimientoException.cs, MantenimientoConflictoException.cs y TecnicoDirectoryException.cs. |
| SolarUY.Infrastructure/Persistence | NodoConfiguration.cs, PanelConfiguration.cs y TareaMantenimientoConfiguration.cs: EF, relaciones y UTC. |
| SolarUY.Infrastructure/Persistence | PanelRepository.cs y TareaMantenimientoRepository.cs: consultas restringidas y escrituras condicionadas. |
| SolarUY.Infrastructure/Identity | KeycloakTecnicoDirectory.cs: Admin REST API mediante client_credentials. |
| SolarUY.Infrastructure/Migrations | 20261015023351_AddMantenimientoPaneles.cs y su Designer.cs: migración incremental. |
| SolarUY.Presentation/Authentication | UsuarioActualService.cs: identidad del circuito con AuthenticationStateProvider. |
| SolarUY.Presentation/Components/Pages | CrearTareaMantenimiento.razor, MantenimientoPaneles.razor y AccesoDenegado.razor. |
| SolarUY.Presentation/Components | App.razor: restauración de la última versión disponible en el historial, requerida por Program.cs. |
| keycloak | TECNICOS.md: aprovisionamiento de service account, configuración externa, migraciones y uso. |
| Carpeta de req004 | implementations.md: este registro de entrega. |

## Archivos modificados

- SolarUY.Infrastructure/DependencyInjection.cs: fábrica de contextos por operación, repositorios, directorio y HttpClient compartido. La disponibilidad scoped del contexto para servicios anteriores se conserva.
- SolarUY.Infrastructure/Persistence/ApplicationDbContext.cs: conjuntos Nodos, Paneles y TareasMantenimiento.
- SolarUY.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs: modelo incremental.
- SolarUY.Presentation/Program.cs: registros de identidad y mantenimiento.
- SolarUY.Presentation/appsettings.json: configuración no secreta Keycloak:Directory.
- SolarUY.Presentation/Components/Layout/NavMenu.razor: acceso por roles al mantenimiento.
- keycloak/realm/solaruy-realm.json: cliente confidencial de directorio y service account con view-users/query-users, sin secreto fijo.
- .gitignore: retirada de la exclusión concreta de App.razor para recuperar un componente requerido.
- requierements.md, specs.md y tasks.md de esta carpeta: decisiones confirmadas, diseño final y avance verificable.

No se modificaron reglas, skills, guardarraíles de CodeQuality.Tests ni las versiones autorizadas. En la corrección posterior de EF CLI se incorporó el proveedor oficial Microsoft.Extensions.Configuration.UserSecrets 8.0.1 solicitado para compartir la configuración de diseño. Los cambios se guardaron en UTF-8 sin BOM. Las altas de Nodo/Panel, Inversor, LecturaPanel y el resto de req003/req005 permanecen fuera del alcance.

## Verificación automatizada final

Comando desde la raíz:

```text
dotnet test SolarUY.sln --no-restore --disable-build-servers -m:1 -p:OutputPath=bin/Req004Verification/net8.0 -v:minimal
```

| Proyecto | Aprobadas | Fallidas | Omitidas |
| --- | ---: | ---: | ---: |
| SolarUY.Domain.Tests | 36 | 0 | 0 |
| CodeQuality.Tests | 9 | 0 | 0 |
| SolarUY.Application.Tests, pruebas existentes | 8 | 0 | 0 |
| Total | 53 | 0 | 0 |

Se añadieron únicamente pruebas unitarias de Domain. Las pruebas existentes de Application se ejecutaron para comprobar regresiones; no se agregaron pruebas unitarias a las otras capas. La salida separada evitó detener la aplicación original que mantiene DLL de bin/Debug en uso.

## Migración y coherencia

La migración conserva AddParqueSolar y crea Nodos, Paneles y TareasMantenimiento, con unicidad de NumeroSerie y relaciones restrictivas. Usa convenciones existentes de Pomelo para tipos y convierte las fechas recuperadas a UTC.

Se ejecutó con éxito contra MariaDB 11.8 temporal:

```text
dotnet ef database update --project SolarUY.Infrastructure --startup-project SolarUY.Infrastructure --configuration Req004Verification --no-build
```

También se ejecutó:

```text
dotnet ef migrations has-pending-model-changes --project SolarUY.Infrastructure --startup-project SolarUY.Infrastructure --configuration Req004Verification --no-build
```

Resultado: sin cambios de modelo pendientes. Infrastructure como startup aprovecha su fábrica de diseño y EF Design existentes; no se añadió esa dependencia al host.

## Integración real y navegador

Se ejecutaron 39 comprobaciones de integración contra MariaDB 11.8 y Keycloak 26.8.1 aislados, usando un ejecutable temporal en artifacts ignorados y sin nuevas dependencias del producto:

- 35 comprobaciones de directorio paginado, 104 técnicos habilitados, roles directos/compuestos/grupos, usuarios inexistentes/deshabilitados/sin rol, persistencia, FK, UTC, creación, asignación, edición, estados, permisos por rol e identificador, filtros e inmutabilidad.
- Rechazo de escrituras anteriores a una finalización o reasignación.
- La service account recibió 403 al intentar escribir un usuario, confirmando permisos efectivos de solo lectura.
- 4 comprobaciones adicionales: edición del formulario con instantánea antigua y cambio solo de mayúsculas, alcance exacto del responsable, secreto incorrecto y secreto ausente.

Resultado: 39 aprobadas. Se usaron exclusivamente credenciales generadas para el entorno temporal; las llamadas del directorio usaron su cuenta de servicio y nunca credenciales administrativas humanas.

En Edge headless se verificaron login OIDC de administrador/técnico, creación Radzen, edición, inicio, finalización por técnico, cancelación propia, filtro de estado, ausencia de acciones administrativas para técnico y pantalla AccesoDenegado sin 404. Se inspeccionaron capturas del formulario y listado. Los scripts se ajustaron para esperar la interactividad de Blazor y seleccionar opciones visibles de Radzen; el recorrido completo terminó correctamente.

Evidencia local ignorada por Git: artifacts/req004/crear-mantenimiento.png, mantenimiento-tecnico.png y acceso-denegado.png. No se versionan el ejecutable de integración, perfiles ni fixtures de prueba.

## Limpieza y configuración del entorno real

Se retiraron los dos contenedores temporales, el navegador y su perfil, y la copia temporal de la aplicación. El Keycloak original y los demás servicios Docker permanecieron activos. La migración y las pruebas no alteraron el realm o la base de datos del usuario.

Para usar la funcionalidad en el entorno real, seguir keycloak/TECNICOS.md:

1. Preparar el cliente solaruy-tecnico-directory y los roles de lectura de su service account. El import no actualiza realms existentes.
2. Guardar Keycloak:Directory:ClientSecret mediante user-secrets en desarrollo o Keycloak__Directory__ClientSecret en despliegue, junto con URL/realm/client configurables.
3. Proporcionar ConnectionStrings__SolarUY externamente y aplicar la migración desde Infrastructure.
4. Contar con paneles existentes y ejecutar la aplicación actualizada.

No se guardaron secretos en Git ni se aplicaron migraciones automáticamente al arrancar. La configuración de secretos y el aprovisionamiento del realm existente se dejan al entorno correspondiente; las verificaciones se realizaron en recursos aislados.

Comprobaciones finales: git diff --check correcto, UTF-8 válido sin BOM y rutas protegidas sin cambios.

## Corrección de user-secrets para EF CLI

El usuario confirmó una MariaDB existente con la migración ya aplicada y pidió que el comando de EF con startup Presentation leyera automáticamente sus user-secrets. La fábrica anterior cargaba JSON y variables de entorno, pero omitía ese proveedor y sustituía una conexión ausente por string.Empty.

Cambios de esta corrección:

- Directory.Build.props: propiedad pública SolarUyUserSecretsId como única definición del identificador existente; no contiene una cadena de conexión ni credenciales.
- SolarUY.Infrastructure/SolarUY.Infrastructure.csproj: referencia Microsoft.Extensions.Configuration.UserSecrets 8.0.1 y UserSecretsId compartido.
- SolarUY.Presentation/SolarUY.Presentation.csproj: reutilización de la misma propiedad en UserSecretsId, conservando las demás referencias que ya tenía el usuario.
- SolarUY.Infrastructure/Persistence/ApplicationDbContextFactory.cs: AddUserSecrets con el atributo de su propio ensamblado, después de JSON y antes de las variables de entorno; validación explícita de conexión configurada. Infrastructure no referencia Presentation.
- keycloak/TECNICOS.md, specs.md, tasks.md e implementations.md de req004: instrucciones y trazabilidad de esta corrección.

Verificaciones ejecutadas:

```powershell
dotnet build
dotnet ef database update --project .\SolarUY.Infrastructure\SolarUY.Infrastructure.csproj --startup-project .\SolarUY.Presentation\SolarUY.Presentation.csproj
dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj --no-build --no-restore
```

Resultado: build correcto con 0 errores y 0 advertencias; EF terminó correctamente sin --connection, indicando que no había migraciones pendientes; 9 pruebas arquitectónicas aprobadas. El primer build dentro del sandbox terminó sin diagnóstico; se repitió el comando fuera de él para obtener y verificar el resultado anterior.

MSBuild confirmó el mismo UserSecretsId en ambos proyectos. Se comprobó también una conexión temporal en blanco mediante la variable de entorno: usando startup Infrastructure, la fábrica emitió el error claro esperado sin abrir la base y se restauró la variable al terminar. Con startup Presentation, EF puede detectar antes la ausencia de conexión al construir los servicios del host, mostrando el guardarraíl de configuración existente.

La conexión real se obtuvo exclusivamente del almacén externo ya configurado por el usuario. No se usó --connection en la verificación final, no se escribieron credenciales en el repositorio y no se modificaron los user-secrets. Las pruebas de Domain y Application no se repitieron en esta corrección de configuración; sus resultados anteriores corresponden a la entrega de mantenimiento.

## Preparación del cliente en el realm existente

El usuario confirmó que `solaruy-tecnico-directory` no existe en su realm actual. Se añadió `keycloak/clients/solaruy-tecnico-directory.json`, con la misma definición no secreta del import inicial, para importar únicamente ese cliente desde la consola. `keycloak/TECNICOS.md` detalla la importación, la asignación manual de `view-users`/`query-users` y el almacenamiento externo del secreto generado.

Se verificaron la validez del JSON, su igualdad con la definición del realm inicial, ausencia del campo secret y codificación UTF-8 sin BOM. Esta preparación no cambia código C# ni el entorno de Keycloak. No se repitieron build ni pruebas .NET; queda pendiente que el usuario importe el cliente, asigne los roles y configure su secreto para verificar el directorio en su entorno real.
