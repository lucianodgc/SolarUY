# Implementación de req001: alta de parque solar

## Actualización de integración frontend

El alta se integra en el inicio, el menú administrativo y el listado de parques, con formulario compartido visualmente con edición y opciones de regreso/cancelación. El recorrido de navegador confirmó alta de un parque temporal, edición y baja posterior. El historial de la base está aplicado y no hay migraciones pendientes. Detalle vigente en [frontend_integracion.md](../frontend_integracion.md); las secciones siguientes conservan la descripción de la entrega original.

## Decisiones autorizadas por el equipo

- MariaDB 11.8 LTS mediante Pomelo.EntityFrameworkCore.MySql 8.0.3 y EF Core 8. MariaDB es una decisión del equipo, no una exigencia de la letra del taller ni de requirements/specs/tasks.
- `IApplicationDbContext` permanece en Application, sin referencias a Entity Framework Core. Infrastructure implementa las operaciones de consulta, agregado y guardado con `ApplicationDbContext` y su `DbSet<ParqueSolar>`.
- Se incluye un listado mínimo en `/parques-solares`, exclusivamente como destino posterior al alta establecido por specs.
- La autenticación externa OpenID se integra en un requerimiento separado.

## Funcionalidad

`ParqueSolar` valida nombre y ubicación obligatorios, con límites de 100 y 200 caracteres. Genera un Guid y una fecha de creación UTC; la capacidad en MW es opcional. No incorpora nodos, paneles ni inversores.

`CrearParqueSolarDtoValidator` utiliza FluentValidation. El servicio verifica la existencia del nombre a través de `IApplicationDbContext`, agrega la entidad, ejecuta `SaveChangesAsync` y devuelve el Id. El índice único también impide duplicados concurrentes; Infrastructure traduce el error de duplicado de MariaDB en una excepción de Application. No se introduce normalización del nombre ni una collation adicional: la consulta y el índice utilizan la comparación configurada en MariaDB.

`/parques-solares/alta` utiliza Radzen y `[Authorize(Roles = "Administrador")]`. Muestra los errores, notifica el éxito y redirige al listado. El listado solo muestra nombre, ubicación y capacidad; no tiene edición, eliminación, filtros, búsqueda ni paginación. Cada página tiene un scope de servicios propio para disponer el contexto al abandonar el componente; el botón se deshabilita durante el guardado. La notificación se mantiene en el layout durante la navegación interactiva.

## Configuración de MariaDB

En `SolarUY.Presentation/appsettings.json`:

```json
{
  "ConnectionStrings": { "SolarUY": "" },
  "MariaDB": { "ServerVersion": "11.8.0" }
}
```

La cadena vacía es intencional. Configurar externamente `ConnectionStrings__SolarUY` con la cadena definida por el equipo. `MariaDB__ServerVersion` permite reemplazar la versión objetivo. No hay valores predeterminados de host, puerto, base de datos, usuario ni contraseña.

La fábrica de diseño lee appsettings del proyecto Presentation cuando se ejecuta desde la raíz de la solución, también admite appsettings del directorio actual y da prioridad a las variables de entorno. Para generar la migración utiliza una conexión sin abrir: no detecta la versión consultando un servidor.

## Migraciones explícitas

Migración generada: `20261008040649_AddParqueSolar`. Crea `ParquesSolares`, los límites de columnas y `IX_ParquesSolares_Nombre` UNIQUE.

Desde la raíz de la solución:

```powershell
dotnet ef migrations add AddParqueSolar --project SolarUY.Infrastructure --startup-project SolarUY.Infrastructure
```

El comando anterior ya fue ejecutado; no repetirlo para la misma migración.

Aplicación pendiente. Una vez definidos y configurados los datos reales de conexión, ejecutar explícitamente:

```powershell
dotnet ef database update --project SolarUY.Infrastructure --startup-project SolarUY.Infrastructure
```

No se ejecuta `Database.Migrate()` al iniciar ni se crea la base automáticamente desde la aplicación.

## Autorización y pendientes

Las páginas están protegidas para `Administrador` y se usa `AuthorizeRouteView`. Se registran los servicios y middleware de autenticación/autorización sin definir esquemas, usuarios, credenciales, cookies ni login local. Sin el proveedor OpenID y su esquema de autenticación/challenge, no se puede comprobar de punta a punta el acceso a estas páginas; el acceso HTTP directo a una ruta protegida no dispone todavía de un esquema para resolver el challenge.

Pendientes: conexión real y aplicación explícita de la migración (tarea 2.5), integración OpenID, asignación/mapeo del rol Administrador y verificación funcional con una sesión del proveedor. No se probó contra un servidor MariaDB ni en navegador autenticado.

## Calidad y pruebas

Se incluyen pruebas de dominio para valores obligatorios, límites, capacidad opcional e identificación/fecha; pruebas del servicio para nombre duplicado, validación antes de persistir, guardado e Id devuelto y conflictos al guardar.

Las reglas arquitectónicas conservan las restricciones de dependencias y nombres. Se corrigen los selectores regex mediante `ResideInNamespaceMatching` y se agrega el `Check` faltante en la regla de servicios. La regla de nombres selecciona implementaciones de interfaces de servicios para no tratar DTO, validadores o excepciones como servicios. Solo las dos reglas de repositorios admiten un conjunto vacío mediante `WithoutRequiringPositiveResults`, porque este requerimiento usa `IApplicationDbContext` y no introduce repositorios. Si existen repositorios, siguen sujetos a las restricciones originales.

