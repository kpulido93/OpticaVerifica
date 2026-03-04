# Optima Verifica - PRD (Product Requirements Document)

## Original Problem Statement
Construir una plataforma web escalable por módulos para ejecutar consultas "presets" basadas en CÉDULA (ID principal) y mostrar resultados en UI con opción de exportar (CSV/XLSX/JSON). Incluir un "Preset Designer" visual SOLO para ADMIN donde se puedan seleccionar tablas/vistas permitidas, arrastrar columnas y definir filtros/ordenamientos sin escribir SQL.

## Architecture
- **Backend**: ASP.NET Core 8 Minimal API + Dapper + MySqlConnector
- **Worker**: .NET HostedService para procesamiento background de jobs
- **Frontend**: Next.js 14 + React + TypeScript + Tailwind CSS
- **Database**: MySQL 8.0
- **Containerization**: Docker Compose

## User Personas
1. **ADMIN**: Acceso total + Diseñador de Presets + Gestión de esquemas
2. **OPERATOR**: Crear jobs + Ver resultados + Exportar
3. **READER**: Ver jobs propios + Ver resultados (sin crear)

## Core Requirements (Static)
- ❌ NO se permite SQL libre del usuario final
- ✅ Todos los presets usan whitelist de tablas/columnas
- ✅ Parámetros siempre parametrizados (sin concatenación)
- ✅ IDs validados: trim, deduplicación, límite máximo
- ✅ Bulk IDs via tabla temporal + JOIN (no IN gigante)

## What's Been Implemented (2026-01-04)

### Backend API (ASP.NET Core 8)
- [x] Basic Authentication con roles (ADMIN/OPERATOR/READER)
- [x] Endpoints de Presets (GET /api/presets, GET /api/presets/{key})
- [x] Endpoints de Jobs (POST, GET, Results, Export, Cancel)
- [x] Endpoints Admin (Schema, Datasets, Presets CRUD, Test)
- [x] Servicios: PresetService, JobService, SchemaService, ExportService, PresetExecutor
- [x] Modelo AST para presets personalizados
- [x] Compilador AST -> SQL con whitelist validation

### Worker Service
- [x] BackgroundService para procesar jobs en cola
- [x] Procesamiento por lotes (batch)
- [x] Handlers hardcodeados para presets V1

### Frontend (Next.js 14)
- [x] Login con Basic Auth
- [x] Dashboard con carga de cédulas (textarea + file upload)
- [x] Selector de presets
- [x] Lista de Jobs con auto-refresh
- [x] Detalle de Job con resultados paginados
- [x] Export a CSV/XLSX/JSON
- [x] Preset Designer visual (ADMIN only)
- [x] Tema oscuro "Cyber-Swiss"

### Database
- [x] Migraciones SQL (jobs, job_items, job_results, presets, schema)
- [x] Seeds de datos iniciales (operadores, schema permitido, presets V1)
- [x] Datos demo para testing

### 3 Presets V1 (Hardcoded)
1. `tss_top5_por_cedula` - Últimos 5 registros TSS
2. `companeros_salario_similar_top10` - Compañeros con salario similar
3. `vehiculo_existe_y_listado` - Vehículos por cédula

### Infrastructure
- [x] Docker Compose (mysql, api, worker, frontend)
- [x] Scripts de setup
- [x] Unit tests para AST compiler

## Prioritized Backlog

### P0 (Critical - Bloqueantes)
- [ ] Conectar frontend a API real (actualmente demo mode)
- [ ] Integrar Worker con el sistema de jobs
- [ ] Probar flujo completo end-to-end

### P1 (High Priority)
- [ ] Compilador AST->SQL completo con JOINs
- [ ] Guardar presets custom desde Designer a DB
- [ ] Paginación server-side optimizada
- [ ] Índices recomendados en tablas existentes

### P2 (Medium Priority)
- [ ] Provider Sigeinfo (estructura base, estado PAUSED_BY_SCHEDULE)
- [ ] Ventana horaria 08:00-18:00 República Dominicana
- [ ] Migración a JWT/Session auth
- [ ] Logs y auditoría de queries

### Future/Backlog
- [ ] Dashboard de métricas (jobs/día, tiempo promedio)
- [ ] Notificaciones cuando job completa
- [ ] Scheduler para jobs recurrentes
- [ ] API rate limiting
- [ ] Cache de resultados frecuentes

## Next Tasks
1. Levantar Docker Compose localmente
2. Ejecutar migraciones en MySQL
3. Probar los 3 presets V1 con cédulas demo
4. Conectar frontend a API real
5. Testing end-to-end del flujo completo
