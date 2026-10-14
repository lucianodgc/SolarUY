# Tareas de Implementación: Infraestructura de base de datos

## 1. Aclaraciones previas

- [ ] 1.1 Confirmar si «Parque» se refiere a `ParqueSolar` y definir para `Nodo` y `Panel` sus atributos, identificadores, invariantes y relaciones de dominio necesarias para persistirlos.
- [ ] 1.2 Decidir y registrar si se conserva `20261008040649_AddParqueSolar` como historial y se agrega una migración posterior, o si se autoriza reemplazar la migración inicial al no existir historial ni bases compartidas que preservar.
- [ ] 1.3 Acordar y registrar el mecanismo de distribución de la configuración local no versionada de MariaDB —nombre de base, usuario y secretos— compatible con el `.env` exigido por el compose actual, de modo que el equipo pueda ejecutar `docker compose up -d`.

## 2. Domain

- [ ] 2.1 Incorporar, tras la definición aprobada, las entidades `Nodo` y `Panel` con las relaciones de navegación, comportamiento e invariantes confirmados, sin referencias a EF Core.
- [ ] 2.2 Crear las pruebas unitarias de Domain que cubran el comportamiento o las validaciones nuevos de Nodo y Panel definidos por el equipo.

## 3. Infrastructure

- [ ] 3.1 Extender `compose.yaml` con el servicio MariaDB 11.8 LTS, persistencia local y configuración externa de secretos acordada, conservando el funcionamiento de Keycloak.
- [ ] 3.2 Mantener la configuración de Pomelo 8.0.3 y EF Core 8 en `ApplicationDbContext`, la fábrica de diseño y el registro de DI, consumiendo `ConnectionStrings__SolarUY` y `MariaDB__ServerVersion` desde configuración externa sin secretos versionados.
- [ ] 3.3 Agregar al contexto los `DbSet` y las configuraciones EF Core de las entidades y relaciones confirmadas, sin definir propiedades o restricciones no aprobadas.
- [ ] 3.4 Generar la migración y actualizar el snapshot conforme a la estrategia de historial aprobada, sin aplicar migraciones automáticamente al iniciar la aplicación.

## 4. Presentation y documentación operativa

- [ ] 4.1 Conservar el arranque existente que registra Infrastructure y la cadena vacía en la configuración versionada; no agregar componentes ni accesos directos de Blazor a Infrastructure.
- [ ] 4.2 Documentar la preparación segura de la configuración local, el único comando `docker compose up -d`, la configuración externa de la cadena de conexión y los comandos de EF Core para generar y aplicar migraciones desde la raíz de la solución.

## 5. Verificación

- [ ] 5.1 Con la configuración local aprobada, comprobar que `docker compose up -d` deja MariaDB disponible.
- [ ] 5.2 Ejecutar `dotnet ef database update` contra esa instancia y comprobar que se crean las tablas del modelo aprobado, incluidas Parque, Nodo y Panel.
- [ ] 5.3 Ejecutar `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` y confirmar que los guardarraíles arquitectónicos pasan sin modificar sus pruebas.

## 6. Cierre y documentación

- [ ] 6.1 Crear el archivo `implementations.md` en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.