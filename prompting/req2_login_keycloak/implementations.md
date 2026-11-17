# Implementación de req2: autenticación con Keycloak

## Recuperación de errores de login (05/11/2026)

En Program.cs, OnRemoteFailure registra el fallo y redirige a InicioSesionIncompleto.razor. La página permite acceso anónimo, muestra un mensaje comprensible y ofrece un enlace completo al inicio para comenzar una autenticación nueva. No se deshabilitaron las comprobaciones de correlación, estado, cookies o roles ni se agregó un reintento automático en bucle.

Verificación: compilación correcta y 9 pruebas de arquitectura aprobadas. En una instancia temporal en localhost:15219 se comprobó HTTP 200 para el aviso sin sesión y redirección a Keycloak para la página privada. Se inició un flujo y se envió su estado de vuelta sin la cookie de correlación: el servidor registró Correlation failed y respondió con redirección al aviso, sin error HTTP 500. Un retorno inválido sin estado también mostró el aviso. La instancia temporal se detuvo después de verificarla.

Esto controla la presentación y permite recuperar el intento. No acredita haber identificado o corregido la causa original del navegador del usuario: si las cookies están bloqueadas o no se conserva el recorrido de login, la validación seguirá rechazándolo. No se comprobó un login completo de los tres roles en esta verificación.

## Actualización de integración frontend

La cabecera conserva identidad y cierre de sesión visibles. `/mi-cuenta` concentra roles y claims, y el inicio/menú muestran accesos por perfil. Se verificó login OIDC y navegación con los tres usuarios de prueba, sin cambiar credenciales ni la configuración del proveedor. Detalle vigente en [frontend_integracion.md](../frontend_integracion.md); el resto documenta la integración original.

## Cambios y tareas completadas

- Se usó la configuración local existente en `.env` y se inició el servicio Keycloak con Docker Compose. Keycloak 26.8.1 importó correctamente el realm `solaruy`.
- Se obtuvo el secreto del cliente confidencial `solaruy-web` y se guardó con `dotnet user-secrets` para `SolarUY.Presentation`. El secreto no se incluyó en archivos versionados ni en este informe.
- Se recorrió el flujo OpenID Connect en desarrollo con los tres usuarios de prueba. El ID token incluyó el rol esperado para cada uno y la aplicación mapeó ese rol a las claims de .NET.
- `admin.test` accedió a la página protegida de alta de parques. `tecnico.test` y `consulta.test` fueron redirigidos a `/Account/AccessDenied`.
- Se verificó que `Authority`, `ClientId` y `ClientSecret` se leen de la configuración del host, que admite variables de entorno.

## Archivos modificados

- `prompting/req2_login_keycloak/tasks.md`: se actualizaron las tareas completadas y se registró el pendiente de las URLs de producción.
- `prompting/req2_login_keycloak/implementations.md`: se agregó este informe.

También se guardó el secreto de `solaruy-web` en el almacén local de .NET User Secrets. No se modificaron archivos de código ni se alteró el `.env` existente.

## Verificación

- Autenticación OIDC local y autorización de la ruta administrativa verificadas con los tres usuarios.
- No se ejecutó `dotnet test`.
- La tarea 5.3 queda pendiente porque la URL HTTPS pública de SolarUY aún no está definida; no se registraron redirecciones de producción ficticias en Keycloak.
