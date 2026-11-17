# Tareas de implementación: alta disponibilidad durante actualización

## 1. Contenedores y red

- [x] 1.1 Crear una imagen de ejecución .NET 8 para SolarUY.Presentation.
- [x] 1.2 Excluir archivos locales, secretos y artefactos de compilación del contexto Docker.
- [x] 1.3 Extender Compose con las instancias A y B, Nginx y la red compartida, conservando MariaDB, Keycloak y phpMyAdmin.
- [x] 1.4 Configurar la conexión MariaDB y los secretos de login web y directorio de técnicos desde variables externas de Compose.
- [x] 1.5 Mantener habilitada la recolección histórica solo en A para evitar que ambas instancias ejecuten el worker.

## 2. Balanceo, sesiones y salud

- [x] 2.1 Configurar Nginx con `ip_hash`, reintentos entre backends, soporte WebSocket para SignalR y buffers suficientes para cabeceras OIDC.
- [x] 2.2 Persistir las claves Data Protection en un volumen compartido por A y B y usar el mismo nombre de aplicación.
- [x] 2.3 Agregar el endpoint anónimo `/health` y health checks para las dos instancias.
- [x] 2.4 Mantener el health check existente de MariaDB y añadir un identificador de instancia a las respuestas para la demostración.

## 3. Operación y verificación

- [x] 3.1 Documentar el inicio de la pila y las direcciones públicas de SolarUY.
- [x] 3.2 Validar `docker compose config --quiet` con la configuración de Compose y un valor temporal para la variable obligatoria.
- [x] 3.3 Demostrar que Nginx sigue respondiendo al detener una instancia.
- [x] 3.4 Demostrar una actualización de A y B de forma secuencial, comprobando la disponibilidad pública durante el proceso.

## 4. Cierre y documentación

- [x] 4.1 Crear `implementations.md` con los cambios, archivos tocados y resultados de verificación; crear `resumen.md` con una síntesis breve del requerimiento.