Comandos de verificación:

```powershell
dotnet build SolarUY.sln
dotnet test SolarUY.sln
```

La configuración de SonarQube está fuera de SolarUY, en `../SonarQube/analizar.ps1`. En la revisión final se ejecutó ese script existente sin modificarlo ni agregar configuración al repositorio.

## Resultado de verificación

Build final: 0 errores y 0 advertencias. Tests: 27 aprobados (10 Domain, 8 Application y 9 CodeQuality), sin fallos ni omisiones. Se generó también el script SQL de la migración sin conectarse a MariaDB; contiene las columnas previstas y el índice único. La migración no fue aplicada a una base real.

Revisión final del 8 de octubre de 2026: no se encontraron errores funcionales que requirieran modificar el código. Application solo referencia Domain y FluentValidation, sin EF Core directo ni transitivo. Los componentes Presentation consumen Application; la referencia a Infrastructure se utiliza únicamente en el arranque para registrar dependencias, excepción expresamente permitida por las reglas. La entidad, la validación FluentValidation, la unicidad en servicio/base, las rutas y el listado mínimo coinciden con specs y las decisiones autorizadas. No hay login local ni migraciones al iniciar. Los guardarraíles mantienen las restricciones: solo admiten la ausencia de repositorios cuando el patrón no está utilizado y seleccionan los servicios por sus contratos para aplicar su convención de nombres.

SonarQube: análisis procesado correctamente, pero Quality Gate `ERROR` exclusivamente por `new_coverage = 0%` frente al mínimo de 80%. El script externo ejecuta build y análisis, sin ejecutar/importar un reporte de cobertura; este resultado no equivale a una medición real de cobertura de los tests. Las otras condiciones pasaron: seguridad y fiabilidad A, cero bloqueantes, cero hallazgos nuevos y cero duplicación nueva. Permanecen ocho code smells anteriores a req001: cuatro marcadores `AssemblyReference` vacíos, tres avisos de contraste del CSS de NavMenu y una recomendación de `RunAsync` sobre el `app.Run()` de la plantilla. El build instrumentado por el scanner produjo cinco advertencias C#; el build normal produjo cero. No se cambió código válido para silenciar esos avisos ni se alteró el Quality Gate desde SolarUY. La cobertura debe resolverse en el flujo externo de análisis.

La implementación está lista para commit con pendientes declarados; no corresponde declarar aceptación funcional completa hasta aplicar explícitamente la migración, integrar OpenID y comprobar el flujo con un usuario Administrador. También queda pendiente obtener una medición de cobertura y aprobar el Quality Gate. `tasks.md` conserva pendiente la tarea 2.5; la revisión de código de 5.2 está realizada, sin afirmar que SonarQube haya aprobado.

## Archivos creados

- `SolarUY.Application.Tests/ParqueSolarServiceTests.cs`
- `SolarUY.Application.Tests/SolarUY.Application.Tests.csproj`
- `SolarUY.Application/Abstractions/IApplicationDbContext.cs`
- `SolarUY.Application/Exceptions/NombreParqueDuplicadoException.cs`
- `SolarUY.Application/ParquesSolares/CrearParqueSolarDto.cs`
- `SolarUY.Application/ParquesSolares/CrearParqueSolarDtoValidator.cs`
- `SolarUY.Application/ParquesSolares/IParqueSolarService.cs`
- `SolarUY.Application/ParquesSolares/ParqueSolarService.cs`
- `SolarUY.Domain.Tests/ParqueSolarTests.cs`
- `SolarUY.Domain/Entities/ParqueSolar.cs`
- `SolarUY.Infrastructure/DependencyInjection.cs`
- `SolarUY.Infrastructure/Migrations/20261008040649_AddParqueSolar.Designer.cs`
- `SolarUY.Infrastructure/Migrations/20261008040649_AddParqueSolar.cs`
- `SolarUY.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`
- `SolarUY.Infrastructure/Persistence/ApplicationDbContext.cs`
- `SolarUY.Infrastructure/Persistence/ApplicationDbContextFactory.cs`
- `SolarUY.Infrastructure/Persistence/ParqueSolarConfiguration.cs`
- `SolarUY.Presentation/Components/Pages/AltaParqueSolar.razor`
- `SolarUY.Presentation/Components/Pages/ParquesSolares.razor`
- `prompting/req001_alta_parque_solar/implementacion.md`

## Archivos modificados

- `CodeQuality.Tests/DomainTests.cs`
- `CodeQuality.Tests/LayerTests.cs`
- `CodeQuality.Tests/NamingConventionTests.cs`
- `CodeQuality.Tests/RepositoryTests.cs`
- `SolarUY.Application/SolarUY.Application.csproj`
- `SolarUY.Infrastructure/SolarUY.Infrastructure.csproj`
- `SolarUY.Presentation/Components/App.razor`
- `SolarUY.Presentation/Components/Layout/MainLayout.razor`
- `SolarUY.Presentation/Components/Routes.razor`
- `SolarUY.Presentation/Components/_Imports.razor`
- `SolarUY.Presentation/Program.cs`
- `SolarUY.Presentation/SolarUY.Presentation.csproj`
- `SolarUY.Presentation/appsettings.json`
- `SolarUY.sln`
- `prompting/req001_alta_parque_solar/tasks.md`
