# Especificaciones Técnicas: Autenticación con Keycloak

## Alcance y arquitectura

SolarUY, aplicación Blazor Server organizada en Clean Architecture, autentica usuarios con Keycloak mediante OpenID Connect. Keycloak se ejecuta en un contenedor Docker y utiliza el realm `solaruy`. Las contraseñas son responsabilidad exclusiva de Keycloak; MariaDB no almacena contraseñas ni credenciales de usuario.

La autenticación y autorización se configuran en el host de Presentation y en la integración con el proveedor externo. No se requieren entidades, reglas ni persistencia de autenticación en Domain, Application o MariaDB.

## 1. Domain

- No se agregan entidades ni reglas de dominio: la identidad, las credenciales y los roles son administrados por Keycloak.
- No se guardan contraseñas ni credenciales en MariaDB.

## 2. Application

- No se agregan servicios ni casos de uso de autenticación. El flujo de inicio y cierre de sesión corresponde al middleware de autenticación y a la interfaz de Presentation.

## 3. Infrastructure

- Keycloak provee el servidor de identidad mediante OpenID Connect y se ejecuta en Docker.
- El realm requerido es `solaruy` y debe definir los roles `Administrador`, `Tecnico` y `Consulta`.
- Deben existir usuarios de prueba en Keycloak, con asignación que permita validar cada uno de los tres roles.
- El aprovisionamiento local se realiza importando `keycloak/realm/solaruy-realm.json` al iniciar el contenedor con `--import-realm`. Las contraseñas de los tres usuarios de prueba se sustituyen desde variables de entorno configuradas en `.env`.

## 4. Presentation

- La aplicación Blazor Server usa OpenID Connect para iniciar sesión con Keycloak.
- Las rutas de la aplicación requieren autenticación. Un usuario no autenticado que intente acceder debe ser enviado al inicio de sesión de Keycloak.
- La interfaz ofrece cierre de sesión que finaliza la sesión local y la sesión en Keycloak.
- Una vez autenticado, la interfaz muestra el nombre del usuario y sus claims y roles activos.
- La aplicación interpreta los claims de roles del token de Keycloak para permitir o denegar funcionalidades de interfaz según el perfil. Los nombres de rol a reconocer son `Administrador`, `Tecnico` y `Consulta`.
- `Authority` y `ClientId` se leen desde `appsettings.json`, no se codifican en C#.
- `ClientSecret` no se almacena en archivos versionados. En desarrollo se obtiene mediante `dotnet user-secrets`; en producción, mediante variables de entorno.
- Para el entorno local se define `Authority` como `http://localhost:8080/realms/solaruy` y `ClientId` como `solaruy-web`. Las URLs y el nombre del cliente deben ajustarse mediante configuración en otros entornos.
- El cliente `solaruy-web` es confidencial y utiliza flujo de código de autorización. El mapper del realm expone los roles como `realm_access.roles` en el ID token; Presentation convierte esos valores a claims de tipo rol.

## Configuración operativa pendiente

- Para ejecutar el entorno local, copiar `.env.example` a `.env`, definir contraseñas locales y guardar el secreto generado para `solaruy-web` con `dotnet user-secrets`.
- En producción se deben proporcionar `Authority`, `ClientId` y `ClientSecret` mediante variables de entorno, y registrar en Keycloak las URLs de redirección del despliegue.
