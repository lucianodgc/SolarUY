# Especificaciones Técnicas: Infraestructura de base de datos

## Contexto y alcance

La solución ya se ejecuta sobre .NET 8 y tiene una arquitectura Clean Architecture con las capas Domain, Application, Infrastructure y Presentation. La persistencia de MariaDB debe mantenerse como un detalle de Infrastructure; Domain no debe referenciar Entity Framework Core y Application no debe conocer `DbContext`, SQL ni el proveedor.

Las versiones autorizadas y ya resueltas por el proyecto son MariaDB 11.8 LTS, Entity Framework Core 8 (8.0.31 en las dependencias actuales) y `Pomelo.EntityFrameworkCore.MySql` 8.0.3. No se incorporarán proveedores, ORMs ni bibliotecas adicionales, ni se cambiarán esas versiones.

Existen `ApplicationDbContext`, su fábrica de diseño, el registro de DI con Pomelo y la configuración externa `ConnectionStrings:SolarUY` / `ConnectionStrings__SolarUY`. Compose incluye MariaDB 11.8, phpMyAdmin y Keycloak. Se conservan `20261008040649_AddParqueSolar` y la migración incremental `AddMantenimientoPaneles`, incorporada por req004 con Nodo y Panel.

## Decisiones vigentes

Las decisiones sobre el modelo se resolvieron en req004; su especificación es la referencia de las entidades compartidas:

1. «Parque» corresponde a `ParqueSolar`. Nodo y Panel utilizan los atributos, relaciones, invariantes y fechas UTC del MER concretados en req004.
2. Se conserva el historial inicial y la migración incremental; no se reemplazan migraciones existentes.
3. Compose lee los valores locales de MariaDB y Keycloak desde `.env` no versionado. La aplicación utiliza user-secrets en desarrollo o variables de entorno; .NET no importa automáticamente `.env`. No se versionan secretos.

## Cambios por capa

### 1. Domain

No se modifica `ParqueSolar` para esta tarea ni se altera el dominio solo para acomodar EF Core.

Nodo y Panel y sus pruebas de dominio ya están implementados por req004, con navegación a ParqueSolar y sin dependencias de EF Core. La ampliación de interfaz no modifica el dominio.

### 2. Application

No requiere cambios para preparar Docker, registrar el proveedor ni crear migraciones. No se agregan servicios, repositorios ni casos de uso de inventario a este requerimiento.

### 3. Infrastructure

Se extenderá el `compose.yaml` existente con un servicio MariaDB basado en la imagen 11.8 LTS. La definición debe usar valores externos acordados para sus secretos, una persistencia de datos definida para el entorno local y la publicación de conexión necesaria para que la aplicación y las herramientas de EF Core accedan a la instancia. No se modificarán los servicios de Keycloak fuera de lo estrictamente necesario para que la configuración compartida siga siendo válida.

Se conservará la configuración actual de EF Core Code-First: `ApplicationDbContext` en Infrastructure, `UseMySql` de Pomelo con `MariaDbServerVersion` 11.8, configuraciones de entidad mediante `IEntityTypeConfiguration<T>`, y `ApplicationDbContextFactory` para las herramientas de diseño. La cadena de conexión se obtendrá exclusivamente de configuración externa; `appsettings.json` seguirá sin secretos y las variables de entorno, incluidos `ConnectionStrings__SolarUY` y `MariaDB__ServerVersion`, prevalecerán para los entornos que las definan.

Tras la aclaración del modelo y de la estrategia de historial, se crearán los `DbSet` y las configuraciones de persistencia estrictamente necesarios para las entidades confirmadas. Las configuraciones declararán tablas, claves, propiedades y relaciones según el modelo aprobado, sin inferir columnas ni restricciones no especificadas. Se generará la migración que corresponda a la estrategia aprobada y se actualizará el snapshot de EF Core. No se aplicarán migraciones automáticamente al iniciar la aplicación.

### 4. Presentation

No requiere una pantalla propia: configura la persistencia que utilizan parques y mantenimiento. Se retira el módulo `/inventario` agregado durante la integración de frontend, porque no estaba definido en los requisitos originales ni se solicitó explícitamente como nueva funcionalidad.

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
