-- SolarUY: base de pruebas para MariaDB 11.8.
-- IMPORTAR ESTE ARCHIVO REEMPLAZA LOS DATOS ACTUALES DE SOLARUY EN LA BASE SELECCIONADA.
-- Seleccionar primero una base de desarrollo; detener SolarUY antes de importar.
-- Incluye estructura y migraciones vigentes al 05/11/2026. No contiene contraseñas.
-- No modifica Keycloak. Las tareas se asignan al tecnico.test del entorno local.
-- En otro entorno, ajustar @TecnicoDemoId al ID real del técnico.
-- Datos: 2 parques, 4 nodos, 4 inversores, 12 paneles, 3 tareas y 0 lecturas.
-- Los cambios de estructura no son transaccionales en MariaDB.

SET @DemoForeignKeyChecks = @@FOREIGN_KEY_CHECKS;
SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS CursoresMonitoreo;
DROP TABLE IF EXISTS AvisosMonitoreo;
DROP TABLE IF EXISTS SecuenciaMonitoreo;
DROP TABLE IF EXISTS LecturasPanel;
DROP TABLE IF EXISTS TareasMantenimiento;
DROP TABLE IF EXISTS Inversores;
DROP TABLE IF EXISTS Paneles;
DROP TABLE IF EXISTS Nodos;
DROP TABLE IF EXISTS ParquesSolares;
DROP TABLE IF EXISTS __EFMigrationsHistory;


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*M!100616 SET @OLD_NOTE_VERBOSITY=@@NOTE_VERBOSITY, NOTE_VERBOSITY=0 */;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `Inversores` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `NodoId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Marca` longtext NOT NULL,
  `Modelo` longtext NOT NULL,
  `PotenciaNominalKW` decimal(65,30) NOT NULL,
  `Estado` int(11) NOT NULL,
  `VolcadoRedActivo` tinyint(1) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Inversores_NodoId` (`NodoId`),
  CONSTRAINT `FK_Inversores_Nodos_NodoId` FOREIGN KEY (`NodoId`) REFERENCES `Nodos` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `LecturasPanel` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `PanelId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `FechaHora` datetime(6) NOT NULL,
  `PotenciaGeneradaW` decimal(18,6) NOT NULL,
  `EstadoSalud` int(11) NOT NULL,
  `NodoId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_LecturasPanel_PanelId_FechaHora` (`PanelId`,`FechaHora`),
  KEY `IX_LecturasPanel_PanelId_FechaHora_PotenciaGeneradaW` (`PanelId`,`FechaHora`,`PotenciaGeneradaW`),
  KEY `IX_LecturasPanel_NodoId_FechaHora` (`NodoId`,`FechaHora`),
  KEY `IX_LecturasPanel_PanelId_FechaHora_NodoId_PotenciaGeneradaW` (`PanelId`,`FechaHora`,`NodoId`,`PotenciaGeneradaW`),
  CONSTRAINT `FK_LecturasPanel_Nodos_NodoId` FOREIGN KEY (`NodoId`) REFERENCES `Nodos` (`Id`),
  CONSTRAINT `FK_LecturasPanel_Paneles_PanelId` FOREIGN KEY (`PanelId`) REFERENCES `Paneles` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `Nodos` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ParqueId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Nombre` longtext NOT NULL,
  `UbicacionEnParque` longtext NOT NULL,
  `CantidadPanelesPrevista` int(11) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Nodos_ParqueId` (`ParqueId`),
  CONSTRAINT `FK_Nodos_ParquesSolares_ParqueId` FOREIGN KEY (`ParqueId`) REFERENCES `ParquesSolares` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `Paneles` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `NodoId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `NumeroSerie` varchar(255) NOT NULL,
  `Marca` longtext NOT NULL,
  `Modelo` longtext NOT NULL,
  `PotenciaNominalW` decimal(65,30) NOT NULL,
  `FechaInstalacion` datetime(6) NOT NULL,
  `Estado` int(11) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Paneles_NumeroSerie` (`NumeroSerie`),
  KEY `IX_Paneles_NodoId` (`NodoId`),
  CONSTRAINT `FK_Paneles_Nodos_NodoId` FOREIGN KEY (`NodoId`) REFERENCES `Nodos` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `ParquesSolares` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Nombre` varchar(100) NOT NULL,
  `Ubicacion` varchar(200) NOT NULL,
  `CapacidadEstimadaMW` decimal(65,30) DEFAULT NULL,
  `FechaCreacion` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ParquesSolares_Nombre` (`Nombre`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `TareasMantenimiento` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `PanelId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Tipo` longtext NOT NULL,
  `Descripcion` longtext NOT NULL,
  `FechaProgramada` datetime(6) NOT NULL,
  `FechaRealizacion` datetime(6) DEFAULT NULL,
  `Estado` int(11) NOT NULL,
  `ResponsableId` longtext NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_TareasMantenimiento_PanelId` (`PanelId`),
  CONSTRAINT `FK_TareasMantenimiento_Paneles_PanelId` FOREIGN KEY (`PanelId`) REFERENCES `Paneles` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `__EFMigrationsHistory` (
  `MigrationId` varchar(150) NOT NULL,
  `ProductVersion` varchar(32) NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*M!100616 SET NOTE_VERBOSITY=@OLD_NOTE_VERBOSITY */;


SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 1;

-- Datos de ejemplo. Fechas expresadas en UTC.
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

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES
('20261008040649_AddParqueSolar', '8.0.31'),
('20261015023351_AddMantenimientoPaneles', '8.0.31'),
('20261022130747_AddLecturaPanel', '8.0.31'),
('20261028235200_AddInversorNodo', '8.0.31'),
('20261029023745_AddRecoleccionPeriodica', '8.0.31'),
('20261029120000_AddDistribucionMonitoreo', '8.0.31'),
('20261029130000_PreserveUnknownHistoricalNodes', '8.0.31'),
('20261029140000_AddHistoricalNodeCoverage', '8.0.31'),
('20261105010000_RemoveDistribucionMonitoreo', '8.0.31');

COMMIT;
-- LecturasPanel queda vacía: no se insertan mediciones.

SET FOREIGN_KEY_CHECKS = @DemoForeignKeyChecks;
