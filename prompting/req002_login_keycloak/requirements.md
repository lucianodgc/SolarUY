# Requerimientos: AutenticaciÃ³n y AutorizaciÃ³n Externa con OpenID Connect (Keycloak)

## DescripciÃ³n General
Como usuario del sistema SolarUY (Administrador, TÃ©cnico o Consulta), quiero autenticarme mediante un proveedor de identidad externo (Keycloak) utilizando el protocolo OpenID Connect, para acceder a la plataforma de forma segura y disponer de los permisos correspondientes segÃºn mi rol asignado.

## Criterios de AceptaciÃ³n

### 1. Servidor de Identidad y Roles (Keycloak)
- El sistema debe conectarse a un servidor Keycloak corriendo en un contenedor Docker con el realm denominado `solaruy`.
- Deben existir los siguientes roles definidos en el realm:
  - `Administrador`
  - `Tecnico`
  - `Consulta`
- Se deben incluir usuarios de prueba creados previamente en Keycloak para validar cada uno de los tres roles.

### 2. Flujo de AutenticaciÃ³n y VisualizaciÃ³n
- **ProtecciÃ³n de rutas:** Si un usuario no autenticado intenta ingresar a la aplicaciÃ³n, debe ser redirigido automÃ¡ticamente a la pantalla de inicio de sesiÃ³n (Login) de Keycloak.
- **Cierre de sesiÃ³n:** El sistema debe ofrecer la opciÃ³n de cerrar sesiÃ³n (Logout), finalizando la sesiÃ³n tanto en la aplicaciÃ³n como en Keycloak.
- **Identidad en pantalla:** Una vez autenticado, la aplicaciÃ³n (Blazor) debe mostrar visiblemente en la interfaz el nombre del usuario y sus claims/roles activos.

### 3. ConfiguraciÃ³n Externalizada y Manejo Seguro de Secretos
- La configuraciÃ³n de OpenID Connect (`Authority`, `ClientId`) debe externalizarse fuera del cÃ³digo C# utilizando `appsettings.json`.
- **Manejo de Secretos:** Los datos sensibles como el `ClientSecret` **NUNCA** deben guardarse en archivos de configuraciÃ³n rastreados por Git (`appsettings.json`). En entorno de desarrollo deben gestionarse mediante `user-secrets` (`dotnet user-secrets`) y en producciÃ³n mediante variables de entorno.

## Reglas de Negocio y Restricciones TÃ©cnicas
- **Desacoplamiento de ContraseÃ±as:** La base de datos del sistema (`MariaDB`) no debe almacenar contraseÃ±as ni credenciales. La gestiÃ³n de contraseÃ±as es responsabilidad exclusiva de Keycloak.
- **Mapeo de Roles:** La aplicaciÃ³n Blazor debe leer e interpretar los *claims* de roles del token emitido por Keycloak para habilitar o denegar funcionalidades de la interfaz segÃºn el perfil del usuario.