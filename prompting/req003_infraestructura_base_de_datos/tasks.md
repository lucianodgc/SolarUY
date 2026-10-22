# Tareas de Implementación: Infraestructura de base de datos

## Estado del alcance

La definición del modelo y la migración incremental se resolvieron en req004. Compose contiene MariaDB, phpMyAdmin y Keycloak. Este requerimiento no necesita una pantalla propia. El inventario agregado en la integración anterior se retiró por exceder el alcance original.

## 1. Aclaraciones previas

- [x] 1.1 Adoptar `ParqueSolar`, Nodo y Panel con atributos, identificadores, invariantes y relaciones resueltos en el MER y req004.
- [x] 1.2 Conservar `AddParqueSolar` y la migración incremental `AddMantenimientoPaneles`, sin reemplazar el historial.
- [ ] 1.3 Confirmar con el equipo el mecanismo de distribución de la configuración local no versionada. El uso actual de `.env` para Compose y user-secrets/variables de entorno para .NET está implementado y documentado; no equivale a confirmar cómo se distribuye al equipo.

## 2. Domain

- [x] 2.1 Reutilizar Nodo y Panel implementados por req004, con navegación, comportamiento e invariantes confirmados y sin referencias a EF Core.
- [x] 2.2 Reutilizar sus pruebas de dominio y comprobar la suite actual: 42 pruebas aprobadas.

## 3. Infrastructure

- [x] 3.1 Disponer del servicio MariaDB 11.8 LTS en Compose, con volumen persistente y secretos externos, conservando Keycloak.
- [x] 3.2 Mantener Pomelo 8.0.3 y EF Core 8, fábrica de diseño, DI y configuración externa sin secretos versionados.
- [x] 3.3 Reutilizar DbSet y configuraciones de entidades/relaciones implementados por req004.
- [x] 3.4 Conservar migración incremental y snapshot existentes, sin aplicar migraciones automáticamente al iniciar.

## 4. Presentation y documentación operativa

- [x] 4.1 Conservar host y cadena vacía versionada; no incorporar UI propia ni accesos directos de Blazor a Infrastructure.
- [x] 4.2 Documentar preparación local, `docker compose up -d`, conexión externa y comandos de EF Core en README y documentación operativa existente.

## 5. Verificación

- [x] 5.1 Confirmar el entorno iniciado con Compose: MariaDB healthy y Keycloak/phpMyAdmin activos, sin reiniciar los servicios para esta ampliación.
- [x] 5.2 Repetir `dotnet ef database update` sobre una base vacía. Ejecutado en la auditoría integral contra `solaruy_audit_cli`, en MariaDB 11.8 aislada: ambas migraciones aplicadas correctamente, sin tocar la base real. Ver `prompting/auditoria_integral.md`.
- [x] 5.3 Ejecutar CodeQuality.Tests con salida separada: 9 pruebas aprobadas, sin modificar guardarraíles.

## 6. Cierre y documentación

- [x] 6.1 Crear `implementations.md` con alcance compartido, cambios y comprobaciones.
