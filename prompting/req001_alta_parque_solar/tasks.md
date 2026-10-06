# Tareas de Implementación: Alta de Parque Solar

- [ ] **1. Capa Domain**
  - [ ] 1.1 Crear la entidad `ParqueSolar` en la capa de Dominio.
  - [ ] 1.2 Agregar las propiedades (`Id`, `Nombre`, `Ubicacion`, `CapacidadEstimadaMW`, `FechaCreacion`).
  - [ ] 1.3 Agregar constructor con validaciones básicas y asignación de `FechaCreacion`.

- [ ] **2. Capa Infrastructure (Persistencia)**
  - [ ] 2.1 Crear la clase `ParqueSolarConfiguration` implementando `IEntityTypeConfiguration<ParqueSolar>`.
  - [ ] 2.2 Configurar el índice único para la propiedad `Nombre` y las longitudes máximas.
  - [ ] 2.3 Agregar la propiedad `DbSet<ParqueSolar>` en el `ApplicationDbContext`.
  - [ ] 2.4 Generar la migración de base de datos (ej. usando el CLI de Entity Framework: `dotnet ef migrations add AddParqueSolar`).
  - [ ] 2.5 Actualizar la base de datos (ej. `dotnet ef database update`).

- [ ] **3. Capa Application**
  - [ ] 3.1 Crear el DTO `CrearParqueSolarDto`.
  - [ ] 3.2 Crear la clase `CrearParqueSolarDtoValidator` usando FluentValidation.
  - [ ] 3.3 Crear la interfaz `IParqueSolarService` y su implementación `ParqueSolarService`.
  - [ ] 3.4 Implementar la lógica del método de creación (inyectar DbContext, chequear unicidad de nombre, guardar cambios, retornar Id).
  - [ ] 3.5 Crear Test Unitario para verificar que el servicio lanza un error si el nombre del parque ya existe.

- [ ] **4. Capa Presentation (Blazor Server con Radzen)**
  - [ ] 4.1 Crear el componente `AltaParqueSolar.razor` con la directiva `@page "/parques-solares/alta"`.
  - [ ] 4.2 Restringir el acceso con `[Authorize(Roles = "Administrador")]` o `<AuthorizeView>`.
  - [ ] 4.3 Diseñar el formulario con `<RadzenTemplateForm>` y los controles de entrada (`RadzenTextBox`, `RadzenNumeric`).
  - [ ] 4.4 Implementar el método `OnValidSubmit` para invocar el método correspondiente en `IParqueSolarService`.
  - [ ] 4.5 Mostrar mensaje de éxito usando el `NotificationService` de Radzen y redirigir al usuario.

- [ ] **5. Verificación de Calidad (Guardarraíles)**
  - [ ] 5.1 Ejecutar suite de pruebas  para validar que no existan violaciones de Clean Architecture.
  - [ ] 5.2 Ejecutar inspección de código (o análisis de SonarQube si está configurado en el pipeline local) para asegurar que no hay code smells.
