# Resumen

Dos instancias web comparten MariaDB, claves de sesión y simulador TCP detrás de Nginx. El arranque se realiza con Iniciar-Demo.ps1 -AltaDisponibilidad tras preparar .env y los dos secretos de Keycloak. Ollama del host debe estar accesible desde Docker.

Se validaron configuración, sintaxis y construcción de las imágenes. El usuario había informado una demostración de caída/actualización correcta; en esta corrección no se repitió ese recorrido ni el login real. El detalle y sus límites están en implementations.md.
