# Estado de implementación: infraestructura de base de datos

## Actualización de auditoría integral

Se ejecutó `dotnet ef database update --project SolarUY.Infrastructure --startup-project SolarUY.Presentation --no-build` sobre `solaruy_audit_cli`, inicialmente vacía, en una MariaDB 11.8 temporal. Se aplicaron las dos migraciones existentes. También se comprobó la idempotencia y ausencia de cambios pendientes del modelo mediante integración real.

El volcado `solaruy.sql` fallaba al consultarlo con EF en Linux porque sus tablas estaban en minúsculas. Se corrigieron únicamente los identificadores de tablas para coincidir con las migraciones; la importación corregida conserva los seis paneles y las seis tareas. No se modificaron migraciones ni datos reales. Se corrigió además la rehidratación UTC de `ParqueSolar.FechaCreacion`, sin cambiar el esquema. Permanece pendiente el acuerdo de distribución de configuración al equipo. Evidencia y límites en [auditoria_integral.md](../auditoria_integral.md).

## Registro de la revisión anterior

Compose contiene MariaDB 11.8 con volumen persistente, phpMyAdmin y Keycloak. Los secretos se obtienen fuera del repositorio. Nodo, Panel, configuraciones EF, conjuntos del contexto y la migración incremental provienen de req004 y se reutilizan. Se conserva el historial de AddParqueSolar y AddMantenimientoPaneles.

La lectura de la base existente confirma dos migraciones aplicadas y ninguna pendiente. No se crean migraciones ni se ejecuta el aprovisionamiento de una base vacía en esta revisión. La evidencia de creación desde cero registrada por req004 se conserva como antecedente; no cierra por sí sola una comprobación nueva.

## Alcance de presentación

Este requerimiento no pide una pantalla propia. El módulo `/inventario` agregado en la entrega anterior excedía el alcance: se retiraron su página, DTO, servicio, repositorio y registros de DI. Parques y mantenimiento continúan utilizando la persistencia existente.

## Verificación y pendientes

La revisión completa pasó 59 pruebas existentes. Se comprobó por lectura el historial y la conservación de los registros originales. No se modificaron tablas ni credenciales.

Quedan pendientes el acuerdo de distribución de configuración al equipo y la reproducción del aprovisionamiento en una base vacía, señalados en `tasks.md`. .NET utiliza user-secrets/variables de entorno y no importa automáticamente el `.env` de Compose.

Ver [frontend_integracion.md](../frontend_integracion.md) para el detalle de alcance y las limitaciones de la revisión.
