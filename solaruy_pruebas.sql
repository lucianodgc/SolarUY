-- SolarUY: datos de demostración para una base de desarrollo con migraciones aplicadas.
-- REEMPLAZA LOS DATOS ACTUALES. Detener todas las webs y recolectores antes de importar.
-- Iniciar-Demo aplica primero las migraciones. Este archivo no recrea tablas ni modifica
-- __EFMigrationsHistory. El borrado y la carga son transaccionales; no deshabilitar las FK.
-- Incluye 2 parques, 4 nodos, 4 inversores, 12 paneles, 3 tareas y ninguna lectura.
-- No modifica Keycloak; @TecnicoDemoId permite elegir un técnico real del entorno.

SET NAMES utf8mb4;
START TRANSACTION;

DELETE FROM IntentosOrdenInversor;
DELETE FROM AlertasPanel;
DELETE FROM CambiosEstadoSaludPanel;
DELETE FROM SaludPaneles;
DELETE FROM LecturasPanel;
DELETE FROM TareasMantenimiento;
DELETE FROM Paneles;
DELETE FROM Inversores;
DELETE FROM Nodos;
DELETE FROM ParquesSolares;

SET @TecnicoDemoId = COALESCE(@TecnicoDemoId, '05f692fb-c636-4164-a7eb-d1fb640d7415');
START TRANSACTION;

INSERT INTO `ParquesSolares` (`Id`, `Nombre`, `Ubicacion`, `CapacidadEstimadaMW`, `FechaCreacion`) VALUES
('48817e6c-cc9f-4099-81da-8af3697cbcc9', 'Parque Demo Maldonado', 'Maldonado, Uruguay', 0.0033, UTC_TIMESTAMP(6) - INTERVAL 120 DAY),
('9066a2ce-a55d-4478-a6ef-4127458d9420', 'Parque Demo Rocha', 'Rocha, Uruguay', 0.0030, UTC_TIMESTAMP(6) - INTERVAL 100 DAY);

INSERT INTO `Nodos` (`Id`, `ParqueId`, `Nombre`, `UbicacionEnParque`, `CantidadPanelesPrevista`) VALUES
('e3bba0fa-2f79-4e50-ad88-091fb6347905', '48817e6c-cc9f-4099-81da-8af3697cbcc9', 'Nodo Norte', 'Sector norte', 3),
('f2be5b35-2e30-4b5e-8ef2-d0b51791152f', '48817e6c-cc9f-4099-81da-8af3697cbcc9', 'Nodo Sur', 'Sector sur', 3),
('e9dce473-48d2-4616-900c-cab980962073', '9066a2ce-a55d-4478-a6ef-4127458d9420', 'Nodo Norte', 'Sector norte', 3),
('431fd251-dd29-44a7-926d-23e9ab8b5a1e', '9066a2ce-a55d-4478-a6ef-4127458d9420', 'Nodo Sur', 'Sector sur', 3);

INSERT INTO `Inversores` (`Id`, `NodoId`, `Marca`, `Modelo`, `PotenciaNominalKW`, `Estado`, `VolcadoRedActivo`) VALUES
('1416e0d4-1fdf-4867-acb9-3555621237a9', 'e3bba0fa-2f79-4e50-ad88-091fb6347905', 'Simulado', 'INV-Demo-3K', 3.0, 0, 0),
('b6d0d889-0e53-4673-8fd6-c3829eac2e15', 'f2be5b35-2e30-4b5e-8ef2-d0b51791152f', 'Simulado', 'INV-Demo-3K', 3.0, 0, 0),
('f0a5b456-4a6f-4293-9dbb-fd0b7711cdc2', 'e9dce473-48d2-4616-900c-cab980962073', 'Simulado', 'INV-Demo-3K', 3.0, 0, 0),
('cc185f9c-1eba-4974-ab31-557b8456aef1', '431fd251-dd29-44a7-926d-23e9ab8b5a1e', 'Simulado', 'INV-Demo-3K', 3.0, 0, 0);

