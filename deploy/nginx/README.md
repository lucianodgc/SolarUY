# Monitoreo con dos instancias

El proxy usa cookie de afinidad, WebSockets y respaldo entre dos aplicaciones. La cookie se actualiza con la instancia que efectivamente respondió, también cuando se utilizó el respaldo. Las zonas de upstream comparten el estado de disponibilidad entre workers de Nginx.

## Preparación

1. Configurar la conexión MariaDB y Keycloak externamente, sin guardar secretos en estos archivos.
2. Aplicar las migraciones con un único operador antes de iniciar los binarios nuevos:

   ```powershell
   dotnet ef database update --project SolarUY.Infrastructure/SolarUY.Infrastructure.csproj
   ```

   Si todavía está pendiente la migración de req011, cumplir primero sus comprobaciones de datos históricos. Para introducir esta primera versión distribuida, deshabilitar la adquisición en binarios anteriores: no generan avisos transaccionales. Mantener una instancia atendiendo la interfaz mientras se reemplaza la otra y habilitar la recolección únicamente en versiones compatibles con el registro compartido.

3. Usar la misma `DataProtection__RutaClaves` y el nombre de aplicación `SolarUY` en ambas instancias. La carpeta debe ser privada, accesible por las identidades de las aplicaciones y estar fuera del código y del contenido web. En hosts distintos debe ser realmente compartida; no bastan dos carpetas locales con el mismo nombre.
4. Usar identificadores diferentes y estables, por ejemplo `solaruy-a` y `solaruy-b`. El valor local predeterminado `solaruy-local` no sirve para las dos instancias simultáneas.

## Arranque local reproducible

En dos terminales, conservar la configuración externa común y establecer respectivamente:

| Variable | Instancia A | Instancia B |
|---|---|---|
| `ASPNETCORE_URLS` | `http://127.0.0.1:5051` | `http://127.0.0.1:5052` |
| `DistribucionMonitoreo__InstanciaId` | `solaruy-a` | `solaruy-b` |
| `DataProtection__RutaClaves` | directorio privado compartido | el mismo directorio |

En cada terminal ejecutar `dotnet run --project SolarUY.Presentation/SolarUY.Presentation.csproj --no-launch-profile`. Las dos pueden tener habilitada la recolección: el bloqueo compartido mantiene un único propietario por ciclo.

Para el proxy local de Docker Desktop:

```powershell
$env:SOLARUY_INSTANCIA_A = 'host.docker.internal:5051'
$env:SOLARUY_INSTANCIA_B = 'host.docker.internal:5052'
$env:SOLARUY_ID_A = 'solaruy-a'
$env:SOLARUY_ID_B = 'solaruy-b'
docker compose -f deploy/nginx/compose.yaml up -d
```

Abrir `http://127.0.0.1:18088/monitoreo`. El puerto puede cambiarse con `SOLARUY_PROXY_PUERTO`. Las direcciones de backend son parámetros; `host.docker.internal` corresponde al escenario local probado, no a una dirección universal de producción.

## Configuración operativa

- `DistribucionMonitoreo__IntervaloSegundos=1` y `DistribucionMonitoreo__TamanoLote=100` controlan el consumidor por instancia. No crean otro recolector ni leen inversores.
- Las credenciales, certificados TLS y claves de Data Protection permanecen externos. Integrar el bloque de proxy con el listener HTTPS existente de Luciano. Para interpretar `X-Forwarded-Proto`/`X-Forwarded-For`, configurar `ReverseProxy__Direccion` con la dirección real del proxy confiable; no confiar indiscriminadamente en cualquier cliente.
- Si cambian las direcciones de los backends, actualizar los parámetros y recargar/recrear la configuración del proxy manteniendo un backend disponible.
- Reemplazar una instancia por vez. El circuito Blazor pertenece al proceso original; si no puede recuperarse, el cliente de monitoreo recarga automáticamente y reconstruye el estado con la cookie compartida y el parque conservado en la URL.
- Los avisos se retienen sin purga automática en esta versión. Cada instancia conserva su cursor; una caída o entrega fallida no lo adelanta. No eliminar avisos o cursores manualmente durante la operación. La entrega es al menos una vez; el mismo aviso puede repetirse sin duplicar lecturas ni puntos.

## Verificación

`MonitoreoNginxIntegrationTests` utiliza dos procesos reales, el proxy, cookies compartidas y WebSockets. `SolarUY.Browser.Tests` prueba la misma aplicación en Chromium aislado: actualizaciones, selección de parques, repetición, liberación de conexiones, interrupción de MariaDB y relevo del circuito. Las instrucciones completas y resultados están en `prompting/req14_Monitoreo_en_Tiempo_Real_con_SignalR/implementations.md`.
