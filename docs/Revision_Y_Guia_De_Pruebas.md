# Pruebas y calidad de SolarUY

[Inicio](../README.md) · [Configuración](Configuracion.md) · [Arquitectura](Arquitectura.md) · [Pruebas y calidad](Revision_Y_Guia_De_Pruebas.md)

Prerrequisitos y procedimientos de verificación automática/manual, SonarQube y evidencia registrada. Todos los comandos parten de la raíz del repositorio en PowerShell. Las instrucciones describen cómo repetir las pruebas; los resultados registrados llevan su fecha.

## Pruebas

### Automáticas de .NET

```powershell
dotnet test SolarUY.sln
```

Sin configuración externa se omiten casos que necesitan servicios. Para MariaDB, usa una base **desechable** con prefijo `solaruy_req007_test` y un usuario con permisos de migración y creación/eliminación de bases auxiliares:

```powershell
$env:SOLARUY_HISTORICO_TEST_CONNECTION = "Server=127.0.0.1;Port=3307;Database=solaruy_req007_test_local;User=USUARIO_PRUEBAS;Password=PASSWORD_PRUEBAS;DefaultCommandTimeout=120;"
dotnet test SolarUY.sln --logger trx --results-directory artifacts/pruebas
Remove-Item Env:SOLARUY_HISTORICO_TEST_CONNECTION
```

Los fixtures borran sus datos: nunca usar la base habitual. La regresión completa necesita también el simulador TCP local en otra terminal. Ejecutar secuencialmente escenarios que compartan base o puertos.

La carga histórica prepara un millón de lecturas y exige cuatro consultas concurrentes, menos de cinco segundos por consulta y menos de 256 MiB adicionales en .NET. Requiere disco/recursos disponibles; preparar datos no forma parte de la latencia medida.

### Modelo real y navegador

Con Ollama, modelo y secretos locales preparados:

```powershell
.\SolarUY.Integration.Tests/Verificar-Chatbot.ps1 -Ollama
```

El verificador prepara la conexión aislada desde la configuración local. Para filtros, otros proveedores y hosts de inspección, consultar [las integraciones](../SolarUY.Integration.Tests/README.md).

Playwright se ejecuta aparte. Ejemplo de salud en una instancia, con la conexión de pruebas definida en la misma terminal:

```powershell
dotnet build SolarUY.sln
Push-Location SolarUY.Browser.Tests
npm ci
npx playwright install chromium
npm test -- --config=playwright.salud.config.mjs
Pop-Location
```

Salud distribuida y órdenes necesitan además el proxy hacia hosts temporales: consultar [Nginx](../deploy/nginx/README.md) y sus fixtures. La configuración predeterminada de monitoreo conserva escenarios históricos del transporte retirado; no acredita toda la disponibilidad actual. Los hosts con identidad simulada tampoco sustituyen OIDC real.

### Recorrido manual de aceptación

Con datos de prueba, registrar usuario, acción, resultado esperado/obtenido y evidencia:

| Paso | Comprobación |
| --- | --- |
| Login | Entrar con los tres usuarios, revisar Mi cuenta, salir y volver a una ruta protegida. |
| Inventario | Administrador crea parque, dos nodos y paneles; edita, traslada y prueba capacidad, validaciones y baja lógica. Comprobar denegación con los otros roles. |
| Mantenimiento | Crear/asignar/editar tarea abierta; cambiar estados como técnico asignado y verificar aislamiento frente a otro técnico. Probar cancelación e inmutabilidad terminal. |
| Simulador y monitoreo | Observar potencia, activar/desactivar panel y volcado como Administrador. Cancelar confirmación no envía orden; otros roles no operan. |
| Histórico y reportes | Esperar muestras; consultar día/mes/año, objetivos y comparación donde corresponda. Contrastar energía, cobertura, gráficos, mantenimiento realizado y CSV. Distinguir cero de ausencia. |
| Salud | Configurar `InversorSimulador:PanelesConFalla` por serie/Guid en el proceso simulador y reiniciarlo; observar alerta e historial. Retirar configuración para comprobar recuperación. Desactivación no equivale a falla. |
| Chatbot | Saludo, resumen de hoy, histórico, mantenimiento autorizado y seguimiento; contrastar cifras. Probar rechazo de órdenes, ambigüedad, falta de datos, error del modelo y aislamiento entre usuarios. |
| Disponibilidad | En 18088, detener A y comprobar navegación/lecturas por B; restaurar A y repetir con B. Puede reconstruirse el circuito y perderse el chat en memoria. |

