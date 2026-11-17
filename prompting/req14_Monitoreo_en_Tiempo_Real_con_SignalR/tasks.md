# Tareas: monitoreo en vivo

- [x] Cierre adicional: crear `resumen.md` con una síntesis breve del trabajo y las verificaciones registradas.

Alcance vigente aprobado el 05/11/2026: una instancia, SignalR directo y retirada de la distribución persistente. Sustituye las tareas anteriores de D2/Nginx; el registro histórico permanece en Git y en implementations.md.

- [x] Mantener telemetría compartida cada 2 segundos y persistencia histórica cada 30 segundos.
- [x] Retirar worker, repositorio, contrato y configuración de distribución persistente.
- [x] Guardar el lote sin avisos ni secuencias; conservar transacción y exclusión del recolector.
- [x] Crear migración incremental que elimina solo las tres tablas técnicas.
- [x] Adaptar integración a la conservación de datos y recolección sin esas tablas.
- [x] Marcar como históricos y omitidos los escenarios de dos instancias que requieren el transporte retirado.
- [x] Compilar y ejecutar las 9 pruebas de arquitectura.
- [x] Ejecutar Domain y las pruebas pertinentes de integración: 57 y 30 correctas; un servidor temporal de navegador omitido.
- [x] Aplicar la migración local y comprobar conservación de datos.
- [x] Comprobar actualizaciones en navegador después de retirar las tablas.
- [x] Actualizar README, requisitos y especificación para el alcance actual.
