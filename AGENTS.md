# Instrucciones para agentes

Estas instrucciones aplican a todo el árbol `SolarUY/`. También se deben respetar las instrucciones aplicables de directorios más específicos. Si hay un conflicto, sigue la instrucción de mayor prioridad y señala el conflicto cuando afecte el trabajo.

## Ubicación de las instrucciones del proyecto

- Las reglas del proyecto están en `SolarUY/.agents/rules/`.
- Las habilidades del proyecto están en `SolarUY/.agents/skills/`.

Consulta las reglas y habilidades pertinentes antes de realizar tareas en el proyecto y respeta sus instrucciones, salvo que entren en conflicto con una instrucción explícita del usuario.

## Forma de trabajo

- Antes de implementar, revisa los requisitos, especificaciones y tareas pertinentes que existan para la funcionalidad solicitada.
- Mantén los documentos de requisitos, especificaciones y tareas coherentes con el alcance solicitado. No agregues requisitos ni decisiones técnicas que no estén respaldados por el usuario o la especificación.
- Limita los cambios a lo necesario para la solicitud. No modifiques archivos ajenos al alcance solo para hacer limpieza o aplicar mejoras oportunistas.

## Arquitectura y calidad

- Las reglas en `.agents/rules/` son la fuente de verdad para las decisiones arquitectónicas y convenciones del proyecto. Consulta las reglas pertinentes en vez de duplicar su contenido aquí.
- Respeta las pruebas y guardarraíles de calidad existentes. No modifiques pruebas arquitectónicas para hacer que pasen; si parecen incorrectas o desactualizadas, explica el problema y solicita instrucciones.
- Sigue las instrucciones aplicables de verificación del proyecto. Al informar el resultado, indica qué verificaciones ejecutaste y sus resultados; si alguna no se ejecutó, dilo claramente.

## Protección de reglas y habilidades

- No crees, edites, renombres ni elimines archivos dentro de `SolarUY/.agents/rules/` o `SolarUY/.agents/skills/` por iniciativa propia.
- Una solicitud para implementar una funcionalidad, corregir código o completar otra tarea no autoriza cambios en reglas o habilidades, aunque parezcan necesarios para completar el trabajo.
- Solo considera modificar esos archivos cuando el usuario lo solicite directamente y de forma explícita.
- Incluso ante una solicitud directa, antes de modificar, crear o eliminar archivos de reglas o habilidades, presenta el cambio previsto y solicita confirmación adicional. No ejecutes ese cambio hasta recibirla.
- Si una regla o habilidad impide completar otra tarea, explica el conflicto y solicita instrucciones; no la alteres para sortearlo.