Prueba de caída:

```powershell
docker compose stop solaruy-a
# Comprobar navegación y nuevas lecturas en el navegador.
docker compose start solaruy-a
```

Repetir con B. Para demostrar actualización, aplicar previamente migraciones compatibles, construir y reemplazar una instancia por vez:

```powershell
docker compose build solaruy-a
docker compose up -d --no-deps solaruy-a
docker compose ps
# Esperar healthy y verificar navegación antes de reemplazar B.
docker compose up -d --no-deps solaruy-b
```

MariaDB libera la exclusión al detectar cierre de sesión; una partición de red no garantiza relevo instantáneo. Migraciones incompatibles con la versión aún activa requieren otro procedimiento: replicar las webs no garantiza actualizaciones arbitrarias sin interrupción.

## SonarQube

El repositorio incluye el **analizador**, no el despliegue del servidor. Inicia la instancia del equipo, verifica http://localhost:9000 y genera un token con permiso de análisis para `SolarUY`:

```powershell
$env:SONAR_TOKEN = "TU_TOKEN"
try {
    .\Analizar-Calidad.ps1
}
finally {
    Remove-Item Env:SONAR_TOKEN
}
```

Otro destino se selecciona con `-Servidor "URL" -Proyecto "CLAVE"`. Para incluir MariaDB define también `SOLARUY_HISTORICO_TEST_CONNECTION` a una base exclusiva: desde el contenedor del analizador, el host se alcanza por `host.docker.internal`, no por `127.0.0.1`.

El script copia fuentes a un entorno Linux separado, compila en serie, inicia simulador auxiliar, ejecuta suites e importa OpenCover de Domain, Application e integración. Recibe token por entorno, ejecuta sin root y exporta evidencia a una carpeta única de `artifacts/calidad/`. Espera el Gate y devuelve error si falla, sin cambiar sus condiciones o línea base.

## Estado y límites conocidos

Evidencia del **17/11/2026**, anterior a esta ampliación documental: **420 aprobadas, 0 fallidas y 14 omitidas**, con MariaDB/simulador aislados e incluyendo nueve controles arquitectónicos. Se corrigieron persistencia de paneles, borrado de inventario y configuración Docker. No acredita un nuevo recorrido completo de navegador con OIDC, relevo y Ollama real.

El último análisis de [SolarUY en SonarQube](http://localhost:9000/dashboard?id=SolarUY) registró cero bloqueantes y ratings A de Security, Reliability y Maintainability. El Gate seguía rojo por cobertura nueva **66,4 % frente al 80 % exigido** y **138 hallazgos nuevos**. Su corrección/capturas siguen pendientes en [req19](../prompting/req19_Calidad/tasks.md), además de recorridos visuales de nodos, paneles y chatbot.

La evaluación de salud por potencia baja permanece deshabilitada y su activación se rechaza mientras falte evidencia operacional suficiente. El contexto del modelo es finito; no se garantiza conversación ilimitada.

Compose usa HTTP local, Keycloak `start-dev` y red fija `172.29.0.0/24`. Producción requiere HTTPS, URLs/callbacks reales, gestión de secretos y disponibilidad de dependencias compartidas. Cambiar puerto/red requiere ajustar también Keycloak y proxy de confianza.

Los pendientes/evidencias por funcionalidad se mantienen en `prompting/`. El [README](../README.md) es el punto de entrada; [TECNICOS.md](../keycloak/TECNICOS.md), [Nginx](../deploy/nginx/README.md) y [las integraciones](../SolarUY.Integration.Tests/README.md) amplían procedimientos específicos.
