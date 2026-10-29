/*M!999999\- enable the sandbox mode */ 
-- MariaDB dump 10.20-11.8.9-MariaDB, for debian-linux-gnu (x86_64)
--
-- Host: localhost    Database: solaruy
-- ------------------------------------------------------
-- Server version	11.8.9-MariaDB-ubu2404

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

--
-- Current Database: `solaruy`
--

CREATE DATABASE /*!32312 IF NOT EXISTS*/ `solaruy` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_ci */;

USE `solaruy`;

--
-- Table structure for table `ParquesSolares`
--

DROP TABLE IF EXISTS `ParquesSolares`;
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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `ParquesSolares`
--

SET @OLD_AUTOCOMMIT=@@AUTOCOMMIT, @@AUTOCOMMIT=0;
LOCK TABLES `ParquesSolares` WRITE;
/*!40000 ALTER TABLE `ParquesSolares` DISABLE KEYS */;
/*!40000 ALTER TABLE `ParquesSolares` ENABLE KEYS */;
UNLOCK TABLES;
COMMIT;
SET AUTOCOMMIT=@OLD_AUTOCOMMIT;

--
-- Table structure for table `__EFMigrationsHistory`
--

DROP TABLE IF EXISTS `__EFMigrationsHistory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `__EFMigrationsHistory` (
  `MigrationId` varchar(150) NOT NULL,
  `ProductVersion` varchar(32) NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `__EFMigrationsHistory`
--

SET @OLD_AUTOCOMMIT=@@AUTOCOMMIT, @@AUTOCOMMIT=0;
LOCK TABLES `__EFMigrationsHistory` WRITE;
/*!40000 ALTER TABLE `__EFMigrationsHistory` DISABLE KEYS */;
INSERT INTO `__EFMigrationsHistory` VALUES
('20261008040649_AddParqueSolar','8.0.31');
/*!40000 ALTER TABLE `__EFMigrationsHistory` ENABLE KEYS */;
UNLOCK TABLES;
COMMIT;
SET AUTOCOMMIT=@OLD_AUTOCOMMIT;

--
-- Table structure for table `__efmigrationshistory`
--

DROP TABLE IF EXISTS `__efmigrationshistory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `__efmigrationshistory` (
  `MigrationId` varchar(150) NOT NULL,
  `ProductVersion` varchar(32) NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `__efmigrationshistory`
--

SET @OLD_AUTOCOMMIT=@@AUTOCOMMIT, @@AUTOCOMMIT=0;
LOCK TABLES `__efmigrationshistory` WRITE;
/*!40000 ALTER TABLE `__efmigrationshistory` DISABLE KEYS */;
INSERT INTO `__efmigrationshistory` VALUES
('20261008040649_AddParqueSolar','8.0.31'),
('20261015023351_AddMantenimientoPaneles','8.0.31');
/*!40000 ALTER TABLE `__efmigrationshistory` ENABLE KEYS */;
UNLOCK TABLES;
COMMIT;
SET AUTOCOMMIT=@OLD_AUTOCOMMIT;

--
-- Table structure for table `nodos`
--

DROP TABLE IF EXISTS `nodos`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `nodos` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ParqueId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Nombre` longtext NOT NULL,
  `UbicacionEnParque` longtext NOT NULL,
  `CantidadPanelesPrevista` int(11) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Nodos_ParqueId` (`ParqueId`),
  CONSTRAINT `FK_Nodos_ParquesSolares_ParqueId` FOREIGN KEY (`ParqueId`) REFERENCES `parquessolares` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `nodos`
--

SET @OLD_AUTOCOMMIT=@@AUTOCOMMIT, @@AUTOCOMMIT=0;
LOCK TABLES `nodos` WRITE;
/*!40000 ALTER TABLE `nodos` DISABLE KEYS */;
INSERT INTO `nodos` VALUES
('73c02e8d-c854-11f1-939e-18c04de89d5b','73bb00e2-c854-11f1-939e-18c04de89d5b','Nodo Norte','Sector norte del parque',3),
('73c0d7a5-c854-11f1-939e-18c04de89d5b','73bb00e2-c854-11f1-939e-18c04de89d5b','Nodo Sur','Sector sur del parque',3),
('73c177f7-c854-11f1-939e-18c04de89d5b','73bbb7f9-c854-11f1-939e-18c04de89d5b','Nodo Principal','Sector central',3);
/*!40000 ALTER TABLE `nodos` ENABLE KEYS */;
UNLOCK TABLES;
COMMIT;
SET AUTOCOMMIT=@OLD_AUTOCOMMIT;

--
-- Table structure for table `paneles`
--

DROP TABLE IF EXISTS `paneles`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `paneles` (
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
  CONSTRAINT `FK_Paneles_Nodos_NodoId` FOREIGN KEY (`NodoId`) REFERENCES `nodos` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `paneles`
--

SET @OLD_AUTOCOMMIT=@@AUTOCOMMIT, @@AUTOCOMMIT=0;
LOCK TABLES `paneles` WRITE;
/*!40000 ALTER TABLE `paneles` DISABLE KEYS */;
INSERT INTO `paneles` VALUES
('73c3f9a6-c854-11f1-939e-18c04de89d5b','73c02e8d-c854-11f1-939e-18c04de89d5b','SOL-MAL-001','Jinko Solar','Tiger Neo 575W',575.000000000000000000000000000000,'2026-01-15 00:00:00.000000',0),
('73c4a545-c854-11f1-939e-18c04de89d5b','73c02e8d-c854-11f1-939e-18c04de89d5b','SOL-MAL-002','Jinko Solar','Tiger Neo 575W',575.000000000000000000000000000000,'2026-01-15 00:00:00.000000',0),
('73c5448c-c854-11f1-939e-18c04de89d5b','73c02e8d-c854-11f1-939e-18c04de89d5b','SOL-MAL-003','LONGi','Hi-MO 6 580W',580.000000000000000000000000000000,'2026-02-10 00:00:00.000000',0),
('73c5da0c-c854-11f1-939e-18c04de89d5b','73c0d7a5-c854-11f1-939e-18c04de89d5b','SOL-MAL-004','Canadian Solar','TOPHiKu6 585W',585.000000000000000000000000000000,'2026-03-05 00:00:00.000000',0),
('73c67206-c854-11f1-939e-18c04de89d5b','73c0d7a5-c854-11f1-939e-18c04de89d5b','SOL-MAL-005','Trina Solar','Vertex N 590W',590.000000000000000000000000000000,'2026-03-05 00:00:00.000000',0),
('73c710d9-c854-11f1-939e-18c04de89d5b','73c177f7-c854-11f1-939e-18c04de89d5b','SOL-ROC-001','JA Solar','DeepBlue 4.0 580W',580.000000000000000000000000000000,'2026-04-20 00:00:00.000000',0);
/*!40000 ALTER TABLE `paneles` ENABLE KEYS */;
UNLOCK TABLES;
COMMIT;
SET AUTOCOMMIT=@OLD_AUTOCOMMIT;

--
-- Table structure for table `parquessolares`
--

DROP TABLE IF EXISTS `parquessolares`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `parquessolares` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Nombre` varchar(100) NOT NULL,
  `Ubicacion` varchar(200) NOT NULL,
  `CapacidadEstimadaMW` decimal(65,30) DEFAULT NULL,
  `FechaCreacion` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ParquesSolares_Nombre` (`Nombre`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `parquessolares`
--

SET @OLD_AUTOCOMMIT=@@AUTOCOMMIT, @@AUTOCOMMIT=0;
LOCK TABLES `parquessolares` WRITE;
/*!40000 ALTER TABLE `parquessolares` DISABLE KEYS */;
INSERT INTO `parquessolares` VALUES
('73bb00e2-c854-11f1-939e-18c04de89d5b','Parque Solar Maldonado','Maldonado, Uruguay',12.500000000000000000000000000000,'2026-10-15 01:54:06.000000'),
('73bbb7f9-c854-11f1-939e-18c04de89d5b','Parque Solar Rocha','Rocha, Uruguay',8.750000000000000000000000000000,'2026-10-15 01:54:06.000000');
/*!40000 ALTER TABLE `parquessolares` ENABLE KEYS */;
UNLOCK TABLES;
COMMIT;
SET AUTOCOMMIT=@OLD_AUTOCOMMIT;

--
-- Table structure for table `tareasmantenimiento`
--

DROP TABLE IF EXISTS `tareasmantenimiento`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `tareasmantenimiento` (
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
  CONSTRAINT `FK_TareasMantenimiento_Paneles_PanelId` FOREIGN KEY (`PanelId`) REFERENCES `paneles` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `tareasmantenimiento`
--

SET @OLD_AUTOCOMMIT=@@AUTOCOMMIT, @@AUTOCOMMIT=0;
LOCK TABLES `tareasmantenimiento` WRITE;
/*!40000 ALTER TABLE `tareasmantenimiento` DISABLE KEYS */;
INSERT INTO `tareasmantenimiento` VALUES
('73cc2073-c854-11f1-939e-18c04de89d5b','73c3f9a6-c854-11f1-939e-18c04de89d5b','Limpieza','Limpieza preventiva de superficie y revisión visual.','2026-10-25 10:00:00.000000',NULL,3,'TECNICO-PRUEBA-001'),
('73cc2270-c854-11f1-939e-18c04de89d5b','73c4a545-c854-11f1-939e-18c04de89d5b','Inspección','Revisión de conexiones y estructura del panel.','2026-10-10 14:00:00.000000','2026-10-15 04:56:10.891391',2,'TECNICO-PRUEBA-001'),
('73cc2304-c854-11f1-939e-18c04de89d5b','73c5448c-c854-11f1-939e-18c04de89d5b','Diagnóstico','Diagnóstico por reducción de potencia generada.','2026-10-15 09:00:00.000000',NULL,1,'TECNICO-PRUEBA-002'),
('73cc2351-c854-11f1-939e-18c04de89d5b','73c5da0c-c854-11f1-939e-18c04de89d5b','Limpieza','Limpieza general del panel.','2026-10-05 08:30:00.000000','2026-10-05 10:15:00.000000',2,'TECNICO-PRUEBA-001'),
('73cc2397-c854-11f1-939e-18c04de89d5b','73c67206-c854-11f1-939e-18c04de89d5b','Inspección','Inspección preventiva cancelada por condiciones climáticas.','2026-10-12 16:00:00.000000',NULL,3,'TECNICO-PRUEBA-002'),
('73cc23dd-c854-11f1-939e-18c04de89d5b','73c710d9-c854-11f1-939e-18c04de89d5b','Limpieza','Retiro de polvo y suciedad acumulada.','2026-11-02 11:00:00.000000',NULL,1,'48a7200a-b030-4818-8979-41116e05e29f');
/*!40000 ALTER TABLE `tareasmantenimiento` ENABLE KEYS */;
UNLOCK TABLES;
COMMIT;
SET AUTOCOMMIT=@OLD_AUTOCOMMIT;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*M!100616 SET NOTE_VERBOSITY=@OLD_NOTE_VERBOSITY */;

-- Dump completed on 2026-10-29  4:45:23
