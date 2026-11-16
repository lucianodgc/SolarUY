# Requerimientos: Reportes con gráficos del histórico (Radzen) y exportación

## Descripción General

Como usuario autenticado del sistema SolarUY, quiero visualizar reportes gráficos de la generación histórica de energía y consultar los mantenimientos realizados, para analizar el rendimiento de los parques solares durante diferentes períodos y exportar la información.

Esta funcionalidad corresponde a la **Entrega 3** y debe reutilizar los servicios existentes de histórico y mantenimiento.

## Criterios de Aceptación

1. **Pantalla de reportes:** El sistema debe disponer de una pantalla con gráficos de barras y líneas utilizando componentes Radzen.
2. **Generación histórica:** Los gráficos deben mostrar la generación de energía por día, mes y año, expresada en kWh.
3. **Filtros:** Permitir seleccionar parque solar, nodo y rango de fechas.
4. **Actualización:** Los gráficos y resultados deben actualizarse al modificar los filtros o el período seleccionado.
5. **Consistencia:** Los valores representados en los gráficos deben coincidir con las consultas del histórico.
6. **Exportación CSV:** Permitir descargar los datos del reporte en formato CSV, respetando los filtros y períodos seleccionados.
7. **Resumen de mantenimiento:** Mostrar los mantenimientos realizados durante el período seleccionado, reutilizando la información existente de la card de mantenimiento.
8. **Usabilidad:** La pantalla debe permitir consultar los reportes de forma clara e indicar cuando no existan datos disponibles.
9. **Autorización:** Los usuarios con roles `Administrador`, `Tecnico` y `Consulta` pueden visualizar y exportar reportes.

## Reglas de Negocio

- Los reportes deben utilizar los datos históricos almacenados en MariaDB, sin modificar las lecturas existentes.
- La información debe corresponder únicamente al parque, nodo y período seleccionados.
- Los gráficos deben mostrar la generación de energía en kWh.
- Los servicios de consulta del histórico existentes en `Application` deben reutilizarse para evitar duplicar los cálculos.
- Los rangos de fechas deben ser válidos.
- Los períodos sin información deben identificarse claramente, sin presentar datos inexistentes como generación real.
- Las lecturas históricas cuya procedencia por nodo sea desconocida no deben atribuirse a un nodo actual.
- El archivo CSV debe contener los mismos resultados que se muestran en el reporte.
- Los mantenimientos deben obtenerse de los registros existentes, evitando duplicar información.
- La implementación debe respetar la arquitectura actual de SolarUY.

## Pruebas

Se deben incluir tests que verifiquen como mínimo:

1. Visualización correcta de gráficos de barras y líneas.
2. Agrupación de generación por día, mes y año.
3. Funcionamiento de los filtros por parque, nodo y fechas.
4. Coincidencia entre los resultados de los gráficos y las consultas históricas.
5. Actualización de resultados al cambiar los filtros.
6. Exportación CSV con los datos y períodos seleccionados.
7. Visualización de los mantenimientos realizados durante el período consultado.
8. Comportamiento correcto cuando no existen datos disponibles.
9. Acceso a reportes según los roles autorizados.
10. Ejecución correcta de los tests existentes y nuevos.