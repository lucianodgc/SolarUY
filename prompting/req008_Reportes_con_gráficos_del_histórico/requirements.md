# Requerimientos: Reportes con gráficos del histórico (Radzen) y exportación

## Descripción General

Como usuario del sistema SolarUY, quiero visualizar reportes gráficos de la generación histórica de energía y consultar los mantenimientos realizados, para analizar el rendimiento de las instalaciones durante diferentes períodos y exportar la información.

## Criterios de Aceptación

1. **Pantalla de reportes:** El sistema debe disponer de una pantalla que permita visualizar la generación histórica mediante gráficos de barras y líneas utilizando componentes Radzen.
2. **Visualización histórica:** Los gráficos deben mostrar la generación de energía por día, mes y año, reutilizando los servicios de consulta del histórico existentes en `Application`.
3. **Filtros:** Permitir filtrar la información por parque solar, nodo y rango de fechas.
4. **Actualización de gráficos:** Los gráficos y resultados deben actualizarse correctamente al modificar los filtros seleccionados.
5. **Exportación CSV:** Permitir descargar los datos del reporte en formato CSV, respetando los filtros y períodos seleccionados.
6. **Resumen de mantenimiento:** Mostrar los mantenimientos realizados durante el período seleccionado, reutilizando la información y funcionalidades existentes de la card de mantenimiento.
7. **Consistencia:** Los valores representados en los gráficos deben coincidir con los resultados obtenidos de las consultas históricas.
8. **Autorización:** Los usuarios con roles `Administrador`, `Tecnico` y `Consulta` pueden visualizar y exportar reportes de todos los parques.

## Reglas de Negocio

- Los reportes deben utilizar los datos históricos existentes, sin modificar las lecturas almacenadas.
- La información debe corresponder exclusivamente al parque, nodo y período seleccionados.
- Los gráficos deben representar correctamente la generación de energía, expresada en kWh.
- El archivo CSV debe contener los mismos datos utilizados para generar el reporte.
- Los mantenimientos deben obtenerse de los registros existentes, evitando duplicar información.
- Los períodos sin información deben identificarse claramente, sin presentar datos inexistentes como generación real.
- La implementación debe respetar la arquitectura actual de SolarUY y reutilizar los servicios existentes, evitando duplicar la lógica de cálculo del histórico.

## Pruebas

Se deben incluir tests que verifiquen como mínimo:

- Visualización correcta de gráficos de barras y líneas.
- Funcionamiento de los filtros por parque, nodo y fechas.
- Coincidencia entre los gráficos y las consultas históricas.
- Actualización de resultados al cambiar los filtros.
- Exportación CSV con los datos y períodos correctos.
- Visualización de los mantenimientos realizados en el período seleccionado.
- Acceso a reportes según los roles autorizados.
- Ejecución correcta de los tests existentes y nuevos.