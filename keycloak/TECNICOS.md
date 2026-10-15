# Directorio de técnicos y mantenimiento

La aplicación consulta usuarios habilitados con rol efectivo Tecnico mediante ITecnicoDirectory, implementado en Infrastructure con Keycloak Admin REST API. No existe una tabla paralela de usuarios/técnicos en MariaDB.

## Cuenta de servicio

El import del realm incluye el cliente confidencial `solaruy-tecnico-directory`, con service accounts activas, login interactivo y password grant deshabilitados. Su service account recibe únicamente `view-users` y `query-users` de `realm-management`. No asignar permisos de escritura ni usar credenciales humanas desde la aplicación.

`--import-realm` omite realms existentes. En un entorno existente, preparar explícitamente ese cliente y sus roles en Keycloak; en uno nuevo los crea el import. No borrar ni reiniciar el realm existente para incorporar esta configuración.

### Incorporar el cliente a un realm existente

Desde la consola de administración de `http://localhost:8080`, usando tu sesión de administración:

1. Seleccionar el realm **solaruy**.
2. Abrir **Clients → Import client** y seleccionar `keycloak/clients/solaruy-tecnico-directory.json`. Es la definición de un único cliente, sin secretos ni usuarios. Confirmar la importación con **Save**.
3. En el cliente creado, comprobar **Client authentication: On**, **Service accounts roles: habilitado**, **Standard flow: deshabilitado** y **Direct access grants: deshabilitado**.
4. Abrir **Service account roles → Assign role**, filtrar por roles de clientes y seleccionar únicamente **realm-management / view-users** y **realm-management / query-users**. Confirmar la asignación. La importación del cliente por sí sola no asigna estos roles.
5. Abrir **Credentials** y copiar el **Client secret** generado por Keycloak. Guardarlo con el comando siguiente, desde la raíz del repositorio.
6. Reiniciar SolarUY y volver a `/mantenimiento`. El filtro Técnico debe cargar usuarios habilitados con rol `Tecnico`; puede quedar vacío si no existen usuarios con ese rol.

Este procedimiento añade el cliente al realm seleccionado sin importar el realm completo ni reemplazar `solaruy-web`. La aplicación usa únicamente la cuenta de servicio; tu sesión administrativa se utiliza para esta preparación manual.

Keycloak genera el secreto. Obtenerlo de Credentials del cliente de servicio y proporcionarlo externamente. En desarrollo:

```powershell
dotnet user-secrets set "Keycloak:Directory:ClientSecret" "<secreto-del-cliente-de-servicio>" --project SolarUY.Presentation
```

En despliegue usar `Keycloak__Directory__BaseUrl`, `Keycloak__Directory__Realm`, `Keycloak__Directory__ClientId` y `Keycloak__Directory__ClientSecret`. BaseUrl debe usar HTTPS salvo localhost. No confundir este secreto con el de `solaruy-web`, usado para login OIDC. No guardar valores reales en Git.

El listado recorre todas las páginas y verifica roles efectivos, incluidos grupos y compuestos. Creación, asignación y edición validan que el responsable exista, esté habilitado y tenga rol Tecnico. Fallos del directorio generan errores controlados e impiden guardar asignaciones no validadas.

Fuentes: [Admin REST API](https://www.keycloak.org/docs-api/latest/rest-api/index.html) y [Service accounts](https://www.keycloak.org/docs/latest/server_admin/index.html#_service_accounts).

## Migración y uso

En desarrollo, guardar `ConnectionStrings:SolarUY` en los user-secrets de `SolarUY.Presentation`. Presentation e Infrastructure comparten el identificador definido en `Directory.Build.props`, de modo que la fábrica de diseño lee automáticamente la misma configuración. También se puede usar `ConnectionStrings__SolarUY`; las variables de entorno prevalecen sobre los secretos locales.

Ejecutar desde la raíz, sin `--connection`:

```powershell
dotnet ef database update --project .\SolarUY.Infrastructure\SolarUY.Infrastructure.csproj --startup-project .\SolarUY.Presentation\SolarUY.Presentation.csproj
```

La fábrica de diseño también admite Infrastructure como startup. Presentation conserva su referencia existente a EF Design para el comando anterior. Si falta la conexión o está en blanco, la fábrica indica que debe configurarse en user-secrets o variables de entorno, sin intentar conectar con valores vacíos. Si la aplicación activa bloquea bin/Debug, compilar con una salida separada antes de usar --no-build y su configuración correspondiente.

`AddMantenimientoPaneles` conserva el historial existente y crea Nodos, Paneles y TareasMantenimiento. No se aplica automáticamente al arrancar.

Acceder a `/mantenimiento` como Administrador o Tecnico. Administrador crea en `/mantenimiento/crear`, edita y reasigna tareas abiertas. Técnicos solo consultan y cambian estados de sus tareas. Las fechas de formularios y listados se introducen y muestran en UTC.

Se requieren paneles existentes: el alta de parques/nodos/paneles queda fuera de estas pantallas. Las tablas nuevas conservan relaciones restrictivas y no incluyen inversores ni lecturas, que pertenecen a otros requerimientos.
