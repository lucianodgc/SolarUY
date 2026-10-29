# Requerimientos: Simulador de inversor remoto

## Descripción

Como usuario de SolarUY, quiero consultar y controlar un inversor simulado que representa el sensor y el inversor del esquema de referencia del profesor, para probar el flujo entre el panel de control y un dispositivo remoto sin necesitar una placa física.

El simulador debe generar valores aleatorios de potencia y responder a órdenes de encendido y apagado. El panel de control de SolarUY se comunicará con el simulador por TCP/IP, como muestra el esquema. El simulador será software ejecutable en los equipos de desarrollo; no se requiere una placa ARM, un Blackberry ni un sensor físico.

## Alcance

- El panel de control existente en SolarUY consulta valores del simulador y permite enviar las órdenes previstas.
- El simulador representa el extremo remoto que contiene el sensor y el inversor.
- La comunicación entre SolarUY y el simulador usa TCP/IP.
- Los valores de potencia se generan aleatoriamente y respetan el estado de encendido/apagado.
- No se requiere integrar ni desplegar hardware real.

## Criterios de aceptación

### Comunicación y lecturas

- SolarUY puede conectarse al simulador por TCP/IP y solicitar el estado y la potencia generada.
- El simulador genera lecturas de potencia aleatorias dentro de límites válidos definidos para la simulación.
- Una nueva lectura puede variar respecto de la anterior sin necesidad de reiniciar SolarUY.
- La respuesta permite identificar el estado actual del inversor junto con la lectura.
- La interfaz presenta los datos recibidos e indica que son simulados.

### Órdenes

- Un usuario Administrador puede enviar las órdenes de prender y apagar el inversor.
- El simulador confirma el estado resultante y las consultas siguientes muestran ese estado.
- Cuando el inversor está apagado, no reporta potencia generada.
- Los roles Técnico y Consulta pueden ver las lecturas y el estado, pero no enviar órdenes.
- La autorización se valida en el servicio de Application, además de proteger los controles de la interfaz.

### Errores

- Si el simulador no está disponible o devuelve una respuesta inválida, SolarUY muestra un error controlado y no presenta esa respuesta como una lectura válida.
- El fallo de comunicación no debe detener el resto de la aplicación.

### Verificación

- Pruebas verifican generación y límites de lecturas, órdenes de prender/apagar y consistencia del estado.
- Pruebas verifican los permisos de Administrador, Técnico y Consulta en la interfaz y en el servicio.
- Pruebas verifican el comportamiento cuando el simulador está desconectado o responde con datos inválidos.

## Fuera de alcance

- Integración con un inversor, sensor o placa física.
- Despliegue obligatorio en ARM o ejecución WebAssembly.
- Cambios a los datos persistidos de parques, nodos o paneles.
