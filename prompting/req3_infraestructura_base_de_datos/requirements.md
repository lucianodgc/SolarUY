# Requerimientos: Base de datos, MariaDB en Docker, DbContext y primera migración

## Descripción General
   - Quiero poder levantar MariaDB en Docker (compose.yaml) y configurar la cadena de conexión fuera del código.
   - Crear el DbContext en Infrastructure (EF Core Code-First, proveedor Pomelo para MariaDB).
   - Primera migración con las entidades Parque, Nodo y Panel.
   - Documentar cómo aplicar migraciones.

## Criterios de Aceptación
   - dotnet ef database update crea las tablas.
   - Todos los compañeros pueden levantar la base con un solo comando.
