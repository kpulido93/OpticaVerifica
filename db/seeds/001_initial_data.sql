-- ============================================
-- OPTIMA VERIFICA - Initial Data Seeds
-- Version: 001
-- ============================================

-- ============================================
-- ALLOWED OPERATORS
-- ============================================

INSERT INTO allowed_operators (operator_key, operator_sql, description, requires_value, is_enabled) VALUES
('eq', '=', 'Igual a', TRUE, TRUE),
('neq', '!=', 'Diferente de', TRUE, TRUE),
('gt', '>', 'Mayor que', TRUE, TRUE),
('gte', '>=', 'Mayor o igual que', TRUE, TRUE),
('lt', '<', 'Menor que', TRUE, TRUE),
('lte', '<=', 'Menor o igual que', TRUE, TRUE),
('in', 'IN', 'En lista de valores', TRUE, TRUE),
('not_in', 'NOT IN', 'No en lista de valores', TRUE, TRUE),
('between', 'BETWEEN', 'Entre dos valores', TRUE, TRUE),
('like', 'LIKE', 'Contiene (con comodines)', TRUE, TRUE),
('starts_with', 'LIKE', 'Comienza con', TRUE, TRUE),
('ends_with', 'LIKE', 'Termina con', TRUE, TRUE),
('is_null', 'IS NULL', 'Es nulo', FALSE, TRUE),
('is_not_null', 'IS NOT NULL', 'No es nulo', FALSE, TRUE)
ON DUPLICATE KEY UPDATE description = VALUES(description);

-- ============================================
-- ALLOWED SCHEMA - Dataset: neon_templaris
-- ============================================

-- Table: tss
INSERT INTO preset_allowed_schema (dataset, table_name, column_name, column_type, is_filterable, is_sortable, is_selectable, display_name) VALUES
('neon_templaris', 'tss', 'id', 'INT', FALSE, TRUE, TRUE, 'ID'),
('neon_templaris', 'tss', 'RNC', 'VARCHAR', TRUE, TRUE, TRUE, 'RNC'),
('neon_templaris', 'tss', 'CEDULA', 'VARCHAR', TRUE, TRUE, TRUE, 'Cédula'),
('neon_templaris', 'tss', 'FECHA', 'DATE', TRUE, TRUE, TRUE, 'Fecha'),
('neon_templaris', 'tss', 'SALARIO', 'DECIMAL', TRUE, TRUE, TRUE, 'Salario'),
('neon_templaris', 'tss', 'EsOficial', 'BIT', TRUE, TRUE, TRUE, 'Es Oficial')
ON DUPLICATE KEY UPDATE display_name = VALUES(display_name);

-- Table: ilocalizadosappsprocessor
INSERT INTO preset_allowed_schema (dataset, table_name, column_name, column_type, is_filterable, is_sortable, is_selectable, display_name) VALUES
('neon_templaris', 'ilocalizadosappsprocessor', 'Id', 'INT', FALSE, TRUE, TRUE, 'ID'),
('neon_templaris', 'ilocalizadosappsprocessor', 'Cedula', 'VARCHAR', TRUE, TRUE, TRUE, 'Cédula'),
('neon_templaris', 'ilocalizadosappsprocessor', 'Nombre', 'VARCHAR', TRUE, TRUE, TRUE, 'Nombre'),
('neon_templaris', 'ilocalizadosappsprocessor', 'Telefono', 'VARCHAR', TRUE, FALSE, TRUE, 'Teléfono'),
('neon_templaris', 'ilocalizadosappsprocessor', 'Estado', 'VARCHAR', TRUE, TRUE, TRUE, 'Estado'),
('neon_templaris', 'ilocalizadosappsprocessor', 'FechaConsulta', 'DATETIME', TRUE, TRUE, TRUE, 'Fecha Consulta')
ON DUPLICATE KEY UPDATE display_name = VALUES(display_name);

-- Table: telefonos_respaldo
INSERT INTO preset_allowed_schema (dataset, table_name, column_name, column_type, is_filterable, is_sortable, is_selectable, display_name) VALUES
('neon_templaris', 'telefonos_respaldo', 'id', 'BIGINT', FALSE, TRUE, TRUE, 'ID'),
('neon_templaris', 'telefonos_respaldo', 'cedula', 'VARCHAR', TRUE, TRUE, TRUE, 'Cédula'),
('neon_templaris', 'telefonos_respaldo', 'telefono', 'VARCHAR', TRUE, FALSE, TRUE, 'Teléfono')
ON DUPLICATE KEY UPDATE display_name = VALUES(display_name);

