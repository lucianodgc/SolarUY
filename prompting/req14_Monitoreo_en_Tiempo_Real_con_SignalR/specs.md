# Especificación: monitoreo en vivo

## Alcance vigente (05/11/2026)

Por pedido del usuario se simplifica la ejecución a una instancia. Este alcance reemplaza el acuerdo anterior D2 de distribución persistente entre instancias. Se mantienen .NET 8, Blazor Server, Radzen, EF Core, MariaDB y Keycloak. El balanceo queda para una etapa posterior.

## Flujo

1. TelemetriaEnVivoWorker consulta ITelemetriaEnVivoService cada 2 segundos. Application obtiene las muestras mediante IInversorClient, implementado con TCP en Infrastructure. El inventario se refresca cada 30 segundos.
2. El worker envía muestras por parque mediante NotificadorTelemetriaEnVivo y MonitoreoHub. Monitoreo y Simulador reciben el mismo evento dentro de la instancia.
3. Monitoreo convierte la muestra y actualiza su serie mediante servicios de Application. Razor presenta resultados sin sumar potencias ni acceder a repositorios.
4. RecoleccionLecturasWorker conserva su ciclo independiente cada 30 segundos. Guarda una muestra histórica, que puede diferir de la muestra visual por el instante de consulta. Reportes e Histórico leen esas mediciones persistidas.
5. Después del guardado se emite un aviso local LecturasConfirmadas. El fallo de entrega no revierte una escritura confirmada. La carga inicial y recuperación pueden consultar el historial; las muestras en vivo no requieren una consulta a la base por evento.

Los 2 segundos son la frecuencia configurada, no una garantía de latencia: la comunicación TCP y la disponibilidad del servidor pueden demorarlo.

## Persistencia

RecoleccionLecturasRepository mantiene la transacción del lote, la validación del nodo histórico, la exclusión del recolector y la prevención de duplicados. No escribe avisos, cursores ni secuencias técnicas.

La migración incremental 20261105010000_RemoveDistribucionMonitoreo retira únicamente AvisosMonitoreo, CursoresMonitoreo y SecuenciaMonitoreo. Se conservan las migraciones anteriores para actualizar bases existentes. El snapshot EF no cambia: las tres tablas se crearon con SQL fuera del modelo. El retroceso recrea las tablas vacías y no recupera sus mensajes eliminados.

Se eliminan IDistribucionMonitoreoRepository, su implementación, DistribucionMonitoreoWorker y su configuración. El encabezado de diagnóstico de instancia usa Instancia:Id opcional, sin almacenamiento en MariaDB.

## Seguridad e interfaz

Administrador, Tecnico y Consulta pueden consultar y suscribirse. El hub valida identidad, rol y existencia del parque; no permite escribir datos. El simulador conserva sus controles exclusivos para Administrador.

Al cambiar de parque se retira la suscripción anterior y se descartan mensajes de otros parques. Al reconectar se recupera la suscripción. La curva conserva como máximo 120 puntos, ordenados por instante, sin duplicar fechas ni unir silenciosamente huecos. Se distinguen valores ausentes y medidos a cero, mostrando la fecha UTC de la muestra.

## Verificación

- Compilar y ejecutar CodeQuality sin cambiar sus guardarraíles y las pruebas existentes de Domain.
- En una base aislada, comprobar que la migración retira las tablas técnicas y conserva inventario y lecturas.
- Comprobar recolección sin esas tablas y sin duplicar panel/instante.
- Verificar entrega local y autorización del hub con sus pruebas de integración.
- Comprobar en navegador que cambian valores y gráfico sin recargar.

Los escenarios históricos de Nginx/Playwright para dos instancias quedan expresamente omitidos porque dependían del transporte retirado. No presentarlos como verificación vigente.
