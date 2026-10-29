# Requerimientos: Monitoreo en tiempo real con SignalR

## Descripción General

Como usuario autenticado del sistema SolarUY (Administrador, Tecnico o Consulta), quiero visualizar en una pantalla de monitoreo la potencia generada por un parque solar, sus nodos y sus paneles, con actualizaciones automaticas, para conocer el estado de generacion sin tener que recargar la pagina.

Este requerimiento pertenece a la Entrega 2 y se apoya en la recoleccion periodica y el almacenamiento de lecturas ya previstos para el sistema.

## Criterios de Aceptacion

### 1. Pantalla de monitoreo

- El sistema debe mostrar la potencia generada actual del parque seleccionado.
- Debe permitir visualizar la potencia generada por cada nodo perteneciente al parque.
- Debe permitir visualizar la potencia generada por cada panel perteneciente a cada nodo.
- La informacion debe identificar claramente el parque, los nodos y los paneles a los que corresponde.
- Debe indicarse la fecha y hora UTC de las lecturas utilizadas y distinguir los casos en los que aun no existan datos disponibles.
- Los valores deben expresarse con unidades consistentes (por ejemplo, W o kW), indicando claramente la unidad presentada.

### 2. Actualizacion automatica mediante SignalR

- La pantalla debe recibir actualizaciones de nuevas lecturas sin recargar la pagina.
- Se debe disponer de un hub de SignalR para notificar a los clientes conectados cuando existan nuevas lecturas persistidas.
- Al ingresar a la pantalla, el usuario debe recibir un estado inicial consultado desde la fuente de datos, sin depender de esperar a la siguiente lectura.
- Una nueva lectura debe actualizar el valor del panel correspondiente y los agregados de su nodo y parque.
- El monitoreo debe gestionar la desconexion y reconexion del cliente, recuperando el estado vigente luego de reconectar.
- No se deben presentar como actuales lecturas antiguas sin identificar su fecha y hora.

### 3. Grafico en vivo con Radzen

- Incluir al menos un grafico sencillo de Radzen que represente la evolucion temporal de la generacion.
- El grafico debe actualizarse automaticamente cuando se reciban nuevas lecturas.
- El eje temporal debe utilizar las fechas y horas de las lecturas, conservando el significado de sus instantes UTC.
- El grafico debe contar con etiquetas y unidades que permitan interpretar los valores.
- La cantidad de puntos conservados en pantalla debe estar acotada para evitar un crecimiento indefinido del consumo de memoria del cliente.

### 4. Seguridad y acceso

- La pantalla de monitoreo debe ser accesible para usuarios autenticados con los roles `Administrador`, `Tecnico` y `Consulta`.
- El monitoreo es exclusivamente de lectura: ninguno de estos roles puede modificar lecturas, paneles, nodos ni parques desde esta funcionalidad.
- La autorizacion debe aplicarse tambien a las consultas y a la conexion al hub, sin depender solamente de ocultar controles en la interfaz.

### 5. Funcionamiento con Nginx y dos instancias

- El monitoreo debe funcionar cuando la aplicacion se ejecute detras de Nginx con dos instancias.
- Nginx debe permitir las conexiones y actualizaciones necesarias para SignalR, incluidas las conexiones WebSocket cuando se utilicen.
- Una lectura generada o persistida por una instancia debe llegar a los clientes conectados a cualquiera de las dos instancias; un hub local aislado por instancia no es suficiente.
- La solucion de distribucion de eventos y la configuracion del balanceador deben coordinarse con la tarea de alta disponibilidad de Luciano.
- La reconexion de un cliente debe permitir recuperar los valores vigentes aunque la nueva conexion quede atendida por otra instancia.

## Reglas de Negocio

- La fuente de verdad de los valores mostrados son las lecturas almacenadas por el mecanismo de recoleccion periodica; SignalR transporta notificaciones y no reemplaza la persistencia.
- La potencia de un panel se obtiene de su ultima lectura valida disponible.
- La potencia de un nodo se calcula a partir de las lecturas vigentes de sus paneles; la potencia del parque se calcula a partir de sus nodos, evitando sumar dos veces un mismo panel.
- Para agregaciones en tiempo real, se debe definir y aplicar un mismo criterio de vigencia temporal para evitar combinar silenciosamente lecturas de intervalos incompatibles. Los paneles sin lectura vigente deben identificarse y no deben interpretarse automaticamente como generacion cero.
- Las fechas y horas se manejan internamente en UTC. La presentacion en horario local, si se implementa, debe indicarlo expresamente.
- Las actualizaciones no deben provocar que una lectura mas antigua sustituya un valor mas reciente.
- La pantalla no debe generar nuevas lecturas ni consultar directamente los inversores: consume la informacion producida por el proceso de recoleccion existente.

## Restricciones Tecnicas e Integracion

- Mantener la arquitectura y las tecnologias aprobadas para SolarUY: aplicacion .NET/Blazor, SignalR, Radzen, EF Core y MariaDB.
- Reutilizar las entidades y relaciones existentes de Parque, Nodo, Panel y LecturaPanel, respetando sus nombres y el modelo vigente.
- Separar la obtencion y agregacion de lecturas de la interfaz Blazor y del mecanismo de notificacion SignalR.
- Emitir notificaciones solo para lecturas que se hayan guardado correctamente, evitando mostrar eventos no confirmados como si estuvieran persistidos.
- Evitar una segunda recoleccion en la capa de monitoreo.
- No dar por resuelto el escenario de dos instancias solo por habilitar WebSockets o afinidad de sesion: debe comprobarse una forma efectiva de distribuir las actualizaciones entre instancias.
- La tecnologia concreta de coordinacion entre instancias debe acordarse con la implementacion de alta disponibilidad; no introducir una infraestructura paralela sin esa coordinacion.
- Los cambios de configuracion deben externalizarse y no deben incluir secretos en archivos versionados.

## Pruebas y Entrega

- Comprobar que la pantalla presenta el estado inicial del parque, nodos y paneles a partir de las lecturas almacenadas.
- Verificar que los valores y el grafico se actualizan automaticamente al persistir nuevas lecturas, sin recargar la pagina.
- Verificar los calculos agregados, evitando duplicados y sin confundir ausencia de datos con potencia cero.
- Verificar el tratamiento de lecturas atrasadas, desconexiones y reconexiones.
- Comprobar que Administrador, Tecnico y Consulta pueden consultar y que ninguna operacion de escritura se expone mediante esta funcionalidad.
- Probar Nginx con dos instancias y clientes conectados a instancias diferentes: ambos deben recibir los cambios producidos por cualquiera de ellas.
- Mantener los tests de dominio existentes en verde e incorporar pruebas de los calculos y componentes nuevos que correspondan.
- Documentar los cambios realizados, la configuracion necesaria y los resultados de las pruebas, indicando expresamente cualquier comprobacion operativa no ejecutada.

## Fuera de Alcance

- Alta, modificacion o eliminacion de parques, nodos y paneles.
- Creacion de un segundo proceso de recoleccion o modificacion de las reglas de adquisicion de lecturas, salvo ajustes de integracion imprescindibles.
- Informes historicos avanzados, exportaciones y alertas automaticas; este requerimiento contempla solo el monitoreo actual y un grafico simple en vivo.
