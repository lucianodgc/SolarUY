
# Monitoreo, salud y alta disponibilidad con Nginx

La pila integrada se define en el `compose.yaml` de la raíz. Nginx distribuye entre `solaruy-a` y `solaruy-b`, usa `ip_hash` para conservar afinidad y hace proxy de WebSockets. Si una instancia falla, reintenta la solicitud en la otra. Las conexiones activas de Blazor Server se reconectan al perder su proceso; no se migra el circuito en memoria.

## Preparación

1. Configurar la conexión MariaDB y Keycloak externamente, sin guardar secretos en estos archivos.

2. Aplicar las migraciones con un único operador antes de iniciar los binarios nuevos:

   ```powershell
   dotnet ef database update --project SolarUY.Infrastructure/SolarUY.Infrastructure.csproj
   ```

   Si todavía está pendiente la migración de req011, cumplir primero sus comprobaciones de datos históricos.

   Salud requiere `AddEstadoSaludAlertas`, que conserva lecturas y enums anteriores.

   D6 no agrega otra migración ni reinstala tablas de transporte.

   No mezclar binarios antiguos que escribían `AvisosMonitoreo` con la versión posterior a `RemoveDistribucionMonitoreo`: deshabilitar su adquisición y reemplazarlos según el procedimiento de esa versión.

   Mantener una instancia atendiendo mientras se reemplaza la otra.

3. Usar la misma `DataProtection__RutaClaves` y el nombre de aplicación `SolarUY` en ambas instancias.

   La carpeta debe ser privada, accesible por las identidades de las aplicaciones y estar fuera del código y del contenido web.

   En hosts distintos debe ser realmente compartida; no bastan dos carpetas locales con el mismo nombre.

4. Usar identificadores diferentes y estables, por ejemplo `solaruy-a` y `solaruy-b`.

   El valor local predeterminado `solaruy-local` no sirve para las dos instancias simultáneas.

## Arranque local reproducible

En dos terminales, conservar la configuración externa común y establecer respectivamente:

| Variable | Instancia A | Instancia B |
|---|---|---|
| `ASPNETCORE_URLS` | `http://127.0.0.1:5051` | `http://127.0.0.1:5052` |
| `Instancia__Id` | `solaruy-a` | `solaruy-b` |
| `SaludDistribucion__InstanciaId` | `solaruy-a` | `solaruy-b` |
| `SaludDistribucion__Habilitada` | `true` | `true` |
| `DataProtection__RutaClaves` | directorio privado compartido | el mismo directorio |

En cada terminal ejecutar:

```powershell
dotnet run --project SolarUY.Presentation/SolarUY.Presentation.csproj --no-launch-profile
```

Las dos pueden tener habilitada la recolección: el bloqueo compartido mantiene un único propietario por ciclo.

El Compose raíz habilita ambas como candidatas, con evaluación y observación de salud activas e identificadores distintos. Solo quien obtiene el bloqueo MariaDB adquiere las lecturas pendientes del intervalo UTC compartido. Si cae esa instancia, la restante puede adquirirlo en un ciclo posterior sin promoción manual; al volver, ambas siguen compitiendo por la misma exclusión. No existe una primaria fija ni se reconstruyen automáticamente los intervalos perdidos. Un bloqueo de sesión se libera cuando MariaDB detecta el cierre de la conexión: no se promete relevo instantáneo ante una partición de red.

B depende de MariaDB y Keycloak, no de que A esté sana. Ambas usan la imagen construida para `solaruy-a`; conservar esa imagen disponible al arrancar B por separado. La elección quedó registrada en [la coordinación de req016](../../prompting/req016_Estado_de_Salud_y_Alertas_de_Fallas/coordinacion_despliegue.md).

## Arranque mediante Docker Compose

Para levantar todos los servicios, preparar primero `.env` desde `.env.example` y ejecutar desde la raíz:

```powershell
docker compose up -d --build
```

La aplicación queda publicada en:

`http://localhost:18088`

Para acceder directamente al monitoreo:

`http://127.0.0.1:18088/monitoreo`

El puerto puede cambiarse con `SOLARUY_PROXY_PUERTO`.

Las direcciones de backend son parámetros; `host.docker.internal` corresponde al escenario local probado, no a una dirección universal de producción.

`GET /health` responde sin autenticación y las dos instancias tienen health checks de Docker.

Ambas montan el volumen privado `dataprotection_keys` y configuran el nombre de aplicación Data Protection `SolarUY`.

