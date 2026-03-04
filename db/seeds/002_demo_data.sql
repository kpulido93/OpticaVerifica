-- ============================================
-- OPTIMA VERIFICA - Demo Data for Testing
-- ============================================

-- Sample TSS records
INSERT INTO tss (RNC, CEDULA, FECHA, SALARIO, EsOficial) VALUES
('401500001', '00100000001', '2024-01-15', 45000.00, 1),
('401500001', '00100000001', '2024-02-15', 46000.00, 1),
('401500001', '00100000001', '2024-03-15', 46500.00, 1),
('401500001', '00100000002', '2024-01-15', 44000.00, 1),
('401500001', '00100000002', '2024-02-15', 44500.00, 1),
('401500001', '00100000003', '2024-01-15', 48000.00, 1),
('401500002', '00100000004', '2024-01-15', 55000.00, 1),
('401500002', '00100000005', '2024-01-15', 52000.00, 1);

-- Sample ilocalizadosappsprocessor records
INSERT INTO ilocalizadosappsprocessor (Cedula, Nombre, Telefono, Estado, FechaConsulta) VALUES
('00100000001', 'Juan Pérez García', '809-555-0001', 'Activo', '2024-03-01 10:00:00'),
('00100000002', 'María López Santos', '809-555-0002', 'Activo', '2024-03-01 10:00:00'),
('00100000003', 'Carlos Rodríguez Díaz', '809-555-0003', 'Activo', '2024-03-01 10:00:00'),
('00100000004', 'Ana Martínez Ruiz', '809-555-0004', 'Activo', '2024-03-01 10:00:00'),
('00100000005', 'Pedro González Vega', '809-555-0005', 'Activo', '2024-03-01 10:00:00');

-- Sample vehi records
INSERT INTO vehi (f1, f2, f3, f4, f5, f6, f7, f8, f9, f10, f11, f12, f13, f14, f15, f19) VALUES
('Toyota', 'Corolla', '2022', 'Sedán', 'Blanco', 'G123456', 'Activo', '', '', '', '', '', '', '', '', '00100000001'),
('Honda', 'Civic', '2021', 'Sedán', 'Negro', 'G234567', 'Activo', '', '', '', '', '', '', '', '', '00100000001'),
('Hyundai', 'Tucson', '2023', 'SUV', 'Gris', 'G345678', 'Activo', '', '', '', '', '', '', '', '', '00100000004');

-- Sample telefonos_respaldo records
INSERT INTO telefonos_respaldo (cedula, telefono) VALUES
('00100000001', '809-555-1001'),
('00100000001', '829-555-1002'),
('00100000002', '809-555-2001');