-- Estado del panel: 0 activo, 1 inactivo, 2 falla.
INSERT INTO `Paneles` (`Id`, `NodoId`, `NumeroSerie`, `Marca`, `Modelo`, `PotenciaNominalW`, `FechaInstalacion`, `Estado`) VALUES
('ab7b902c-48e1-47dd-9edb-48440bc9a006', 'e3bba0fa-2f79-4e50-ad88-091fb6347905', 'DEMO-MAL-001', 'Panel de prueba', 'Demo 550W', 550, UTC_TIMESTAMP(6) - INTERVAL 90 DAY, 0),
('b9e936f7-f2dd-4bb2-b2f7-56480898b1bc', 'e3bba0fa-2f79-4e50-ad88-091fb6347905', 'DEMO-MAL-002', 'Panel de prueba', 'Demo 550W', 550, UTC_TIMESTAMP(6) - INTERVAL 90 DAY, 0),
('8ccb1703-0d82-4005-a8f3-b10e3f02d041', 'e3bba0fa-2f79-4e50-ad88-091fb6347905', 'DEMO-MAL-003', 'Panel de prueba', 'Demo 550W', 550, UTC_TIMESTAMP(6) - INTERVAL 90 DAY, 0),
('969633b4-4545-4416-a0a4-eabc45e6ae7d', 'f2be5b35-2e30-4b5e-8ef2-d0b51791152f', 'DEMO-MAL-004', 'Panel de prueba', 'Demo 550W', 550, UTC_TIMESTAMP(6) - INTERVAL 90 DAY, 0),
('65656fe7-6366-424e-ba27-48c8380df028', 'f2be5b35-2e30-4b5e-8ef2-d0b51791152f', 'DEMO-MAL-005', 'Panel de prueba', 'Demo 550W', 550, UTC_TIMESTAMP(6) - INTERVAL 90 DAY, 1),
('84dde9ff-595d-4a2d-a1ed-0eb9dd84f7d6', 'f2be5b35-2e30-4b5e-8ef2-d0b51791152f', 'DEMO-MAL-006', 'Panel de prueba', 'Demo 550W', 550, UTC_TIMESTAMP(6) - INTERVAL 90 DAY, 0),
('aa43cd21-d078-4891-9107-41806a64055a', 'e9dce473-48d2-4616-900c-cab980962073', 'DEMO-ROC-001', 'Panel de prueba', 'Demo 500W', 500, UTC_TIMESTAMP(6) - INTERVAL 90 DAY, 0),
('d7341a11-1454-4fea-b8ca-999efe359ef1', 'e9dce473-48d2-4616-900c-cab980962073', 'DEMO-ROC-002', 'Panel de prueba', 'Demo 500W', 500, UTC_TIMESTAMP(6) - INTERVAL 90 DAY, 0),
('cb84a26e-84c3-4015-b8b8-b8bfadb188e1', 'e9dce473-48d2-4616-900c-cab980962073', 'DEMO-ROC-003', 'Panel de prueba', 'Demo 500W', 500, UTC_TIMESTAMP(6) - INTERVAL 90 DAY, 0),
('63f9ace4-dbb2-426d-b864-58c53c699b6f', '431fd251-dd29-44a7-926d-23e9ab8b5a1e', 'DEMO-ROC-004', 'Panel de prueba', 'Demo 500W', 500, UTC_TIMESTAMP(6) - INTERVAL 90 DAY, 0),
('8338a89b-5873-407c-bbb6-0747f8e90077', '431fd251-dd29-44a7-926d-23e9ab8b5a1e', 'DEMO-ROC-005', 'Panel de prueba', 'Demo 500W', 500, UTC_TIMESTAMP(6) - INTERVAL 90 DAY, 2),
('b244eff2-9a25-4c68-a9b7-bfa56846be41', '431fd251-dd29-44a7-926d-23e9ab8b5a1e', 'DEMO-ROC-006', 'Panel de prueba', 'Demo 500W', 500, UTC_TIMESTAMP(6) - INTERVAL 90 DAY, 0);

-- Tareas abiertas: pendientes (0) y en curso (1).
INSERT INTO `TareasMantenimiento` (`Id`, `PanelId`, `Tipo`, `Descripcion`, `FechaProgramada`, `FechaRealizacion`, `Estado`, `ResponsableId`) VALUES
('3576115e-7948-48f9-aca7-3b9f808bbf54', 'ab7b902c-48e1-47dd-9edb-48440bc9a006', 'Limpieza', 'Limpiar el panel y comprobar su superficie.', UTC_TIMESTAMP(6) + INTERVAL 1 DAY, NULL, 0, @TecnicoDemoId),
('4498bb85-96cc-437e-bca7-57e7a4638382', '65656fe7-6366-424e-ba27-48c8380df028', 'Inspección', 'Revisar el panel inactivo antes de volver a utilizarlo.', UTC_TIMESTAMP(6) + INTERVAL 2 DAY, NULL, 0, @TecnicoDemoId),
('3536cb41-6b0a-4a3a-9e33-a0fc7178a7ad', '8338a89b-5873-407c-bbb6-0747f8e90077', 'Reparación', 'Revisar la falla simulada y sus conexiones.', UTC_TIMESTAMP(6) - INTERVAL 1 DAY, NULL, 1, @TecnicoDemoId);

COMMIT;
-- No se inventan mediciones: la recolección generará las nuevas lecturas.