La guía de demostración para detener una instancia y reemplazar las imágenes de forma ordenada está en [Entrega 3](../../README.md#entrega-3-alta-disponibilidad).

## Configuración operativa

- `SaludDistribucion__IntervaloSegundos=1` controla el observador por instancia y debe ser menor o igual a `SaludPanel__EvaluacionSegundos`. Lee salud confirmada y topología; no crea otro recolector ni consulta inversores.

- `Instancia__Id` se conserva por compatibilidad para el encabezado de afinidad; si se omite, se usa `SaludDistribucion__InstanciaId`. Cuando estén ambos, mantener el mismo valor, coincidente con `SOLARUY_ID_A/B` del proxy.

- `SaludPanel__Habilitada=false` permite una instancia que solo observe salud; también desactiva su evaluación posterior a la adquisición. Mantener al menos un evaluador habilitado.

- Si se desea una instancia sin adquisición, establecer además `RecoleccionLecturas__Habilitada=false`. La verificación distribuida deshabilita ambas funciones en la instancia receptora para demostrar que no depende de procesamiento local.

- Las credenciales, certificados TLS y claves de Data Protection permanecen externos. Integrar el bloque de proxy con el listener HTTPS existente de Luciano.

- Para interpretar `X-Forwarded-Proto`/`X-Forwarded-For`, configurar `ReverseProxy__Direccion` con la dirección real del proxy confiable; no confiar indiscriminadamente en cualquier cliente.

- Si cambian las direcciones de los backends, actualizar los parámetros y recargar/recrear la configuración del proxy manteniendo un backend disponible.

- Reemplazar una instancia por vez. El circuito Blazor pertenece al proceso original; si no puede recuperarse, el cliente de monitoreo recarga automáticamente y reconstruye el estado con la cookie compartida y el parque conservado en la URL.

- Cada observador conserva en memoria las firmas del último snapshot entregado. Un fallo no las adelanta; al reiniciar invalida todos los parques actuales, incluidos vacíos, y reconstruye el seguimiento.

- El estado compartido y todo su historial permanecen en MariaDB. Los avisos convergen al estado vigente; pueden repetirse y no reproducen individualmente todas las transiciones intermedias. La consulta del historial conserva esas transiciones sin duplicarlas.

## Reemplazo de una instancia

1. Confirmar migraciones aplicadas por un único operador, política de salud igual en ambos procesos, claves compartidas y nombres de instancia consistentes con el proxy.

2. Mantener B disponible y reemplazar A.

   Si A era el único evaluador/recolector, habilitar esas funciones en B durante la operación o restablecer A para reanudar el procesamiento.

   La exclusión compartida evita dos adquisiciones del mismo panel/intervalo si se solapan.

3. Verificar `X-SolarUY-Instancia`, cookie de afinidad, autorización y salud vigente.

   El circuito perdido de Blazor se reconstruye desde MariaDB conservando el parque de la URL; el cliente que ya estaba en B sigue conectado.

4. Esperar recuperación de adquisición/evaluación y entrega antes de reemplazar B.

   No detener ambas instancias a la vez.

   En el primer despliegue de salud, aplicar antes el esquema incremental y no habilitar workers nuevos contra tablas inexistentes.

La ejecución con dos procesos y el relevo se verifican localmente.

Certificados, directorios compartidos en hosts distintos, reinicio de servicios y coordinación del entorno real se aplican por el operador responsable; estas instrucciones no acreditan una aprobación de otro integrante del equipo.

## Verificación

`SaludNginxIntegrationTests` utiliza dos procesos reales, el simulador TCP, proxy, cookies compartidas y WebSockets; prueba que A o B puedan ser la única fuente y que el otro observador reciba cambios/cierres.

`ObservacionSaludIntegrationTests` comprueba reintentos, reinicio, commits durante lectura y traslados.

Desde `SolarUY.Browser.Tests`, ejecutar:

```powershell
npm test -- --config=playwright.salud-distribuida.config.mjs
```

Esto verifica clientes distintos, salud, alertas e historial y el relevo completo de Blazor.

Definir:

- `SOLARUY_HISTORICO_TEST_CONNECTION` con una base aislada `solaruy_req007_test...`.
- `SOLARUY_MONITOREO_PROXY_URL` con el proxy para puertos 5051/5052.

Resultados y límites en:

`prompting/req016_Estado_de_Salud_y_Alertas_de_Fallas/implementations.md`
