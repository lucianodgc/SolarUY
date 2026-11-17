# Requerimientos: Histórico de generación por día, mes y año

## Descripción General

Como usuario del sistema SolarUY, quiero consultar el historial de generación de energía de un parque solar, nodo o panel, para conocer la energía generada durante un día, mes o año determinado, comparar períodos y analizar el rendimiento de las instalaciones.

## Criterios de Aceptación

1. **Consultas históricas:** El sistema debe permitir consultar la generación de energía por día, mes y año, seleccionando un parque, nodo o panel específico.
2. **Rangos de fechas:** Permitir consultar y comparar la generación entre diferentes períodos de tiempo.
3. **Máximos y mínimos:** Mostrar los valores máximos y mínimos de generación dentro del período seleccionado.
4. **Consultas eficientes:** Los cálculos deben realizarse mediante consultas agregadas sobre la entidad `Lectura` directamente en la base de datos, evitando cargar todos los registros en memoria.
5. **Servicios reutilizables:** Implementar los servicios de consulta en la capa `Application`, permitiendo que otras funcionalidades, como el chatbot, puedan utilizarlos.
6. **Visualización:** Mostrar los resultados históricos de manera clara, permitiendo seleccionar el período y el parque, nodo o panel correspondiente.
7. **Pruebas:** Verificar los resultados utilizando datos de prueba conocidos y comprobar el rendimiento con grandes cantidades de lecturas.
8. **Recolección periódica:** Guardar automáticamente mediciones de los paneles registrados para alimentar el histórico, usando la integración del simulador actual y un único recolector efectivo entre instancias. Ampliación solicitada el 22/10/2026.

## Reglas de Negocio

- La generación histórica debe calcularse únicamente a partir de las lecturas correspondientes al parque, nodo o panel seleccionado.
- La generación de un nodo debe contemplar sus paneles, y la de un parque debe contemplar sus nodos.
- Los resultados deben expresarse utilizando unidades de energía coherentes, como kWh.
- Los períodos consultados deben ser válidos y las fechas de inicio no pueden ser posteriores a las fechas de fin.
- Las consultas no deben modificar los registros históricos existentes.
- La implementación debe respetar la arquitectura actual de SolarUY y permitir reutilizar los servicios desde futuras funcionalidades.

## Pruebas

Se deben incluir tests que verifiquen como mínimo:

- Consulta de generación diaria, mensual y anual.
- Consulta por parque, nodo y panel.
- Comparación entre rangos de fechas.
- Obtención correcta de máximos y mínimos.
- Resultados correctos con datos de prueba conocidos.
- Rendimiento adecuado con grandes volúmenes de lecturas.
- Tests existentes y nuevos ejecutándose correctamente.
