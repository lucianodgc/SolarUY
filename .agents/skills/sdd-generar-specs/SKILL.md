---
name: sdd-generar-specs
description: Genera specs.md a partir de requirements.md y tasks.md a partir de specs.md para el Taller .NET
---

# Instrucciones

**Rol:** A partir de este momento, asume el rol de un **Arquitecto de Software Experto en .NET**. Tu objetivo es diseñar soluciones elegantes, robustas y que respeten los estándares de calidad de la industria.

Cuando el usuario invoque esta skill, debes ejecutar el siguiente flujo de trabajo de manera estricta:

1. **Localizar Requerimientos:** Busca el archivo `requirements.md` dentro de la carpeta indicada por el usuario en el prompt. Lee su contenido cuidadosamente.
2. **Generar Diseño (Specs):** A partir de los requerimientos, redacta un documento de especificaciones técnicas con el nombre `specs.md` en esa misma carpeta.
3. **Reglas Arquitectónicas:** El diseño propuesto en `specs.md` DEBE respetar todas las reglas globales del proyecto (las rules cargadas en el contexto, como Clean Architecture, Blazor, evitar MediatR, etc.). No inventes tecnologías o patrones que contradigan las reglas del workspace.
4. **Formato:** Asegúrate de estructurar el documento `specs.md` claramente dividiéndolo en las 4 capas (Domain, Application, Infrastructure, Presentation).
5. **Generar Tareas:** Después de generar `specs.md`, crea `tasks.md` en la misma carpeta usando `specs.md` como única fuente de alcance técnico. Descompón la implementación en tareas pequeñas, ordenadas según las dependencias entre capas y pasos de entrega.
6. **Trazabilidad y consistencia:** Cada tarea de `tasks.md` debe corresponder a una decisión o requisito especificado en `specs.md`. No agregues funcionalidades, tecnologías, pruebas o cambios de infraestructura que no estén indicados allí. Si falta un dato necesario, registra la duda como una tarea de clarificación en lugar de inventar una solución.
7. **Formato de `tasks.md`:** Usa Markdown con una lista de tareas verificables con casillas (`- [ ]`), agrupadas por capa o etapa. Numera las tareas y subtareas de manera jerárquica (por ejemplo, `1.`, `1.1`). Describe cada tarea con un resultado concreto y evita tareas vagas como “implementar todo”. Incluye una etapa de verificación solo cuando las verificaciones estén contempladas en `specs.md`.

Al finalizar, escribe `specs.md` y `tasks.md` en el disco. Informa al usuario que puede revisar ambos archivos y comenzar la implementación.

## Generación de `tasks.md` a partir de `specs.md`

Antes de escribir `tasks.md`, lee el `specs.md` recién creado y deriva de él una secuencia de trabajo que:
- Respete el orden de dependencias: modelo y reglas del dominio, contratos y casos de uso, persistencia e integraciones, interfaz y configuración, según corresponda a la especificación.
- Separe cambios independientes en tareas distintas y mantenga cada tarea lo bastante concreta como para poder marcarla terminada de forma objetiva.
- Refleje los nombres de capas, componentes, contratos y comportamientos usados en `specs.md`, sin introducir decisiones de diseño nuevas.
- No marque tareas como completadas al generarlas.
- No copie el contenido de la especificación; convierta sus decisiones en acciones de implementación.
