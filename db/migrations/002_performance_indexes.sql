-- Performance indexes for core preset scalability
-- Tarea 5

CREATE INDEX idx_tss_cedula_fecha ON tss (CEDULA, FECHA);
CREATE INDEX idx_tss_rnc_fecha ON tss (RNC, FECHA);

CREATE INDEX idx_ilocalizados_cedula_fecha_consulta ON ilocalizadosappsprocessor (Cedula, FechaConsulta);

CREATE INDEX idx_vehi_f19 ON vehi (f19);

CREATE INDEX idx_telefonos_respaldo_cedula ON telefonos_respaldo (cedula);
