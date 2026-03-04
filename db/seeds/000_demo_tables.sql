-- ============================================
-- OPTIMA VERIFICA - Demo Tables Structure
-- Run this BEFORE demo data if tables don't exist
-- ============================================

-- Table: tss (if not exists in target DB)
CREATE TABLE IF NOT EXISTS tss (
    id INT AUTO_INCREMENT PRIMARY KEY,
    RNC VARCHAR(50),
    CEDULA VARCHAR(255),
    FECHA DATE,
    SALARIO DECIMAL(15,2),
    EsOficial BIT,
    INDEX idx_tss_cedula_fecha (CEDULA, FECHA DESC),
    INDEX idx_tss_rnc_fecha (RNC, FECHA DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Table: ilocalizadosappsprocessor (if not exists)
CREATE TABLE IF NOT EXISTS ilocalizadosappsprocessor (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Cedula VARCHAR(50),
    Nombre VARCHAR(255),
    Telefono VARCHAR(50),
    Estado VARCHAR(100),
    FechaConsulta DATETIME,
    INDEX idx_iloc_cedula_fecha (Cedula, FechaConsulta DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Table: telefonos_respaldo (if not exists)
CREATE TABLE IF NOT EXISTS telefonos_respaldo (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    cedula VARCHAR(100),
    telefono VARCHAR(100),
    INDEX idx_tel_cedula (cedula)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Table: vehi (if not exists)
CREATE TABLE IF NOT EXISTS vehi (
    id INT AUTO_INCREMENT PRIMARY KEY,
    f1 VARCHAR(255),
    f2 VARCHAR(255),
    f3 VARCHAR(255),
    f4 VARCHAR(255),
    f5 VARCHAR(255),
    f6 VARCHAR(255),
    f7 VARCHAR(255),
    f8 VARCHAR(255),
    f9 VARCHAR(255),
    f10 VARCHAR(255),
    f11 VARCHAR(255),
    f12 VARCHAR(255),
    f13 VARCHAR(255),
    f14 VARCHAR(255),
    f15 VARCHAR(255),
    f19 VARCHAR(255),
    INDEX idx_vehi_f19 (f19)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
