# Requerimientos: Estado de salud de paneles y alertas de falla

## Descripción General

Como usuario autenticado de SolarUY (Administrador, Tecnico o Consulta), quiero conocer el estado de salud de los paneles y nodos del parque solar, detectar fallas rapidamente y consultar los cambios de estado anteriores para dar seguimiento a problemas de generacion.

Esta funcionalidad corresponde a la **Entrega 3** y se integra con la recoleccion periodica de lecturas y la pantalla de monitoreo en tiempo real existentes. No debe iniciar una segunda recoleccion independiente.

## Criterios de Aceptacion

### 1. Estados y reglas de dominio

- El dominio debe contemplar los siguientes estados de salud de un panel: `Normal`, `Degradado`, `Falla` y `Desconectado`.
- La determinacion del estado debe centralizarse en reglas de dominio verificables mediante pruebas automatizadas.
- Las reglas deben poder identificar, como minimo, una potencia generada anormalmente baja frente a la potencia esperada, una falla explicitamente informada por el inversor y la ausencia de lecturas dentro de un plazo definido.
- No se debe clasificar un panel como fallado unicamente por generar poca potencia cuando las condiciones normales de operacion puedan justificarlo (por ejemplo, fuera del periodo de generacion). La definicion de potencia esperada y los umbrales de degradacion, falla y desconexion deben acordarse y ser configurables antes de implementarlos.
- Se debe distinguir el estado de salud calculado a partir de lecturas del estado operativo o administrativo que ya tenga la entidad `Panel`, evitando sobrescribir datos con significados distintos.

### 2. Indicadores en la pantalla de monitoreo

- La pantalla de monitoreo existente debe mostrar el estado de salud de cada panel, con un indicador visual y una etiqueta textual comprensible.
- Debe mostrar tambien un indicador por nodo, calculado a partir del estado de sus paneles segun una regla de agregacion documentada y probada.
- Un panel con falla simulada debe aparecer identificado en pantalla en pocos segundos desde que el sistema detecta y procesa la falla, sujeto al intervalo configurado de recoleccion.
- La informacion debe actualizarse automaticamente sin recargar la pagina, reutilizando el mecanismo SignalR del monitoreo existente.
- Cuando no haya datos suficientes o vigentes para determinar el estado, la interfaz no debe presentar datos anteriores como si fueran actuales.

### 3. Alertas activas

- El sistema debe presentar una lista de alertas activas asociadas a paneles con problemas detectados.
- Cada alerta debe identificar al menos el panel afectado, su nodo y parque actuales, el estado o tipo de problema y la fecha y hora UTC de deteccion.
- La lista debe actualizarse en tiempo real al detectar, cambiar o resolver una condicion de alerta.
- Se debe evitar generar alertas activas duplicadas por una misma condicion sostenida del mismo panel, incluso al reintentar un ciclo o ejecutar dos instancias.
- Una alerta deja de estar activa cuando desaparece la condicion que la origino, conservando la trazabilidad necesaria del cambio de estado.

### 4. Historico de cambios de estado

- El sistema debe registrar y permitir consultar el historial de transiciones de estado de salud de cada panel.
- Cada cambio registrado debe asociarse al panel y guardar el estado anterior, el estado nuevo y el instante del cambio en UTC.
- Las mediciones repetidas que mantengan el mismo estado no deben generar transiciones historicas duplicadas.
- El historial debe conservarse aunque el estado actual del panel cambie posteriormente.
- Debe poder visualizarse el historial desde la interfaz de monitoreo o desde un detalle accesible de cada panel.

### 5. Seguridad y permisos

- Los roles `Administrador`, `Tecnico` y `Consulta` pueden visualizar los estados, las alertas activas y el historial en modo solo lectura.
- El acceso debe requerir autenticacion y respetar el esquema existente de Keycloak.
- Esta funcionalidad no incorpora operaciones manuales de creacion, edicion o eliminacion de estados y alertas por parte de los usuarios.

## Reglas de Negocio

