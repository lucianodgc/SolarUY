# Reglas de Arquitectura y Despliegue de SolarUY

Estas reglas describen la arquitectura de ejecución de SolarUY. Al crear o modificar componentes, integraciones o configuración de despliegue, se deben respetar las siguientes decisiones:

## 1. Aplicación y capas
- SolarUY se ejecuta como una aplicación Blazor Server, con componentes de interfaz construidos con Blazor y Radzen.
- Cada instancia de la aplicación contiene las capas `Presentation`, `Application`, `Domain` e `Infrastructure`.
- Las dependencias entre capas apuntan hacia `Domain`, de acuerdo con Clean Architecture. `Presentation` invoca casos de uso de `Application`; `Infrastructure` implementa los detalles de persistencia e integraciones externas.
- El sistema debe poder ejecutarse en dos o más instancias de la aplicación. Las instancias atienden la interfaz y los casos de uso; no deben duplicar trabajos exclusivos de recolección de datos.

## 2. Tráfico y sesiones
- El tráfico del navegador llega a las instancias a través de Nginx, que termina HTTP/HTTPS y distribuye las solicitudes.
- Nginx debe admitir WebSocket para las conexiones SignalR de Blazor Server y mantener afinidad de sesión, de modo que las solicitudes de una conexión persistente lleguen a la instancia que la atiende.
- El despliegue debe permitir reemplazar una instancia por vez mientras otra continúa atendiendo solicitudes.

## 3. Servicios e integraciones externas
- La autenticación de usuarios se integra con Keycloak mediante OpenID Connect.
- MariaDB es la base de datos de la aplicación. El acceso y la persistencia se implementan en `Infrastructure`.
- La comunicación con el simulador del inversor se realiza desde la aplicación mediante una integración encapsulada en `Infrastructure`.
- Solo una instancia de la aplicación, o un worker dedicado, debe leer el simulador y guardar sus lecturas. No se debe ejecutar ese trabajo concurrentemente en todas las instancias, para evitar duplicar datos.
- Las consultas al modelo de IA se realizan mediante una integración encapsulada. El flujo del chatbot es de solo lectura y debe respetar los permisos del usuario autenticado; no puede ejecutar órdenes ni modificar datos.

## 4. Responsabilidad del chatbot
- El panel de chat es exclusivamente para consultas.
- Las consultas deben limitarse a la información que el usuario autenticado está autorizado a leer.
- El modelo de IA es un servicio externo: no debe recibir acceso directo a la base de datos ni credenciales que permitan omitir la autorización de la aplicación.

