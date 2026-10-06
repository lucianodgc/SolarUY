# Especificaciones Técnicas: Alta de Parque Solar

## Arquitectura y Patrones
- **Estilo:** Clean Architecture (Domain, Application, Infrastructure, Presentation/API).
- **Patrones:** Application Services (Arquitectura en capas clásica o CQRS pero mediante Servicios).

## Detalles de Implementación por Capa

### 1. Capa Domain (Core)
- **Entidad:** `ParqueSolar`
  - **Propiedades:** 
    - `Id` (Guid, PK)
    - `Nombre` (string, max 100 caracteres)
    - `Ubicacion` (string, max 200 caracteres)
    - `CapacidadEstimadaMW` (decimal, opcional)
    - `FechaCreacion` (DateTime, asignado en el constructor)
  - **Comportamiento:** Constructor con validaciones básicas (por ejemplo, `Nombre` y `Ubicacion` no pueden ser null o whitespace).

### 2. Capa Application (Servicios)
- **DTO de Entrada:** `CrearParqueSolarDto`
  - Propiedades: `Nombre`, `Ubicacion`, `CapacidadEstimadaMW`.
- **Validador:** `CrearParqueSolarDtoValidator` (usando FluentValidation)
  - Reglas: `Nombre` requerido (max 100), `Ubicacion` requerida (max 200).
- **Servicio:** `ParqueSolarService` (implementa la interfaz `IParqueSolarService`)
  - Dependencias: `IApplicationDbContext` (para acceso a BD).
  - Método: `CrearParqueSolarAsync(CrearParqueSolarDto dto)`
  - Lógica: 
    1. Validar que no exista ya un `ParqueSolar` con ese mismo nombre en la base de datos (lanzar excepción de validación o conflicto si existe).
    2. Instanciar la entidad `ParqueSolar`.
    3. Agregar la entidad al DbSet y hacer `SaveChangesAsync()`.
    4. Retornar el `Id` generado.

### 3. Capa Infrastructure (Persistencia)
- **Configuración de Entity Framework:** `ParqueSolarConfiguration : IEntityTypeConfiguration<ParqueSolar>`
  - Definir la tabla y longitud máxima de columnas.
  - Crear un **Índice Único** para la columna `Nombre` para garantizar a nivel de BD la regla de negocio.
- **DbContext:** Asegurarse de exponer `DbSet<ParqueSolar> ParquesSolares { get; set; }`.
- **Migraciones:** Se deberá generar y aplicar una migración para reflejar estos cambios en la base de datos.

### 4. Capa Presentation (Blazor Server con Radzen)
- **Componente:** `AltaParqueSolar.razor`
  - **Ruta:** `@page "/parques-solares/alta"`
  - **Seguridad:** Utilizar el componente `<AuthorizeView Roles="Administrador">` para proteger el contenido o el atributo `[Authorize(Roles = "Administrador")]`.
  - **UI (Radzen):**
    - Utilizar `RadzenTemplateForm` vinculado al modelo `CrearParqueSolarDto`.
    - Campos: `RadzenTextBox` para Nombre y Ubicación, `RadzenNumeric` para Capacidad Estimada.
    - Botón de envío: `RadzenButton` de tipo submit.
  - **Flujo:** Al ejecutarse el `OnValidSubmit`, el componente inyecta `IParqueSolarService` y ejecuta el método de creación. Tras el éxito, muestra una notificación con `NotificationService` de Radzen y redirige a la lista de parques con `NavigationManager`.
