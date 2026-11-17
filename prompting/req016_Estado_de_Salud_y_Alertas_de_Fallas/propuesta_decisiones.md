# Decisiones de req016 — D1–D5 confirmadas; D6 autorizada e implementada

El usuario confirmó D1–D5. Inicialmente solicitó solo preparar D6; posteriormente pidió completar los pendientes y respondió «hazlo lo mejor posible», autorizando avanzar con la solución propuesta. D6 está implementada y verificada técnicamente; la coordinación se cerró el 17/11/2026 mediante la validación y delegación de Luciano comunicadas por el usuario; ver `coordinacion_despliegue.md`.

## D1. Potencia esperada

Para el simulador actual, usar `PotenciaNominalW × FactorEsperado`, con factor inicial propuesto 1. Evaluar potencia baja solamente para paneles administrativamente activos, con lectura válida y una ventana de generación habilitada/configurada explícitamente. No activar por defecto la clasificación por potencia baja sin conocer esa ventana y la condición de operación del inversor: las lecturas históricas actuales no prueban si estaba apagado o si el panel estaba desactivado en el simulador.

Propuesta: dejar esa clasificación deshabilitada inicialmente y habilitarla por configuración cuando se disponga de esas condiciones. Mientras tanto, seguir detectando falla explícita, desconexión y señal sana; mostrar el motivo y que la comprobación de potencia esperada no está habilitada. No añadir fuentes meteorológicas ni modificar el significado de la lectura histórica.

## D2. Umbrales y precedencia

- Umbrales propuestos, configurables para cuando se habilite D1: potencia inferior al 50 % de la esperada = Falla; desde el 50 % e inferior al 80 % = Degradado; desde el 80 % = Normal.
- Una lectura vence al alcanzar tres intervalos de recolección sin datos, configurable. Con el intervalo actual de 30 segundos, corresponde a 90 segundos. Un panel sin primera muestra espera ese mismo plazo desde la primera observación persistida del evaluador, sin inventar una fecha de recepción anterior.
- Precedencia: falta de lectura vigente = Desconectado; con lectura vigente, `Fallado` = Falla; luego potencia baja si D1 está habilitada; finalmente `Sano` = Normal. `Desconocido`, procedencia no demostrable y muestras futuras producen evidencia insuficiente, sin inventar Normal.
- Evaluación periódica cada 5 segundos, configurable, más el procesamiento posterior al commit. Sin persistencia adicional de condición/histéresis en esta versión: una evaluación suficiente confirma el cambio.

## D3. Estado del nodo

Propuesta: prioridad Falla > Desconectado > Degradado > Normal entre estados suficientes de sus paneles actuales. Informar cobertura y advertir insuficiencia si faltan paneles evaluables. Si no hay estados suficientes o el nodo está vacío, mostrar «Sin datos suficientes», sin crear otro estado de negocio.

## D4. Compatibilidad, arranque y traslados

- No cambiar `EstadoPanel` ni `EstadoSaludLectura`, sus valores históricos o el contrato de inversor. El enum de salud calculada vive en `Domain.Salud`.
- La primera evaluación suficiente crea un cambio con estado anterior nulo; fecha de cambio y detección iguales al instante UTC de evaluación, conservando separadamente la fecha de evidencia. No reconstruir episodios previos al arranque de la funcionalidad.
- Usar únicamente lecturas posteriores a la instalación y con `NodoId` histórico demostrado igual al nodo actual. Un traslado invalida evidencia del nodo anterior; esperar nuevas lecturas sin reasignar las anteriores. Mostrar las alertas con la topología actual.
- Evidencia insuficiente no cierra automáticamente una alerta activa: mostrarla como última condición confirmada sin vigencia demostrada, hasta una evaluación suficiente o desconexión según D2.

## D5. Alertas

Una única alerta activa por panel, correspondiente a su condición prioritaria no Normal. Una condición sostenida mantiene su episodio. Al cambiar entre condiciones de problema, cerrar el episodio previo y abrir otro en la misma transacción. Al volver a Normal, cerrar. Una recurrencia abre un episodio nuevo, sin reabrir ni borrar los cerrados. Cambiar política requiere reiniciar con configuración coherente en ambas instancias; solo una evaluación suficiente distinta produce transición, conservando el motivo.

## D6. Dos instancias — autorizada e implementada

El usuario autorizó avanzar con MariaDB existente después de solicitar completar los pendientes y responder «hazlo lo mejor posible». Se implementó y verificó la solución técnica descrita aquí; la coordinación se cerró el 17/11/2026 mediante la validación y delegación de Luciano comunicadas por el usuario; ver `coordinacion_despliegue.md`.

1. Cada instancia ejecuta ObservacionSaludWorker con identificador y período configurables. IObservacionSaludRepository/ObservacionSaludRepository leen dos conjuntos dentro de una transacción RepeatableRead: parques, incluidos vacíos, y firmas de los paneles con su salud/topología actual. No consultan inversores ni escriben salud.
2. El seguimiento en memoria es por panel: versión, instante de evaluación, nodo evaluado, suficiencia y pertenencia actual. No se usa MAX global ni cursor temporal. Una modificación invalida los grupos de parques anterior y actual; una retirada invalida el anterior y libera su entrada.
3. Al arrancar o reiniciar, invalidar todos los parques actuales permite recuperar también una selección que quedó vacía durante una caída. Ese aviso de parque completo tiene PanelId nulo. La carga inicial y la reconexión consultan salud, alertas e historial abierto desde MariaDB.
4. El seguimiento avanza únicamente después del envío local completo. Ante error o cancelación del transporte sin detener el host, el snapshot se reintenta. Un reinicio reconstruye el seguimiento. Puede haber avisos repetidos; no se promete entrega exactamente una vez ni reproducción individual de cada invalidación intermedia. Todas las transiciones permanecen consultables en el historial.
5. La memoria es proporcional al inventario actual y se liberan entradas retiradas. No se reinstalaron tablas de transporte de req014 ni se agregó Redis u otro servicio. Se midió un promedio local de 12,16 ms por snapshot de 1004 paneles en cinco lecturas; no es un límite contractual ni una medición de producción.
6. SaludPanel:Habilitada permite un proceso que solo observe salud. El proceso receptor se probó sin adquisición ni evaluación local; por tanto, su cliente no depende de volver a procesar las lecturas. La evaluación concurrente mantiene la serialización transaccional existente.
7. V8 se ejecutó con dos procesos reales detrás del Nginx del proyecto, claves compartidas y clientes en instancias diferentes: fuente A o B, falla, recuperación, cierre de alertas, reemplazo y reconexión. La prueba de navegador también conservó el parque de la URL y mantuvo conectado al cliente de la instancia restante. Las pruebas de concurrencia de persistencia se mantuvieron separadas.

La entrega converge al estado vigente. Si el equipo necesita reproducir individualmente cada aviso a cada cliente, deberá acordar ese alcance distinto; el historial actual ya conserva las transiciones. La coordinación de la elección técnica quedó cerrada por delegación comunicada el 17/11/2026. La aplicación del despliegue real y la verificación de certificados siguen a cargo del operador y no se presentan como realizadas; ver `coordinacion_despliegue.md`, deploy/nginx/README.md e implementations.md.
