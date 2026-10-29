# Implementación: Simulador TCP del inversor

## Resumen

La aplicación web ahora usa `InversorTcpClient`, registrado en Infrastructure como implementación de `IInversorClient`. El proceso separado `SolarUY.InversorSimulador` escucha en `127.0.0.1:5055` por defecto y usa el motor `InversorSimulador` para responder con JSON Lines UTF-8. La aplicación consulta el inventario de MariaDB y envía nodos y paneles al simulador por TCP; no hay topología de prueba predeterminada ni cambios persistidos por sus controles.

Las lecturas por panel reciben un factor aleatorio entre 0,90 y 1,00 sin depender de la hora real. El simulador conserva un estado global de encendido: inicia encendido y, cuando se apaga, las potencias reportadas son cero. El estado reside en el proceso simulador y no se persiste. El nodo y los paneles predeterminados son configuración local del proceso y no modifican los registros de MariaDB.

`IInversorService` consulta y cambia el estado global. Application autoriza el encendido/apagado solo a Administrador; Técnico y Consulta conservan acceso de lectura. `/simulador` muestra el estado nuevo, los controles de Administrador y errores de conexión sin mantener lecturas antiguas visibles como actuales.

## Protocolo y ejecución local

Cada operación abre una conexión TCP independiente y envía una solicitud JSON en una línea UTF-8. El servidor devuelve una respuesta JSON en una línea con éxito, resultado o código/mensaje de error. Las líneas se limitan a 1 MiB y ambas partes usan una espera de 3 segundos.

Desde la raíz del repositorio, iniciar primero el proceso simulador y dejarlo activo:

```powershell
dotnet run --project SolarUY.InversorSimulador/SolarUY.InversorSimulador.csproj
```

Después iniciar SolarUY como siempre. La dirección y el puerto se configuran mediante `InversorTcp__Host` e `InversorTcp__Port` en la aplicación y en el simulador. El inventario se toma de MariaDB y se sincroniza automáticamente al abrir/actualizar la pantalla. Las fallas simuladas se pueden configurar mediante `InversorSimulador__PanelesConFalla` en el proceso simulador.

## Archivos modificados o creados

- `SolarUY.Application/Abstractions/IInversorClient.cs`: consulta y comando del estado global.
- `SolarUY.Application/Inversores/IInversorService.cs`, `InversorService.cs` y `EstadoInversorDto.cs`: caso de uso autorizado y estado para Presentation.
- `SolarUY.Application/Exceptions/ErrorComunicacionInversorException.cs`: error de aplicación para indisponibilidad y respuestas inválidas.
- `SolarUY.Infrastructure/Inversores/InversorSimulador.cs`: variación aleatoria y estado global que apaga la generación.
- `SolarUY.Infrastructure/Inversores/Tcp/`: opciones, protocolo JSON Lines, cliente TCP y servidor del simulador.
- `SolarUY.Infrastructure/DependencyInjection.cs`: registro del cliente TCP y validación de configuración.
- `SolarUY.InversorSimulador/`: proceso .NET 8 que hospeda el listener.
- `SolarUY.Presentation/Components/Pages/SimuladorInversor.razor` y `appsettings.json`: controles y conexión TCP.
- `SolarUY.Integration.Tests/InversorIntegrationTests.cs`, `RecoleccionLecturasIntegrationTests.cs` y `InversorTcpIntegrationTests.cs`: casos de estado/roles, adaptación de contratos y cobertura de TCP.
- `SolarUY.sln`, `README.md`, `SolarUY.Integration.Tests/README.md` y los documentos REQ013.

## Verificación

- `dotnet build SolarUY.sln --no-restore`: correcto, 0 advertencias y 0 errores.
- `dotnet test SolarUY.sln`: 46 pruebas Domain, 16 Application, 9 CodeQuality y 29 Integration pasaron. Se omitieron 33 pruebas de integración que requieren `SOLARUY_HISTORICO_TEST_CONNECTION`.
- `dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --filter FullyQualifiedName~InversorTcpIntegrationTests`: 3 pruebas pasaron y 0 fallaron.
