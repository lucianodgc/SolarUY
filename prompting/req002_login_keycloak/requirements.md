# Requerimientos: Autenticación y Autorización Externa con OpenID Connect (Keycloak)

## Descripción General
Como usuario del sistema SolarUY (Administrador, Técnico o Consulta), quiero autenticarme mediante un proveedor de identidad externo (Keycloak) utilizando el protocolo OpenID Connect, para acceder a la plataforma de forma segura y disponer de los permisos correspondientes según mi rol asignado.

## Criterios de Aceptación

### 1. Servidor de Identidad y Roles (Keycloak)
- El sistema debe conectarse a un servidor Keycloak corriendo en un contenedor Docker con el realm denominado `solaruy`.
- Deben existir los siguientes roles definidos en el realm:
  - `Administrador`
  - `Tecnico`
  - `Consulta`
- Se deben incluir usuarios de prueba creados previamente en Keycloak para validar cada uno de los tres roles.

### 2. Flujo de Autenticación y Visualización
- **Protección de rutas:** Si un usuario no autenticado intenta ingresar a la aplicación, debe ser redirigido automáticamente a la pantalla de inicio de sesión (Login) de Keycloak.
- **Cierre de sesión:** El sistema debe ofrecer la opción de cerrar sesión (Logout), finalizando la sesión tanto en la aplicación como en Keycloak.
- **Identidad en pantalla:** Una vez autenticado, la aplicación (Blazor) debe mostrar visiblemente en la interfaz el nombre del usuario y sus claims/roles activos.

### 3. Configuración Externalizada y Manejo Seguro de Secretos
- La configuración de OpenID Connect (`Authority`, `ClientId`) debe externalizarse fuera del código C# utilizando `appsettings.json`.
- **Manejo de Secretos:** Los datos sensibles como el `ClientSecret` **NUNCA** deben guardarse en archivos de configuración rastreados por Git (`appsettings.json`). En entorno de desarrollo deben gestionarse mediante `user-secrets` (`dotnet user-secrets`) y en producción mediante variables de entorno.

## Reglas de Negocio y Restricciones Técnicas
- **Desacoplamiento de Contraseñas:** La base de datos del sistema (`MariaDB`) no debe almacenar contraseñas ni credenciales. La gestión de contraseñas es responsabilidad exclusiva de Keycloak.
- **Mapeo de Roles:** La aplicación Blazor debe leer e interpretar los *claims* de roles del token emitido por Keycloak para habilitar o denegar funcionalidades de la interfaz según el perfil del usuario.
