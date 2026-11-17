# Pruebas y calidad

[Inicio](../README.md) · [Configuración](Configuracion.md) · [Arquitectura](Arquitectura.md)

## Pruebas automáticas

Desde la raíz: `dotnet test SolarUY.sln`. Para incluir MariaDB, define una base **desechable** cuyo nombre empiece por `solaruy_req007_test`:

```powershell
$env:SOLARUY_HISTORICO_TEST_CONNECTION = "Server=127.0.0.1;Port=3307;Database=solaruy_req007_test_local;User=USUARIO_PRUEBAS;Password=PASSWORD_PRUEBAS;DefaultCommandTimeout=120;"
dotnet test SolarUY.sln --logger trx --results-directory artifacts/pruebas
Remove-Item Env:SOLARUY_HISTORICO_TEST_CONNECTION
```

Los fixtures borran datos y requieren permisos para migrar y crear/eliminar bases auxiliares. Nunca uses la base habitual. La regresión completa necesita el simulador local en otra terminal; evita ejecutar escenarios simultáneos sobre la misma base/puertos. Una prueba omitida no equivale a aprobada.

Con Ollama y los secretos locales preparados: `.\SolarUY.Integration.Tests/Verificar-Chatbot.ps1 -Ollama`. Playwright se ejecuta aparte con Node/npm y Chromium; los prerrequisitos y escenarios específicos están en [integraciones](../SolarUY.Integration.Tests/README.md) y [Nginx](../deploy/nginx/README.md).

## Recorrido manual

Monitoreo recibe la última muestra vigente al suscribirse o muestra espera inicial. Salud y Reportes requieren lecturas guardadas: usa el arranque normal, sin `-SinGuardarHistorial`, y espera un ciclo de 30 segundos. Después actualiza el reporte. En las fechas puedes elegir hora; selecciona Rango para que la consulta respete esas horas, ya que Diario/Mensual/Anual abarcan períodos completos.

Usa datos de prueba y registra usuario, resultado esperado/obtenido y evidencia:

| Función | Qué comprobar |
| --- | --- |
| Login y permisos | Entrar/salir con los tres roles; revisar Mi cuenta y rechazos de operaciones no autorizadas. |
| Inventario | Crear, editar, trasladar y dar de baja paneles; validar campos, capacidad y conservación del historial. |
| Inicio | Comprobar accesos según rol: mantenimiento solo para Administrador/Tecnico; altas solo para Administrador. |
| Mantenimiento | Administrador crea/asigna/edita/cancela; solo el técnico asignado inicia/completa y puede cancelar sus tareas. Probar también llamadas directas, aislamiento e inmutabilidad terminal. |
| Simulador y monitoreo | Operar paneles/volcado únicamente desde Simulador como Administrador; cancelar confirmación no envía órdenes. Monitoreo muestra potencia, salud y alertas, y enlaza al Simulador. |
| Histórico y reportes | Esperar muestras de 30 segundos. Reportes integra detalle por parque/nodo/panel, comparación de períodos, gráficos, mantenimientos y CSV para cada período. Sin datos indica falta de mediciones, incluidas horas futuras. |
| Salud | Configurar `InversorSimulador:PanelesConFalla` por serie/Guid en el simulador y reiniciarlo; comprobar alerta y recuperación al retirar la configuración. |
| Chatbot | Probar resumen, histórico, seguimiento, falta de datos/error y aislamiento; contrastar respuestas y rechazar órdenes. |
| Disponibilidad | Detener una web, verificar navegación y nuevas lecturas por la otra; restaurar y repetir. El circuito/chat puede reiniciarse. |

Para caída: `docker compose stop solaruy-a`; después de comprobar, `docker compose start solaruy-a`. Repite con B. Para actualización, construye la imagen y reemplaza una instancia por vez con `docker compose up -d --no-deps solaruy-a` / `solaruy-b`, esperando `healthy` entre ambas. Aplica antes las migraciones compatibles; la redundancia web no cubre fallos de las dependencias compartidas.

## SonarQube

Con un servidor SonarQube disponible en 9000 y un token con permiso de análisis:

```powershell
$env:SONAR_TOKEN = "TU_TOKEN"
try { .\Analizar-Calidad.ps1 }
finally { Remove-Item Env:SONAR_TOKEN }
```

El repositorio incluye el analizador, no el servidor. Publica cobertura y espera el Quality Gate. Para incluir MariaDB, configura también la conexión aislada; desde su contenedor usa `host.docker.internal` para alcanzar el host. Resultados en `artifacts/calidad/`.

## Estado y límites conocidos

Última evidencia registrada, **17/11/2026**: 420 pruebas aprobadas, 0 fallidas y 14 omitidas. Quedan recorridos visuales con OIDC, nodos/paneles, chatbot y relevo real.

SonarQube publicó con cero bloqueantes y ratings A; el Gate seguía rojo por cobertura nueva de **66,4 % frente al 80 % exigido** y **138 hallazgos nuevos**. Correcciones/capturas pendientes en [req19](../prompting/req19_Calidad/tasks.md).

La evaluación de salud por potencia baja sigue deshabilitada por falta de evidencia operacional suficiente. Compose usa HTTP local y Keycloak de desarrollo; producción necesita HTTPS y callbacks públicos. Los requisitos y la evidencia detallada permanecen en `prompting/`.
