# Gestión de paneles solares

## 1. Objetivo

Implementar la gestión de los paneles solares de un parque, registrando sus características, su estado y el nodo en el que están instalados.

La entidad Panel será utilizada como base para funcionalidades posteriores, como el mantenimiento y el monitoreo de los paneles.

## 2. Reglas de negocio

    1. Cada panel debe estar asociado a un único nodo.
    2. Cada panel debe registrar su modelo, potencia nominal, fecha de instalación y estado.
    3. La cantidad de paneles asociados a un nodo debe coincidir con la cantidad configurada para ese nodo.
    4. No se debe permitir una operación que provoque una discrepancia entre la cantidad de paneles registrados y la configuración del nodo.
    5. Las operaciones de alta, edición y baja de paneles estarán restringidas a usuarios con rol Administrador.

## 3. Funcionalidades

### 3.1. ABM de paneles

Los usuarios con rol **Administrador** pueden:

    - Dar de alta un panel y asociarlo a un nodo.
    - Consultar los paneles registrados.
    - Consultar el listado de paneles de un nodo específico.
    - Editar las características de un panel.
    - Dar de baja un panel, respetando las reglas de consistencia de cantidades.

### 3.2. Información del panel

Cada panel debe almacenar, como mínimo:

- **Identificador:** identificador único del panel.
- **Modelo:** modelo del panel solar.
- **Potencia nominal:** potencia nominal especificada por el fabricante.
- **Fecha de instalación:** fecha en la que se instaló el panel.
- **Estado:** estado actual del panel.
- **Nodo:** nodo en el que está instalado.

### 3.3. Interfaz de usuario

Desarrollar las pantallas utilizando Radzen para:

- Consultar el listado de paneles.
- Consultar los paneles agrupados o filtrados por nodo.
- Crear y editar paneles mediante formularios.
- Mostrar el nodo asociado a cada panel.
- Confirmar la baja de un panel.
- Mostrar los mensajes de validación cuando una operación no cumpla las reglas de negocio.

### 3.4. Consistencia de cantidades

El sistema debe validar que la cantidad de paneles registrados en un nodo coincida con la cantidad configurada en dicho nodo.

La validación debe contemplar las operaciones de alta, edición y baja, así como los cambios de asociación entre nodos, si estos están permitidos.

Si una operación provoca una discrepancia, el sistema debe rechazarla o gestionar la actualización de la configuración del nodo de forma controlada, según la regla de negocio que se defina.

## 4. Criterios de aceptación

1. Un Administrador puede dar de alta un panel y asociarlo a un nodo.
2. Un Administrador puede consultar y editar los paneles registrados.
3. Un Administrador puede dar de baja un panel respetando las reglas de consistencia.
4. Cada panel muestra correctamente el nodo en el que está instalado.
5. El sistema permite consultar el listado de paneles de un nodo específico.
6. El sistema impide que una operación genere una discrepancia entre la cantidad de paneles registrados y la cantidad configurada en el nodo.
7. Los usuarios sin rol Administrador no pueden realizar operaciones de alta, edición ni baja.
8. Las reglas de dominio cuentan con pruebas automatizadas que se ejecutan correctamente.

## 5. Modelo de dominio sugerido

### Panel

- Identificador.
- Modelo.
- Potencia nominal.
- Fecha de instalación.
- Estado.
- Nodo al que pertenece.

### Relación con Nodo

- Cada panel pertenece a un único nodo.
- Un nodo puede tener varios paneles.
- La cantidad de paneles asociados debe coincidir con la cantidad configurada en el nodo.

## 6. Pruebas automatizadas

Incluir pruebas que verifiquen, como mínimo, los siguientes casos:

- Crear un panel con datos válidos.
- Rechazar un panel con datos inválidos.
- Asociar correctamente un panel a un nodo.
- Consultar los paneles asociados a un nodo.
- Editar las características de un panel.
- Dar de baja un panel respetando la consistencia de cantidades.
- Rechazar operaciones que generen discrepancias entre los paneles registrados y la configuración del nodo.
- Verificar las reglas de autorización para las operaciones protegidas.

Las pruebas de dominio deben comprobar las reglas de negocio y las invariantes correspondientes. Las pruebas de autorización deben verificar que solo los usuarios con rol Administrador puedan ejecutar las operaciones protegidas.

## 7. Decisiones pendientes

Antes de comenzar la implementación, se deben definir los siguientes aspectos:

1. **Cantidad configurada en el nodo:** determinar si se trata de la cantidad máxima permitida o de la cantidad exacta que debe existir.
2. **Cambios de asociación:** definir si un panel puede trasladarse de un nodo a otro y qué validaciones deben aplicarse.
3. **Estados del panel:** especificar los estados permitidos y las condiciones para cambiar de uno a otro.
4. **Baja de paneles:** definir si se permite la eliminación física o si debe utilizarse una baja lógica para conservar el historial.
5. **Potencia nominal:** definir la unidad de medida, por ejemplo, vatios pico (Wp).