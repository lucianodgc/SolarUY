# Requerimiento: Gestión multiparque con configuraciones independientes

## Objetivo

Permitir que los usuarios con rol Administrador gestionen los parques solares existentes, manteniendo una configuración independiente para cada parque.

## Requerimientos
    - Listado de parques: permitir al administrador consultar los parques solares registrados y buscar parques mediante un criterio de búsqueda apropiado.
    - Alta de parques: permitir registrar nuevos parques solares, reutilizando la funcionalidad existente de alta cuando corresponda.
    - Edición de parques: permitir modificar los datos y la configuración de un parque existente.
    - Baja de parques: permitir dar de baja un parque, respetando las reglas de integridad y las dependencias existentes.
    - Configuración independiente: cada parque debe mantener sus propios datos de ubicación, capacidad y parámetros de configuración. Los cambios en un parque no deben modificar la configuración de otros.
    - Validaciones: validar los datos de entrada y las reglas de negocio antes de guardar los cambios.
    - Mensajes al usuario: mostrar mensajes claros para operaciones exitosas y errores de validación o de negocio.
    - Autorización: restringir las operaciones de gestión de parques al rol Administrador.

## Criterios de aceptación
    - El administrador puede realizar el ABM completo de parques: alta, baja, modificación y consulta, incluyendo listado y búsqueda.
    - Cada parque conserva su propia ubicación, capacidad y parámetros de configuración.
    - Dos o más parques pueden coexistir con configuraciones diferentes sin interferencias entre ellos.
    - Los usuarios que no tengan el rol Administrador no pueden ejecutar las operaciones restringidas.
    - Las validaciones y los mensajes de error y éxito son claros.
    - Los tests de dominio y de arquitectura terminan correctamente.