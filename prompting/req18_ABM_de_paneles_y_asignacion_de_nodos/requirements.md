# Requerimiento: Administración de paneles solares

## Objetivo

Permitir la administración de los paneles solares instalados en los nodos de los parques solares, manteniendo sus características y su asociación con el nodo correspondiente.

Esta entidad será utilizada posteriormente por las funcionalidades de mantenimiento y monitoreo.

## Requerimientos funcionales

1. **Registro de paneles:** permitir registrar paneles con los siguientes atributos:
   - Modelo.
   - Potencia nominal.
   - Fecha de instalación.
   - Estado.

2. **Asociación con nodos:** cada panel debe estar asociado a un nodo existente. Debe ser posible identificar en qué nodo está instalado cada panel.

3. **Listado de paneles:** permitir consultar los paneles registrados y visualizar a qué nodo pertenece cada uno.

4. **Listado por nodo:** permitir consultar los paneles asociados a un nodo específico.

5. **Edición de paneles:** permitir modificar las características de un panel y, si las reglas de negocio lo permiten, cambiar su asociación con otro nodo.

6. **Baja de paneles:** permitir dar de baja un panel respetando las reglas de negocio y las relaciones existentes con otras entidades.

7. **Validaciones:** validar los datos de los paneles, la existencia del nodo asociado y las reglas de cantidad de paneles configuradas para cada nodo.

8. **Autorización:** restringir las operaciones de alta, edición y baja al rol Administrador.

9. **Mensajes al usuario:** mostrar mensajes claros para operaciones exitosas y errores de validación o de negocio.

## Reglas de negocio

- Un panel debe pertenecer a un nodo existente.
- La cantidad de paneles asociados a un nodo debe respetar la cantidad establecida en su configuración.
- Las operaciones sobre paneles no deben modificar accidentalmente los datos de otros paneles ni los de otros nodos.
- Las reglas de baja deben considerar las relaciones del panel con las funcionalidades futuras de mantenimiento y monitoreo.

## Criterios de aceptación

- El Administrador puede realizar el alta, edición y baja de paneles.
- Se puede consultar el listado general de paneles y el listado de paneles de un nodo específico.
- Se visualiza correctamente el nodo al que pertenece cada panel.
- Se valida la cantidad de paneles asociada a cada nodo conforme a su configuración.
- Los usuarios que no tengan el rol Administrador no pueden ejecutar las operaciones restringidas.
- Los tests de dominio terminan correctamente.

## Restricciones y decisiones tecnológicas

- Respetar la arquitectura Clean Architecture existente en SolarUY.
- Reutilizar las entidades, contratos, servicios y componentes existentes cuando corresponda.
- Respetar las tecnologías y versiones aprobadas por el equipo.
- No introducir dependencias, patrones ni tecnologías nuevas sin justificación y autorización.
- No duplicar funcionalidades existentes.

## Decisiones confirmadas

- `CantidadPanelesPrevista` es una capacidad máxima; un valor `null` significa que el nodo no tiene límite configurado.
- Los atributos de texto son obligatorios, la potencia nominal debe ser positiva y la fecha de instalación se almacena en UTC.
- Los estados son `Activo`, `Inactivo` y `Falla`; todas las transiciones entre estos estados están permitidas.
- Se permite cambiar un panel de nodo si el destino existe y tiene capacidad disponible. El panel deja de contabilizarse en el origen y conserva su historial de lecturas y mantenimiento.
- La baja es lógica; se conserva el panel y sus relaciones históricas, pero deja de participar en listados operativos, inventario y recolección de lecturas.
