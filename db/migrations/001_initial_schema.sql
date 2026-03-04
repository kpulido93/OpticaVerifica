-- ============================================
-- OPTIMA VERIFICA - Database Schema Migration
-- Version: 001
-- Description: Initial schema setup
-- ============================================

-- ============================================
-- JOBS MANAGEMENT TABLES
-- ============================================

CREATE TABLE IF NOT EXISTS jobs (
    id CHAR(36) PRIMARY KEY,
    preset_key VARCHAR(100) NOT NULL,
    preset_version INT DEFAULT 1,
    status ENUM('PENDING', 'PROCESSING', 'COMPLETED', 'FAILED', 'CANCELLED', 'PAUSED_BY_SCHEDULE') NOT NULL DEFAULT 'PENDING',
    total_items INT NOT NULL DEFAULT 0,
    processed_items INT NOT NULL DEFAULT 0,
    failed_items INT NOT NULL DEFAULT 0,
    params_json JSON,
    error_message TEXT,
    created_by VARCHAR(100) NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    started_at DATETIME,
    completed_at DATETIME,
    INDEX idx_jobs_status (status),
    INDEX idx_jobs_created_by (created_by),
    INDEX idx_jobs_created_at (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS job_items (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    job_id CHAR(36) NOT NULL,
    cedula VARCHAR(50) NOT NULL,
    status ENUM('PENDING', 'PROCESSING', 'COMPLETED', 'FAILED') NOT NULL DEFAULT 'PENDING',
    error_message TEXT,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    processed_at DATETIME,
    FOREIGN KEY (job_id) REFERENCES jobs(id) ON DELETE CASCADE,
    INDEX idx_job_items_job_status (job_id, status),
    INDEX idx_job_items_cedula (cedula)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS job_results (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    job_id CHAR(36) NOT NULL,
    job_item_id BIGINT UNSIGNED,
    cedula VARCHAR(50) NOT NULL,
    result_json JSON NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (job_id) REFERENCES jobs(id) ON DELETE CASCADE,
    FOREIGN KEY (job_item_id) REFERENCES job_items(id) ON DELETE SET NULL,
    INDEX idx_job_results_job (job_id),
    INDEX idx_job_results_cedula (cedula)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS job_artifacts (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    job_id CHAR(36) NOT NULL,
    artifact_type ENUM('CSV', 'XLSX', 'JSON') NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    file_path VARCHAR(500) NOT NULL,
    file_size BIGINT UNSIGNED,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    expires_at DATETIME,
    FOREIGN KEY (job_id) REFERENCES jobs(id) ON DELETE CASCADE,
    INDEX idx_job_artifacts_job (job_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================
-- PRESET MANAGEMENT TABLES
-- ============================================

CREATE TABLE IF NOT EXISTS preset_definitions (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    preset_key VARCHAR(100) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    dataset VARCHAR(100) NOT NULL,
    is_hardcoded BOOLEAN NOT NULL DEFAULT FALSE,
    is_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    created_by VARCHAR(100),
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_preset_key (preset_key),
    INDEX idx_preset_enabled (is_enabled)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS preset_versions (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    preset_id INT UNSIGNED NOT NULL,
    version INT NOT NULL,
    ast_json JSON NOT NULL,
    compiled_sql TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_by VARCHAR(100),
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (preset_id) REFERENCES preset_definitions(id) ON DELETE CASCADE,
    UNIQUE KEY uk_preset_version (preset_id, version),
    INDEX idx_preset_versions_active (preset_id, is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS preset_allowed_schema (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    dataset VARCHAR(100) NOT NULL,
    table_name VARCHAR(100) NOT NULL,
    column_name VARCHAR(100),
    column_type VARCHAR(50),
    is_filterable BOOLEAN NOT NULL DEFAULT TRUE,
    is_sortable BOOLEAN NOT NULL DEFAULT TRUE,
    is_selectable BOOLEAN NOT NULL DEFAULT TRUE,
    display_name VARCHAR(200),
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_schema_table_column (dataset, table_name, column_name),
    INDEX idx_schema_dataset (dataset)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS allowed_operators (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    operator_key VARCHAR(20) NOT NULL UNIQUE,
    operator_sql VARCHAR(50) NOT NULL,
    description VARCHAR(200),
    requires_value BOOLEAN NOT NULL DEFAULT TRUE,
    is_enabled BOOLEAN NOT NULL DEFAULT TRUE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================
-- TEMPORARY TABLE FOR BULK ID PROCESSING
-- ============================================

-- This table is created per-job execution for bulk ID processing
-- Template: CREATE TEMPORARY TABLE tmp_ids_{job_id} (cedula VARCHAR(50) PRIMARY KEY);

-- ============================================
-- RECOMMENDED INDEXES FOR EXISTING TABLES
-- ============================================

-- Run these on your existing tables:
-- ALTER TABLE tss ADD INDEX idx_tss_cedula_fecha (CEDULA, FECHA DESC);
-- ALTER TABLE tss ADD INDEX idx_tss_rnc_fecha (RNC, FECHA DESC);
-- ALTER TABLE ilocalizadosappsprocessor ADD INDEX idx_iloc_cedula_fecha (Cedula, FechaConsulta DESC);
-- ALTER TABLE vehi ADD INDEX idx_vehi_f19 (f19);