- El estado de salud resulta de la informacion de recoleccion y de reglas del dominio, no de una seleccion manual en la pantalla.
- Solo deben registrarse transiciones cuando el estado calculado sea diferente del ultimo estado persistido; deben contemplarse reintentos y procesamiento concurrente.
- Los instantes se manejan en UTC. El historico debe preservar el orden temporal y evitar que una lectura antigua revierta indebidamente un estado mas reciente.
- La condicion `Desconectado` debe reflejar la falta de comunicacion o de datos vigentes segun la regla aprobada; no debe confundirse automaticamente con potencia cero.
- Las reglas para determinar el estado de un nodo y la prioridad entre condiciones simultaneas deben quedar documentadas antes de su implementacion.
- Las alertas reflejan condiciones de falla vigentes; su cierre automatico no debe borrar el historico.
- La simulacion de fallas debe aprovechar las capacidades del inversor simulado ya disponibles en SolarUY.

## Restricciones Tecnicas e Integracion

- Reutilizar las entidades `Parque`, `Nodo`, `Panel`, las lecturas de panel y los servicios de recoleccion/monitoreo existentes, respetando los nombres y contratos reales del repositorio.
- Ubicar las reglas de clasificacion y transicion en **Domain**, la orquestacion y las consultas en **Application**, la persistencia en **Infrastructure** y los componentes visuales en **Presentation**.
- Utilizar MariaDB y EF Core para persistir los cambios de estado, mediante una migracion incremental que conserve los datos existentes.
- Reutilizar SignalR y Radzen en la pantalla de monitoreo existente, sin agregar servicios de infraestructura nuevos por esta funcionalidad.
- Mantener el funcionamiento previsto con dos instancias detras de Nginx. La distribucion de eventos entre instancias debe seguir el mecanismo aprobado para monitoreo; no elegir otro transporte sin coordinacion.
- Evitar modificar el comportamiento de la recoleccion historica o reemplazar su estado de salud existente sin una decision explicita de compatibilidad.

## Decisiones Pendientes Antes de Implementar

- Fuente y formula para calcular la potencia esperada (incluidas condiciones de operacion en las que una potencia baja es normal).
- Umbrales y precedencia de `Normal`, `Degradado`, `Falla` y `Desconectado`; tiempo maximo sin datos y tratamiento de lecturas incompletas.
- Regla de agregacion del estado de salud de los nodos.
- Correspondencia entre los estados de salud nuevos y los estados/enumeraciones existentes de `LecturaPanel` y `Panel`, incluyendo tratamiento de datos historicos.
- Politica de cierre/reactivacion de alertas ante fallas intermitentes, sin duplicaciones.

Estas decisiones deben cerrarse sin inventar valores fijos ni modificar retroactivamente datos que no permitan inferir su estado original.

## Pruebas y Entrega

- **Pruebas de dominio:** clasificacion de estados, limites de umbrales, prioridad entre condiciones, ausencia de datos, transiciones, agregacion por nodo y no duplicacion de transiciones.
- **Pruebas de integracion:** persistencia y consulta del historico, migracion incremental, alerta activa unica por condicion, cierre al recuperarse y concurrencia entre instancias.
- **Pruebas funcionales:** simular una falla de panel y comprobar que el indicador del panel, el del nodo y la lista de alertas se actualizan automaticamente; comprobar tambien la recuperacion y la consulta del historico.
- **Seguridad:** verificar el acceso de `Administrador`, `Tecnico` y `Consulta` como lectores autenticados.
- Documentar los cambios, las decisiones de reglas, los resultados de las pruebas y las verificaciones de interfaz o despliegue que no hayan podido ejecutarse.

## Fuera de Alcance

- Gestion manual de mantenimientos o asignacion de tecnicos.
- Notificaciones externas (correo, SMS o WhatsApp).
- Reemplazar la recoleccion periodica o implementar un segundo sistema de monitoreo.
- Incorporar Redis u otros servicios nuevos sin aprobacion del equipo.
