# Tecnologías autorizadas
- Plataforma: .NET 8.
- Base de datos: MariaDB 11.8 LTS.
- ORM: Entity Framework Core 8 con Pomelo.EntityFrameworkCore.MySql 8.0.3.
- Autenticación: Keycloak mediante OpenID Connect.
- Interfaz: Blazor Server con Radzen.

# Reglas
- No sustituir ni actualizar estas versiones, ni agregar proveedores, ORM o
- bibliotecas no autorizados, sin aprobación del equipo.
- Si hay una incompatibilidad, documentarla y pedir autorización. No cambiar
    versiones para resolverla.
- Las cadenas de conexión y los secretos van fuera del código fuente y de los
- archivos versionados.