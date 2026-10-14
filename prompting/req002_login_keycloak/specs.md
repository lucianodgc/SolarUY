# Especificaciones TÃ©cnicas: AutenticaciÃ³n con Keycloak

## Alcance y arquitectura

SolarUY, aplicaciÃ³n Blazor Server organizada en Clean Architecture, autentica usuarios con Keycloak mediante OpenID Connect. Keycloak se ejecuta en un contenedor Docker y utiliza el realm `solaruy`. Las contraseÃ±as son responsabilidad exclusiva de Keycloak; MariaDB no almacena contraseÃ±as ni credenciales de usuario.

La autenticaciÃ³n y autorizaciÃ³n se configuran en el host de Presentation y en la integraciÃ³n con el proveedor externo. No se requieren entidades, reglas ni persistencia de autenticaciÃ³n en Domain, Application o MariaDB.

## 1. Domain

- No se agregan entidades ni reglas de dominio: la identidad, las credenciales y los roles son administrados por Keycloak.
- No se guardan contraseÃ±as ni credenciales en MariaDB.

## 2. Application

- No se agregan servicios ni casos de uso de autenticaciÃ³n. El flujo de inicio y cierre de sesiÃ³n corresponde al middleware de autenticaciÃ³n y a la interfaz de Presentation.

## 3. Infrastructure

- Keycloak provee el servidor de identidad mediante OpenID Connect y se ejecuta en Docker.
- El realm requerido es `solaruy` y debe definir los roles `Administrador`, `Tecnico` y `Consulta`.
- Deben existir usuarios de prueba en Keycloak, con asignaciÃ³n que permita validar cada uno de los tres roles.
- El aprovisionamiento local se realiza importando `keycloak/realm/solaruy-realm.json` al iniciar el contenedor con `--import-realm`. Las contraseÃ±as de los tres usuarios de prueba se sustituyen desde variables de entorno configuradas en `keycloak/.env`.

## 4. Presentation

- La aplicaciÃ³n Blazor Server usa OpenID Connect para iniciar sesiÃ³n con Keycloak.
- Las rutas de la aplicaciÃ³n requieren autenticaciÃ³n. Un usuario no autenticado que intente acceder debe ser enviado al inicio de sesiÃ³n de Keycloak.
- La interfaz ofrece cierre de sesiÃ³n que finaliza la sesiÃ³n local y la sesiÃ³n en Keycloak.
- Una vez autenticado, la interfaz muestra el nombre del usuario y sus claims y roles activos.
- La aplicaciÃ³n interpreta los claims de roles del token de Keycloak para permitir o denegar funcionalidades de interfaz segÃºn el perfil. Los nombres de rol a reconocer son `Administrador`, `Tecnico` y `Consulta`.
- `Authority` y `ClientId` se leen desde `appsettings.json`, no se codifican en C#.
- `ClientSecret` no se almacena en archivos versionados. En desarrollo se obtiene mediante `dotnet user-secrets`; en producciÃ³n, mediante variables de entorno.
- Para el entorno local se define `Authority` como `http://localhost:8080/realms/solaruy` y `ClientId` como `solaruy-web`. Las URLs y el nombre del cliente deben ajustarse mediante configuraciÃ³n en otros entornos.
- El cliente `solaruy-web` es confidencial y utiliza flujo de cÃ³digo de autorizaciÃ³n. El mapper del realm expone los roles como `realm_access.roles` en el ID token; Presentation convierte esos valores a claims de tipo rol.

## ConfiguraciÃ³n operativa pendiente

- Para ejecutar el entorno local, copiar `keycloak/.env.example` a `keycloak/.env`, definir contraseÃ±as locales y guardar el secreto generado para `solaruy-web` con `dotnet user-secrets`.
- En producciÃ³n se deben proporcionar `Authority`, `ClientId` y `ClientSecret` mediante variables de entorno, y registrar en Keycloak las URLs de redirecciÃ³n del despliegue.
