# Especificaciones Técnicas: Infraestructura de base de datos

## Contexto y alcance

La solución ya se ejecuta sobre .NET 8 y tiene una arquitectura Clean Architecture con las capas Domain, Application, Infrastructure y Presentation. La persistencia de MariaDB debe mantenerse como un detalle de Infrastructure; Domain no debe referenciar Entity Framework Core y Application no debe conocer `DbContext`, SQL ni el proveedor.

Las versiones autorizadas y ya resueltas por el proyecto son MariaDB 11.8 LTS, Entity Framework Core 8 (8.0.31 en las dependencias actuales) y `Pomelo.EntityFrameworkCore.MySql` 8.0.3. No se incorporarán proveedores, ORMs ni bibliotecas adicionales, ni se cambiarán esas versiones.

La inspección muestra que ya existen `ApplicationDbContext`, su fábrica de diseño, el registro de DI con Pomelo, la configuración externa `ConnectionStrings:SolarUY` / `ConnectionStrings__SolarUY` y la migración `20261008040649_AddParqueSolar`. También existe `compose.yaml`, pero actualmente solo define Keycloak: no hay un servicio MariaDB.

## Decisiones y aclaraciones requeridas

Antes de generar una migración que incluya las tres entidades, el equipo debe resolver y registrar lo siguiente:

1. El código solo contiene `ParqueSolar`; no existen `Nodo` ni `Panel`. Los requerimientos no definen atributos, identificadores, invariantes ni relaciones entre Parque, Nodo y Panel. No se pueden modelar ni configurar tablas o claves foráneas sin inventar el dominio. También se debe confirmar si «Parque» en este requerimiento corresponde a la entidad existente `ParqueSolar`.
2. `AddParqueSolar` ya es una migración versionada y solo crea `ParquesSolares`. El equipo debe decidir si se conserva como historial y se crea una migración posterior para el modelo confirmado, o si puede reemplazarse por una migración inicial completa. Esta segunda opción solo sería segura si no hay bases de datos ni historial compartido que preservar.
3. Para que el equipo pueda iniciar la base con `docker compose up -d`, debe acordar cómo se distribuyen los valores locales de MariaDB (nombre de base, usuario, contraseña y, si corresponde, contraseña de root) sin versionar secretos. El repositorio ya ignora `.env`; no se deben escribir credenciales en `compose.yaml`, `appsettings*.json` ni documentación versionada. La solución acordada debe ser compatible con el `.env` que el compose actual ya exige para Keycloak.

## Cambios por capa

### 1. Domain

No se modifica `ParqueSolar` para esta tarea: ya es la única entidad de dominio disponible y no se alterará solo para acomodar EF Core.

Una vez aprobada la definición funcional, se incorporarán las entidades de dominio `Nodo` y `Panel` y sus relaciones de navegación con el parque y entre sí, con comportamiento e invariantes definidos por el equipo. Las entidades no incluirán referencias a Entity Framework Core ni detalles de tablas. Sus pruebas de dominio se crearán junto con cualquier comportamiento o validación nuevo, conforme a las reglas del proyecto.

### 2. Application

No requiere cambios para preparar Docker, registrar el proveedor ni crear migraciones. `IApplicationDbContext` ya permite los casos de uso existentes de `ParqueSolar` sin depender de EF Core.

No se añadirán interfaces, repositorios, servicios ni casos de uso para Nodo o Panel en este alcance. Si un requerimiento posterior necesita operar esas entidades, definirá las abstracciones de Application correspondientes antes de su implementación en Infrastructure.

### 3. Infrastructure

Se extenderá el `compose.yaml` existente con un servicio MariaDB basado en la imagen 11.8 LTS. La definición debe usar valores externos acordados para sus secretos, una persistencia de datos definida para el entorno local y la publicación de conexión necesaria para que la aplicación y las herramientas de EF Core accedan a la instancia. No se modificarán los servicios de Keycloak fuera de lo estrictamente necesario para que la configuración compartida siga siendo válida.

Se conservará la configuración actual de EF Core Code-First: `ApplicationDbContext` en Infrastructure, `UseMySql` de Pomelo con `MariaDbServerVersion` 11.8, configuraciones de entidad mediante `IEntityTypeConfiguration<T>`, y `ApplicationDbContextFactory` para las herramientas de diseño. La cadena de conexión se obtendrá exclusivamente de configuración externa; `appsettings.json` seguirá sin secretos y las variables de entorno, incluidos `ConnectionStrings__SolarUY` y `MariaDB__ServerVersion`, prevalecerán para los entornos que las definan.

Tras la aclaración del modelo y de la estrategia de historial, se crearán los `DbSet` y las configuraciones de persistencia estrictamente necesarios para las entidades confirmadas. Las configuraciones declararán tablas, claves, propiedades y relaciones según el modelo aprobado, sin inferir columnas ni restricciones no especificadas. Se generará la migración que corresponda a la estrategia aprobada y se actualizará el snapshot de EF Core. No se aplicarán migraciones automáticamente al iniciar la aplicación.

### 4. Presentation

No requiere componentes Blazor, controladores ni lógica de negocio para esta infraestructura. El host ya llama a `AddSolarInfrastructure` durante el arranque y el archivo de configuración versionado contiene una cadena vacía, por lo que no se introducirán accesos de UI a Infrastructure.

Solo se mantendrá la configuración de host necesaria para leer la cadena de conexión externa y la versión de MariaDB; no se guardarán secretos en `appsettings*.json` ni en código fuente.

## Operación y documentación

Se documentará, en un archivo versionado de uso del proyecto, el procedimiento acordado para:

- preparar de forma segura la configuración local no versionada y levantar MariaDB con un único `docker compose up -d` una vez disponible dicha configuración;
- proporcionar `ConnectionStrings__SolarUY` y, cuando corresponda, `MariaDB__ServerVersion` a la aplicación y a `dotnet ef`;
- generar futuras migraciones y aplicar la migración vigente desde la raíz de la solución con `dotnet ef database update`, indicando los proyectos de Infrastructure y de inicio apropiados;
- distinguir la migración ya existente de la nueva estrategia aprobada, para no modificar historial compartido por accidente.

## Verificación

Después de implementar las decisiones pendientes, se verificará con la configuración local aprobada que:

1. `docker compose up -d` deja disponible el servicio MariaDB para los compañeros.
2. `dotnet ef database update` contra esa instancia termina correctamente y crea las tablas derivadas del modelo aprobado, incluidas Parque, Nodo y Panel cuando dichas entidades y relaciones estén definidas.
3. `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` pasa, para confirmar que los cambios respetan los guardarraíles arquitectónicos. No se modificarán las pruebas de calidad para conseguir ese resultado.
