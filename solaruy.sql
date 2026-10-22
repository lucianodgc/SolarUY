-- phpMyAdmin SQL Dump
-- version 5.2.1
-- https://www.phpmyadmin.net/
--
-- Servidor: 127.0.0.1
-- Tiempo de generación: 22-10-2026 a las 04:42:59
-- Versión del servidor: 10.4.32-MariaDB
-- Versión de PHP: 8.2.12

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Base de datos: `solaruy`
--

-- --------------------------------------------------------

--
-- Estructura de tabla para la tabla `nodos`
--

CREATE TABLE `nodos` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ParqueId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Nombre` longtext NOT NULL,
  `UbicacionEnParque` longtext NOT NULL,
  `CantidadPanelesPrevista` int(11) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Volcado de datos para la tabla `nodos`
--

INSERT INTO `nodos` (`Id`, `ParqueId`, `Nombre`, `UbicacionEnParque`, `CantidadPanelesPrevista`) VALUES
('73c02e8d-c854-11f1-939e-18c04de89d5b', '73bb00e2-c854-11f1-939e-18c04de89d5b', 'Nodo Norte', 'Sector norte del parque', 3),
('73c0d7a5-c854-11f1-939e-18c04de89d5b', '73bb00e2-c854-11f1-939e-18c04de89d5b', 'Nodo Sur', 'Sector sur del parque', 3),
('73c177f7-c854-11f1-939e-18c04de89d5b', '73bbb7f9-c854-11f1-939e-18c04de89d5b', 'Nodo Principal', 'Sector central', 3);

-- --------------------------------------------------------

--
-- Estructura de tabla para la tabla `paneles`
--

CREATE TABLE `paneles` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `NodoId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `NumeroSerie` varchar(255) NOT NULL,
  `Marca` longtext NOT NULL,
  `Modelo` longtext NOT NULL,
  `PotenciaNominalW` decimal(65,30) NOT NULL,
  `FechaInstalacion` datetime(6) NOT NULL,
  `Estado` int(11) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Volcado de datos para la tabla `paneles`
--

INSERT INTO `paneles` (`Id`, `NodoId`, `NumeroSerie`, `Marca`, `Modelo`, `PotenciaNominalW`, `FechaInstalacion`, `Estado`) VALUES
('73c3f9a6-c854-11f1-939e-18c04de89d5b', '73c02e8d-c854-11f1-939e-18c04de89d5b', 'SOL-MAL-001', 'Jinko Solar', 'Tiger Neo 575W', 575.000000000000000000000000000000, '2026-01-15 00:00:00.000000', 0),
('73c4a545-c854-11f1-939e-18c04de89d5b', '73c02e8d-c854-11f1-939e-18c04de89d5b', 'SOL-MAL-002', 'Jinko Solar', 'Tiger Neo 575W', 575.000000000000000000000000000000, '2026-01-15 00:00:00.000000', 0),
('73c5448c-c854-11f1-939e-18c04de89d5b', '73c02e8d-c854-11f1-939e-18c04de89d5b', 'SOL-MAL-003', 'LONGi', 'Hi-MO 6 580W', 580.000000000000000000000000000000, '2026-02-10 00:00:00.000000', 0),
('73c5da0c-c854-11f1-939e-18c04de89d5b', '73c0d7a5-c854-11f1-939e-18c04de89d5b', 'SOL-MAL-004', 'Canadian Solar', 'TOPHiKu6 585W', 585.000000000000000000000000000000, '2026-03-05 00:00:00.000000', 0),
('73c67206-c854-11f1-939e-18c04de89d5b', '73c0d7a5-c854-11f1-939e-18c04de89d5b', 'SOL-MAL-005', 'Trina Solar', 'Vertex N 590W', 590.000000000000000000000000000000, '2026-03-05 00:00:00.000000', 0),
('73c710d9-c854-11f1-939e-18c04de89d5b', '73c177f7-c854-11f1-939e-18c04de89d5b', 'SOL-ROC-001', 'JA Solar', 'DeepBlue 4.0 580W', 580.000000000000000000000000000000, '2026-04-20 00:00:00.000000', 0);

-- --------------------------------------------------------

--
-- Estructura de tabla para la tabla `parquessolares`
--

