# Resumen

Se preparó SolarUY para ejecutarse en dos instancias Docker detrás de Nginx, compartir las claves de Data Protection y comprobar salud y continuidad durante reemplazos. Compose requiere el secreto OIDC del cliente `solaruy-web` en `.env`. La validación de configuración pasó; queda demostrar caída y actualización con Docker Desktop y las credenciales locales.
