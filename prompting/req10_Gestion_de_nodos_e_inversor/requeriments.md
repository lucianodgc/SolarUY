# Gestión de nodos e inversores de un parque solar

## 1. Objetivo

Implementar la gestión de los nodos que componen un parque solar, incluyendo la información de sus inversores asociados, mediante una interfaz desarrollada con Radzen.

## 2. Funcionalidades

### 2.1. ABM de nodos

Los usuarios con rol **Administrador** pueden:

    - Crear un nodo dentro de un parque.
    - Consultar los nodos asociados a un parque.
    - Editar la información de un nodo.
    - Eliminar un nodo, siempre que el parque conserve al menos otro nodo.

### 2.2. Información del inversor

    - Cada nodo debe mostrar la información de su inversor asociado.
    - Debe visualizarse el estado del inversor y si el volcado de energía a la red está activo.
    - Los datos del inversor deben estar asociados correctamente al nodo correspondiente.

### 2.3. Interfaz de usuario

Desarrollar las pantallas utilizando **Radzen** para:

    - Consultar los nodos de un parque.
    - Crear y editar nodos mediante formularios.
    - Consultar la información del inversor asociado.
    - Confirmar la eliminación de un nodo.

### 2.4. Seguridad

    - Las operaciones de creación, edición y eliminación estarán disponibles exclusivamente para usuarios con rol Administrador.
    - Los usuarios sin ese rol no podrán ejecutar estas operaciones, aunque intenten acceder directamente a los endpoints o métodos protegidos.
    - Basarse en lo especificado en el requerimiento 9

## 3. Criterios de aceptación

    1. Un Administrador puede crear un nodo dentro de un parque.
    2. Un Administrador puede consultar y editar los nodos de un parque.
    3. Un Administrador puede eliminar un nodo si el parque conserva al menos otro nodo.
    4. El sistema rechaza la eliminación del último nodo de un parque.
    5. Cada nodo muestra correctamente su inversor asociado, incluyendo su estado y el estado del volcado a red.
    6. Los usuarios sin rol Administrador no pueden ejecutar operaciones de creación, edición ni eliminación.
    7. Las validaciones de dominio cuentan con pruebas automatizadas que verifican las reglas anteriores y se ejecutan correctamente.
    8. Las pantallas y formularios cumplen las funcionalidades definidas y muestran los errores de validación correspondientes.

## 4. Pruebas automatizadas

Incluir pruebas que verifiquen, como mínimo, los siguientes casos:

    - Crear un parque con un nodo válido.
    - Crear un nodo con una cantidad de paneles válida.
    - Rechazar datos inválidos de un nodo, según las reglas definidas.
    - Permitir eliminar un nodo cuando el parque tiene dos o más nodos.
    - Rechazar la eliminación del último nodo de un parque.
    - Verificar la asociación entre nodo e inversor.
    - Verificar las reglas de autorización en las pruebas correspondientes.

Las pruebas de dominio deben validar las invariantes del parque y del nodo. Las pruebas de autorización deben comprobar que solamente los usuarios con rol Administrador pueden realizar las operaciones protegidas.

## 5. Decisiones pendientes

Antes de comenzar la implementación, se deben definir los siguientes aspectos:

1. **Parámetros técnicos del nodo:** especificar qué parámetros deben almacenarse, por ejemplo, potencia instalada, orientación, inclinación o tensión, según los requisitos del proyecto.
2. **Gestión del inversor:** definir si el inversor se crea junto con el nodo o si se selecciona uno existente.
3. **Estados del inversor:** definir los estados operativos permitidos y sus reglas de transición.
4. **Eliminación de nodos:** definir qué sucede con el inversor asociado cuando se elimina un nodo.