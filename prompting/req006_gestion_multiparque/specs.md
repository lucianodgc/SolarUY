# Especificaciones técnicas: Gestión multiparque con configuraciones independientes

## Contexto y alcance

Esta funcionalidad amplía la gestión del agregado `ParqueSolar` ya incorporado por `req001`. El alcance es el ABM administrativo: consulta de listado y búsqueda, alta reutilizando el flujo existente, edición y baja. Cada operación actúa sobre una instancia identificada por su `Id`; por lo tanto, las modificaciones de un parque no pueden afectar los datos de otro.

Los datos de configuración actualmente definidos para `ParqueSolar` en el modelo aprobado son `Ubicacion` y `CapacidadEstimadaMW`, además de `Nombre`. No se agregarán atributos ni entidades para otros «parámetros de configuración» porque no están definidos en el modelo de datos ni en este requerimiento.

## Decisiones aprobadas

1. **Búsqueda:** se busca por `Nombre` y `Ubicacion`, con coincidencia parcial. Un criterio vacío o compuesto solo por espacios muestra todos los parques ordenados por nombre.
2. **Configuración:** los únicos datos gestionados son `Nombre`, `Ubicacion` y `CapacidadEstimadaMW`. No se agregan campos ni entidades de configuración adicionales.
3. **Baja con dependencias:** la baja es física y en cascada. Antes de ejecutarla, la interfaz debe pedir una confirmación explícita al Administrador. La operación elimina tareas de mantenimiento, lecturas si esa tabla existe, paneles, nodos y finalmente el parque, dentro de una transacción.

## Cambios por capa

### 1. Domain

- Extender `ParqueSolar` con un comportamiento de actualización que reciba `Nombre`, `Ubicacion` y `CapacidadEstimadaMW`, conserve `Id` y `FechaCreacion`, y aplique las mismas invariantes del constructor: nombre y ubicación obligatorios, con máximos de 100 y 200 caracteres respectivamente.
- La actualización de la entidad no resuelve unicidad ni accede a persistencia; esa verificación corresponde al caso de uso. No se incorporan campos de configuración no definidos, entidades de dependencias ni referencias a EF Core.
- Crear pruebas unitarias de dominio para la actualización válida, la conservación de identidad/fecha y las validaciones de entrada.

### 2. Application

- Ampliar `IParqueSolarService` y `ParqueSolarService` con los casos de uso necesarios para obtener un parque por `Id`, buscar/listar por coincidencia parcial de nombre o ubicación, actualizar sus datos y eliminarlos en cascada.
- Incorporar los DTO de entrada y salida estrictamente necesarios para edición, detalle y listado/búsqueda. El DTO de edición contendrá solamente `Nombre`, `Ubicacion` y `CapacidadEstimadaMW`; sus reglas de validación serán las ya aprobadas para esos campos.
- Ampliar la abstracción de persistencia de Application con operaciones por `Id`, consulta de listado/búsqueda, comprobación de nombre duplicado que excluya el propio `Id`, actualización y baja en cascada. Application seguirá dependiendo solo de esta abstracción y de Domain, sin conocer `DbContext`, SQL ni implementaciones de Infrastructure.
- Antes de guardar una edición, validar el DTO, obtener el parque, informar claramente si no existe, comprobar la unicidad de `Nombre` excluyendo el mismo parque y delegar la mutación en `ParqueSolar`. La creación existente se reutiliza sin duplicar su lógica.
- Convertir conflictos de unicidad e inexistencia en errores de negocio distinguibles para que Presentation pueda mostrar mensajes claros.
- Verificar mediante `IUsuarioActual` la identidad autenticada, identificador no vacío y rol Administrador en las seis operaciones del servicio, antes de validar entradas o acceder a persistencia. Se conserva `IParqueSolarService`; el constructor incorpora la dependencia de identidad ya existente en Application.

### 3. Infrastructure

- Implementar en `ApplicationDbContext` las operaciones de `IApplicationDbContext` para consulta por identificador, búsqueda/listado, actualización y baja en cascada, usando EF Core solo en esta capa.
- Mantener el índice único de `ParquesSolares.Nombre` como garantía frente a concurrencia y traducir su violación al error de negocio correspondiente también durante una edición.
- La modificación de los datos existentes de `ParqueSolar` no requiere una migración. La cascada se realiza en una transacción de Infrastructure para respetar las claves foráneas existentes sin alterar migraciones versionadas ni un esquema que aún no modela todas las dependencias.

### 4. Presentation

- Mantener las rutas y el acceso de alta existentes, reutilizando `AltaParqueSolar.razor` para registrar parques.
- Extender `ParquesSolares.razor` para mostrar el listado, el control de búsqueda por nombre o ubicación, y acciones de navegar a edición y de solicitar baja. La vista presentará estados vacío, carga y errores de consulta con mensajes comprensibles.
- Crear una página de edición protegida para Administrador que cargue el parque por `Id`, presente los campos existentes y envíe el DTO de actualización. Debe comunicar éxito, errores de validación, duplicidad, inexistencia y errores inesperados sin contener reglas de negocio.
- La acción de baja estará protegida para Administrador, pedirá confirmación en la interfaz y mostrará el resultado o error de la operación.
- Todas las páginas y acciones de gestión de parques conservarán `[Authorize(Roles = "Administrador")]`. La interfaz invocará únicamente `IParqueSolarService`; no accederá a `ApplicationDbContext` ni a Infrastructure.
- `ParqueSolarComponentBase` conserva el ámbito de `OwningComponentBase<IParqueSolarService>` por componente y propaga a su proveedor de autenticación la identidad y los cambios del circuito. Evita resolver un proveedor hijo sin inicializar o compartir el contexto de datos durante todo el circuito. Se retira la suscripción al disponer el componente.

## Verificación

- Ejecutar las pruebas unitarias de Domain, incluidas las nuevas pruebas de actualización de `ParqueSolar`.
- Ejecutar `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` para comprobar las reglas de arquitectura y nomenclatura. No se modificarán los tests estructurales para lograr su aprobación.
- Comprobar manualmente con un usuario Administrador el alta, listado, búsqueda, edición, baja y los mensajes; y con un usuario sin ese rol, la denegación de las operaciones restringidas.
