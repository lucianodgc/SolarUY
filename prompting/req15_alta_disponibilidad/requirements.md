# Entrega 3: alta disponibilidad durante actualización

## Requisito no funcional

La aplicación debe permanecer disponible si una instancia cae o se reemplaza por una versión nueva, mientras otra instancia sigue atendiendo tráfico.

## Alcance

- Construir la aplicación .NET 8 en una imagen Docker.
- Ejecutar dos instancias de la aplicación detrás de Nginx, junto con MariaDB y Keycloak en Docker Compose.
- Mantener afinidad por IP con `ip_hash` y permitir WebSockets para SignalR/Blazor Server.
- Persistir y compartir las claves de ASP.NET Data Protection entre las dos instancias.
- Añadir comprobaciones de salud para las instancias y MariaDB.
- Documentar la sustitución ordenada de una instancia por vez y la prueba de caída.

## Criterios de aceptación

- Al detener una instancia, Nginx deriva sus solicitudes a la instancia sobreviviente.
- Al actualizar las imágenes de una en una, el endpoint público continúa respondiendo y la instancia restante atiende tráfico.

## Límites operativos conocidos

- La afinidad de Nginx conserva la conexión SignalR en una instancia mientras esa instancia vive. Si el proceso cae, el circuito Blazor Server se reconstruye mediante la reconexión del cliente.
- En esta configuración solo `solaruy-a` ejecuta la recolección histórica. El worker actual no implementa una elección distribuida de propietario; habilitar ambos duplicaría trabajo.
