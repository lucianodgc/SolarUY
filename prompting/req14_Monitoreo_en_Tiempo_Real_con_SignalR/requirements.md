# Requerimientos: monitoreo en tiempo real con SignalR

Como usuario autenticado quiero consultar la generación de un parque, sus nodos y paneles sin recargar la página.

## Alcance aprobado

- Ejecutar actualmente una instancia de SolarUY. El balanceo queda para otra etapa.
- Consultar el simulador cada 2 segundos y compartir la misma muestra con Monitoreo y Simulador mediante SignalR.
- Guardar lecturas históricas en segundo plano cada 30 segundos; conservar Histórico y Reportes basados en MariaDB.
- Retirar las tablas técnicas AvisosMonitoreo, CursoresMonitoreo y SecuenciaMonitoreo, y el proceso que las consulta, conservando los datos solares.

## Criterios de aceptación

- Seleccionar parque e identificar sus nodos y paneles, con potencia en W y fecha UTC.
- Actualizar valores y gráfico automáticamente; acotar los puntos y distinguir ausencia de datos de cero medido.
- La actualización en vivo no debe esperar al guardado histórico ni consultar la base por cada muestra recibida.
- Mantener carga inicial, reconexión, cambio de parque y liberación de suscripciones al salir.
- Administrador, Tecnico y Consulta pueden consultar. Autorizar también consultas y suscripciones en el servidor.
- Monitoreo no permite modificar parques, nodos, paneles ni lecturas. El simulador conserva los permisos de operación existentes.
- La migración elimina solo las tablas técnicas; las lecturas y el inventario permanecen intactos y se siguen guardando sin duplicados.
- Documentar qué se verificó y qué quedó pendiente. La distribución entre varias instancias no forma parte de esta versión.

## Restricciones

Mantener las tecnologías y capas existentes. Application coordina y calcula, Infrastructure consulta TCP o MariaDB y Presentation muestra datos y transporta eventos. No se incorporan nuevas dependencias ni se modifican las reglas protegidas del proyecto.
