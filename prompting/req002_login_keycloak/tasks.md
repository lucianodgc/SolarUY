# Tareas de Implementación: Autenticación con Keycloak

## 1. Aclaraciones de configuración

- [x] **1.1** Definir `solaruy-web` como cliente local y `http://localhost:8080/realms/solaruy` como autoridad local; mantener ambos valores configurables.
- [x] **1.2** Definir el claim `realm_access.roles` en el ID token y mapear sus valores a claims de rol de .NET.
- [x] **1.3** Definir la importación del archivo del realm al iniciar Keycloak y proporcionar credenciales de prueba por variables locales no versionadas.

## 2. Keycloak e integración externa

- [x] **2.1** Preparar la configuración de Keycloak en Docker con el realm `solaruy`.
- [x] **2.2** Definir en el realm los roles `Administrador`, `Tecnico` y `Consulta`.
- [x] **2.3** Declarar usuarios de prueba en el realm y asignarles los roles para validar los tres perfiles.
- [x] **2.4** Usar la configuración local existente en `.env` en la raíz del proyecto e iniciar Keycloak para importar el realm y disponer de los usuarios de prueba.

## 3. Presentation: autenticación y configuración

- [x] **3.1** Configurar `Authority` y `ClientId` desde `appsettings.json` en el host Blazor Server.
- [x] **3.2** Configurar la lectura de `ClientSecret` desde `dotnet user-secrets` en desarrollo y desde variables de entorno en producción, sin incluirlo en archivos versionados.
- [x] **3.3** Configurar autenticación OpenID Connect con Keycloak y exigir autenticación para el acceso a la aplicación.
- [x] **3.4** Configurar el flujo para redirigir a Keycloak cuando un usuario no autenticado intente acceder.
- [x] **3.5** Incorporar una opción de cierre de sesión que cierre la sesión local y la sesión de Keycloak.
- [x] **3.6** Obtener el secreto generado para `solaruy-web` y guardarlo con `dotnet user-secrets` en el proyecto Presentation.

## 4. Presentation: identidad y roles

- [x] **4.1** Interpretar los claims de roles emitidos por Keycloak y reconocer `Administrador`, `Tecnico` y `Consulta` para la autorización de funcionalidades de interfaz.
- [x] **4.2** Mostrar en la interfaz el nombre del usuario autenticado y sus claims y roles activos.

## 5. Validación y configuración operativa

- [x] **5.1** Validar con cada usuario de prueba que el rol asignado desde `realm_access.roles` se reconoce en .NET y habilita o deniega las funcionalidades de interfaz correspondientes.
- [x] **5.2** Verificar que `Authority`, `ClientId` y `ClientSecret` se pueden configurar mediante variables de entorno en producción.
- [ ] **5.3** Registrar en Keycloak las URLs de redirección de producción cuando se defina la URL HTTPS pública de SolarUY.

## 6. Cierre y documentación

- [x] 6. Crear el archivo implementations.md en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.
