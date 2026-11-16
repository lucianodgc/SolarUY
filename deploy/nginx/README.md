# Proxy Nginx de alta disponibilidad

La pila integrada se define en el `compose.yaml` de la raíz. Nginx distribuye entre `solaruy-a` y `solaruy-b`, usa `ip_hash` para conservar afinidad y hace proxy de WebSockets. Si una instancia falla, reintenta la solicitud en la otra. Las conexiones activas de Blazor Server se reconectan al perder su proceso; no se migra el circuito en memoria.

Para levantar todos los servicios, preparar primero `.env` desde `.env.example` y ejecutar desde la raíz:

```powershell
docker compose up -d --build
```

La aplicación queda publicada en `http://localhost:18088`. `GET /health` responde sin autenticación y las dos instancias tienen health checks de Docker. Ambas montan el volumen privado `dataprotection_keys` y configuran el nombre de aplicación Data Protection `SolarUY`.

La guía de demostración para detener una instancia y reemplazar las imágenes de forma ordenada está en [Entrega 3](../../README.md#entrega-3-alta-disponibilidad).
