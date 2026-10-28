# Especificación: Autorización por roles para la gestión solar

## Objetivo y alcance

Aplicar los permisos definidos en `requirements.md` tanto en la navegación/rutas de Blazor como en los servicios de Application. Se reutiliza la identidad y los roles que ya proporciona `IUsuarioActual`; no se agregan proveedores de identidad, bibliotecas ni mecanismos paralelos de autorización.

Los nombres de rol son exactamente `Administrador`, `Tecnico` y `Consulta`. La autorización de cada operación se evalúa con los roles del usuario autenticado en la solicitud. Las reglas funcionales propias del mantenimiento y del inversor siguen perteneciendo a sus requisitos específicos.

## Domain

- No se requiere modificar entidades ni reglas del dominio: los roles determinan el acceso a casos de uso y no alteran invariantes de parques, nodos, paneles, mantenimiento o inversores.
- No se agregarán referencias de autenticación o presentación al proyecto Domain.

## Application

- Los servicios que realizan operaciones cubiertas obtienen la identidad mediante `IUsuarioActual` y `UsuarioActualDto`, definidos en `SolarUY.Application.Abstractions`.
- Las comprobaciones se realizan dentro de cada operación del servicio antes de leer o modificar recursos, invocar integraciones o persistir cambios. Una llamada directa al servicio queda sujeta a la misma autorización que una llamada desde Blazor.
- Los permisos para operaciones existentes quedan así:

| Operación | Administrador | Técnico | Consulta |
|---|---:|---:|---:|
| Crear, editar o eliminar parques, nodos y paneles | Sí | No | No |
| Consultar parques, nodos y paneles | Sí | Sí | Sí |
| Gestionar mantenimiento conforme a `req004_Mantenimiento_de_paneles` | Sí | Sí, limitado a sus tareas según ese requisito | No |
| Consultar estado del inversor | Sí | Sí | Sí |
| Emitir órdenes al inversor | Sí | No | No |

- La fila de consulta de datos refleja el permiso de solo lectura solicitado. Las consultas de mantenimiento mantienen el alcance ya definido en `req004_Mantenimiento_de_paneles`: Administrador consulta todas las tareas y Técnico solo las asignadas; Consulta no accede al mantenimiento.
- Los servicios rechazan usuarios no autenticados, sin identidad válida o sin rol permitido, usando la denegación existente del servicio (`UnauthorizedAccessException` u otra excepción de autorización ya definida por el caso de uso). La capa de presentación no debe traducir una denegación en éxito ni continuar la operación.
- En los servicios existentes, se completa o corrige la matriz donde corresponda: `ParqueSolarService` actualmente exige Administrador también para consultas; el simulador de inversor ya separa consulta de órdenes; `TareaMantenimientoService` ya restringe las tareas a Administrador y Técnico. Se conservan sus reglas específicas de alcance.
- Para operaciones de nodos y paneles, aplicar las mismas comprobaciones a los casos de uso CRUD que existan en el código. No se define aquí el modelo, los datos, ni la creación de funcionalidades de nodos/paneles que no existan; la identificación del punto de entrada real debe confirmarse durante la implementación.

## Infrastructure

- No se requieren cambios en persistencia, esquema de base de datos, Keycloak ni integración con el inversor para autorizar operaciones.
- Infrastructure no implementa decisiones de permisos ni consulta directamente claims; Application decide usando `IUsuarioActual`.
- No se agregan paquetes ni dependencias. La composición de servicios y la adaptación de claims existentes se mantienen en Presentation.

## Presentation

- Proteger cada ruta o componente que exponga una operación con `[Authorize(Roles = "...")]` y usar `AuthorizeView` con los mismos roles para ocultar acciones no disponibles.
- Administrador: rutas y acciones de escritura de parques, nodos, paneles y órdenes al inversor.
- Administrador y Técnico: acceso a mantenimiento según las rutas y operaciones ya especificadas para mantenimiento.
- Administrador, Técnico y Consulta: pantallas de consulta de solo lectura para datos generales e inversor, conforme a las operaciones disponibles.
- Mostrar la vista de acceso denegado ya existente cuando el usuario autenticado no tenga el rol de una ruta. Mantener el inicio de sesión para usuarios no autenticados según la configuración OIDC actual.
- La visibilidad de botones y enlaces es una ayuda de interfaz; nunca reemplaza la autorización del servicio.
- Revisar las páginas existentes (`ParquesSolares`, alta/edición de parque, mantenimiento y simulador) para que rutas, acciones y servicios coincidan con la matriz. Proteger del mismo modo las pantallas CRUD de nodos/paneles si están presentes.

## Verificación contemplada

- Pruebas de Application por operación para Administrador, Técnico, Consulta, usuario no autenticado y usuario sin rol; comprobar que los rechazos ocurren antes de efectos de persistencia o integración.
- Pruebas de presentación/configuración de rutas para confirmar el acceso permitido y la denegación para cada rol.
- Incluir casos de lectura de datos generales, escritura administrativa, órdenes al inversor y límites de acceso al mantenimiento.
- No se prescribe una tecnología de pruebas nueva. Usar los proyectos y mecanismos de pruebas existentes.
