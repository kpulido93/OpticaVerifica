-- Seed common operators used by schema-driven preset validation (idempotent)
INSERT INTO allowed_operators (operator_key, operator_sql, description, requires_value, is_enabled)
VALUES
    ('eq', '=', 'Equals', 1, 1),
    ('neq', '!=', 'Not equals', 1, 1),
    ('gt', '>', 'Greater than', 1, 1),
    ('gte', '>=', 'Greater than or equal', 1, 1),
    ('lt', '<', 'Less than', 1, 1),
    ('lte', '<=', 'Less than or equal', 1, 1),
    ('like', 'LIKE', 'Contains pattern', 1, 1),
    ('in', 'IN', 'Value in list', 1, 1),
    ('between', 'BETWEEN', 'Between two values', 1, 1),
    ('is_null', 'IS NULL', 'Is null', 0, 1),
    ('is_not_null', 'IS NOT NULL', 'Is not null', 0, 1),
    ('in_ids', 'IN', 'Bulk id filter', 1, 1)
ON DUPLICATE KEY UPDATE
    operator_sql = VALUES(operator_sql),
    description = VALUES(description),
    requires_value = VALUES(requires_value),
    is_enabled = VALUES(is_enabled);

-- Seed schema whitelist for core Optima tables from the current database metadata (idempotent)
INSERT INTO preset_allowed_schema
(
    dataset,
    table_name,
    column_name,
    column_type,
    is_filterable,
    is_sortable,
    is_selectable,
    display_name
)
SELECT
    'neon_templaris' AS dataset,
    c.table_name,
    c.column_name,
    c.column_type,
    1 AS is_filterable,
    1 AS is_sortable,
    1 AS is_selectable,
    NULL AS display_name
FROM information_schema.columns c
WHERE c.table_schema = DATABASE()
  AND c.table_name IN ('jobs', 'job_items', 'job_results', 'preset_definitions', 'preset_versions')
ON DUPLICATE KEY UPDATE
    column_type = VALUES(column_type),
    is_filterable = VALUES(is_filterable),
    is_sortable = VALUES(is_sortable),
    is_selectable = VALUES(is_selectable),
    display_name = VALUES(display_name);