-- Table: vehi (vehículos)
INSERT INTO preset_allowed_schema (dataset, table_name, column_name, column_type, is_filterable, is_sortable, is_selectable, display_name) VALUES
('neon_templaris', 'vehi', 'f1', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 1'),
('neon_templaris', 'vehi', 'f2', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 2'),
('neon_templaris', 'vehi', 'f3', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 3'),
('neon_templaris', 'vehi', 'f4', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 4'),
('neon_templaris', 'vehi', 'f5', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 5'),
('neon_templaris', 'vehi', 'f6', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 6'),
('neon_templaris', 'vehi', 'f7', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 7'),
('neon_templaris', 'vehi', 'f8', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 8'),
('neon_templaris', 'vehi', 'f9', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 9'),
('neon_templaris', 'vehi', 'f10', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 10'),
('neon_templaris', 'vehi', 'f11', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 11'),
('neon_templaris', 'vehi', 'f12', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 12'),
('neon_templaris', 'vehi', 'f13', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 13'),
('neon_templaris', 'vehi', 'f14', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 14'),
('neon_templaris', 'vehi', 'f15', 'VARCHAR', TRUE, TRUE, TRUE, 'Campo 15'),
('neon_templaris', 'vehi', 'f19', 'VARCHAR', TRUE, TRUE, TRUE, 'Cédula (f19)')
ON DUPLICATE KEY UPDATE display_name = VALUES(display_name);

-- ============================================
-- HARDCODED PRESETS V1
-- ============================================

INSERT INTO preset_definitions (preset_key, name, description, dataset, is_hardcoded, is_enabled, created_by) VALUES
('tss_top5_por_cedula', 'TSS - Últimos 5 registros', 'Obtiene los últimos 5 registros de TSS para una cédula, ordenados por fecha descendente', 'neon_templaris', TRUE, TRUE, 'SYSTEM'),
('companeros_salario_similar_top10', 'Compañeros con salario similar', 'Encuentra los 10 compañeros de trabajo (mismo RNC) con salario similar dentro de un porcentaje de tolerancia', 'neon_templaris', TRUE, TRUE, 'SYSTEM'),
('vehiculo_existe_y_listado', 'Vehículos por cédula', 'Verifica si existe un vehículo registrado y lista los primeros 50 registros', 'neon_templaris', TRUE, TRUE, 'SYSTEM')
ON DUPLICATE KEY UPDATE description = VALUES(description);

-- Insert preset versions with AST JSON
INSERT INTO preset_versions (preset_id, version, ast_json, is_active, created_by)
SELECT id, 1, JSON_OBJECT(
    'type', 'HARDCODED',
    'handler', 'TssTop5Handler',
    'description', 'Últimos 5 registros TSS por FECHA DESC',
    'inputs', JSON_ARRAY(
        JSON_OBJECT('name', 'cedula', 'type', 'string', 'required', TRUE)
    ),
    'outputs', JSON_ARRAY('id', 'RNC', 'CEDULA', 'FECHA', 'SALARIO', 'EsOficial')
), TRUE, 'SYSTEM'
FROM preset_definitions WHERE preset_key = 'tss_top5_por_cedula'
ON DUPLICATE KEY UPDATE ast_json = VALUES(ast_json);

INSERT INTO preset_versions (preset_id, version, ast_json, is_active, created_by)
SELECT id, 1, JSON_OBJECT(
    'type', 'HARDCODED',
    'handler', 'CompanerosSalarioSimilarHandler',
    'description', 'Top 10 compañeros del mismo RNC con salario similar',
    'inputs', JSON_ARRAY(
        JSON_OBJECT('name', 'cedula', 'type', 'string', 'required', TRUE),
        JSON_OBJECT('name', 'tolerancePct', 'type', 'decimal', 'required', FALSE, 'default', 0.10)
    ),
    'outputs', JSON_ARRAY('CEDULA', 'Nombre', 'Telefono', 'RNC', 'SALARIO', 'FECHA', 'SalaryDiff')
), TRUE, 'SYSTEM'
FROM preset_definitions WHERE preset_key = 'companeros_salario_similar_top10'
ON DUPLICATE KEY UPDATE ast_json = VALUES(ast_json);

INSERT INTO preset_versions (preset_id, version, ast_json, is_active, created_by)
SELECT id, 1, JSON_OBJECT(
    'type', 'HARDCODED',
    'handler', 'VehiculoExisteHandler',
    'description', 'Verifica existencia y lista vehículos',
    'inputs', JSON_ARRAY(
        JSON_OBJECT('name', 'cedula', 'type', 'string', 'required', TRUE)
    ),
    'outputs', JSON_ARRAY('existe', 'vehiculos')
), TRUE, 'SYSTEM'
FROM preset_definitions WHERE preset_key = 'vehiculo_existe_y_listado'
ON DUPLICATE KEY UPDATE ast_json = VALUES(ast_json);
