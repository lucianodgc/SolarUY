# Resumen req18

Se implementó el ABM de paneles solares. Administrador puede dar de alta, editar, trasladar y realizar baja lógica; Administrador, Tecnico y Consulta pueden consultar los paneles activos.

Cada nodo respeta su capacidad máxima configurada y los traslados conservan el historial del panel. La baja no borra lecturas ni mantenimientos. Las pruebas de dominio y arquitectura finalizaron correctamente; queda pendiente comprobar la migración y autorización contra MariaDB e identidad reales.