CREATE TABLE `parquessolares` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Nombre` varchar(100) NOT NULL,
  `Ubicacion` varchar(200) NOT NULL,
  `CapacidadEstimadaMW` decimal(65,30) DEFAULT NULL,
  `FechaCreacion` datetime(6) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Volcado de datos para la tabla `parquessolares`
--

INSERT INTO `parquessolares` (`Id`, `Nombre`, `Ubicacion`, `CapacidadEstimadaMW`, `FechaCreacion`) VALUES
('73bb00e2-c854-11f1-939e-18c04de89d5b', 'Parque Solar Maldonado', 'Maldonado, Uruguay', 12.500000000000000000000000000000, '2026-10-15 01:54:06.000000'),
('73bbb7f9-c854-11f1-939e-18c04de89d5b', 'Parque Solar Rocha', 'Rocha, Uruguay', 8.750000000000000000000000000000, '2026-10-15 01:54:06.000000');

-- --------------------------------------------------------

--
-- Estructura de tabla para la tabla `tareasmantenimiento`
--

CREATE TABLE `tareasmantenimiento` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `PanelId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Tipo` longtext NOT NULL,
  `Descripcion` longtext NOT NULL,
  `FechaProgramada` datetime(6) NOT NULL,
  `FechaRealizacion` datetime(6) DEFAULT NULL,
  `Estado` int(11) NOT NULL,
  `ResponsableId` longtext NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Volcado de datos para la tabla `tareasmantenimiento`
--

INSERT INTO `tareasmantenimiento` (`Id`, `PanelId`, `Tipo`, `Descripcion`, `FechaProgramada`, `FechaRealizacion`, `Estado`, `ResponsableId`) VALUES
('73cc2073-c854-11f1-939e-18c04de89d5b', '73c3f9a6-c854-11f1-939e-18c04de89d5b', 'Limpieza', 'Limpieza preventiva de superficie y revisión visual.', '2026-10-25 10:00:00.000000', NULL, 3, 'TECNICO-PRUEBA-001'),
('73cc2270-c854-11f1-939e-18c04de89d5b', '73c4a545-c854-11f1-939e-18c04de89d5b', 'Inspección', 'Revisión de conexiones y estructura del panel.', '2026-10-10 14:00:00.000000', '2026-10-15 04:56:10.891391', 2, 'TECNICO-PRUEBA-001'),
('73cc2304-c854-11f1-939e-18c04de89d5b', '73c5448c-c854-11f1-939e-18c04de89d5b', 'Diagnóstico', 'Diagnóstico por reducción de potencia generada.', '2026-10-15 09:00:00.000000', NULL, 1, 'TECNICO-PRUEBA-002'),
('73cc2351-c854-11f1-939e-18c04de89d5b', '73c5da0c-c854-11f1-939e-18c04de89d5b', 'Limpieza', 'Limpieza general del panel.', '2026-10-05 08:30:00.000000', '2026-10-05 10:15:00.000000', 2, 'TECNICO-PRUEBA-001'),
('73cc2397-c854-11f1-939e-18c04de89d5b', '73c67206-c854-11f1-939e-18c04de89d5b', 'Inspección', 'Inspección preventiva cancelada por condiciones climáticas.', '2026-10-12 16:00:00.000000', NULL, 3, 'TECNICO-PRUEBA-002'),
('73cc23dd-c854-11f1-939e-18c04de89d5b', '73c710d9-c854-11f1-939e-18c04de89d5b', 'Limpieza', 'Retiro de polvo y suciedad acumulada.', '2026-11-02 11:00:00.000000', NULL, 1, '48a7200a-b030-4818-8979-41116e05e29f');

-- --------------------------------------------------------

--
-- Estructura de tabla para la tabla `__efmigrationshistory`
--

CREATE TABLE `__efmigrationshistory` (
  `MigrationId` varchar(150) NOT NULL,
  `ProductVersion` varchar(32) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Volcado de datos para la tabla `__efmigrationshistory`
--

INSERT INTO `__efmigrationshistory` (`MigrationId`, `ProductVersion`) VALUES
('20261008040649_AddParqueSolar', '8.0.31'),
('20261015023351_AddMantenimientoPaneles', '8.0.31');

--
-- Índices para tablas volcadas
--

--
-- Indices de la tabla `nodos`
--
ALTER TABLE `nodos`
  ADD PRIMARY KEY (`Id`),
  ADD KEY `IX_Nodos_ParqueId` (`ParqueId`);

--
-- Indices de la tabla `paneles`
--
ALTER TABLE `paneles`
  ADD PRIMARY KEY (`Id`),
  ADD UNIQUE KEY `IX_Paneles_NumeroSerie` (`NumeroSerie`),
  ADD KEY `IX_Paneles_NodoId` (`NodoId`);

--
-- Indices de la tabla `parquessolares`
--
ALTER TABLE `parquessolares`
  ADD PRIMARY KEY (`Id`),
  ADD UNIQUE KEY `IX_ParquesSolares_Nombre` (`Nombre`);

--
-- Indices de la tabla `tareasmantenimiento`
--
ALTER TABLE `tareasmantenimiento`
  ADD PRIMARY KEY (`Id`),
  ADD KEY `IX_TareasMantenimiento_PanelId` (`PanelId`);

--
-- Indices de la tabla `__efmigrationshistory`
--
ALTER TABLE `__efmigrationshistory`
  ADD PRIMARY KEY (`MigrationId`);

--
-- Restricciones para tablas volcadas
--

--
-- Filtros para la tabla `nodos`
--
ALTER TABLE `nodos`
  ADD CONSTRAINT `FK_Nodos_ParquesSolares_ParqueId` FOREIGN KEY (`ParqueId`) REFERENCES `parquessolares` (`Id`);

--
-- Filtros para la tabla `paneles`
--
ALTER TABLE `paneles`
  ADD CONSTRAINT `FK_Paneles_Nodos_NodoId` FOREIGN KEY (`NodoId`) REFERENCES `nodos` (`Id`);

--
-- Filtros para la tabla `tareasmantenimiento`
--
ALTER TABLE `tareasmantenimiento`
  ADD CONSTRAINT `FK_TareasMantenimiento_Paneles_PanelId` FOREIGN KEY (`PanelId`) REFERENCES `paneles` (`Id`);
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
