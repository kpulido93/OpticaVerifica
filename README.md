# Optima Verifica

Plataforma web escalable para ejecutar consultas "presets" basadas en CÉDULA con exportación (CSV/XLSX/JSON) y un diseñador visual de presets para administradores.

## Stack Tecnológico

- **Backend API**: ASP.NET Core 8 Minimal API + Dapper + MySqlConnector
- **Worker**: .NET HostedService para jobs en background
- **Frontend**: Next.js 14 + React + TypeScript + Tailwind CSS
- **Base de Datos**: MySQL 8.0
- **Contenedores**: Docker Compose

## Estructura del Proyecto

```
optima-verifica/
├── api/                    # ASP.NET Core 8 API
│   ├── Controllers/
│   ├── Services/
│   ├── Models/
│   └── Program.cs
├── worker/                 # Background Worker Service
├── frontend/               # Next.js Frontend
│   ├── src/
│   │   ├── app/
│   │   ├── components/
│   │   └── lib/
│   └── package.json
├── db/
│   ├── migrations/         # SQL Migrations
│   └── seeds/              # Initial Data
├── scripts/                # Utility scripts
├── docker-compose.yml
└── .env.example
```

## Configuración Rápida

1. **Copiar variables de entorno**
   ```bash
   cp .env.example .env
   ```

2. **Iniciar servicios**
   ```bash
   docker-compose up -d
   ```

3. **Aplicar migraciones**
   ```bash
   docker-compose exec api dotnet ef database update
   # O ejecutar scripts SQL manualmente
   ```

4. **Acceder a la aplicación**
   - Frontend: http://localhost:3000
   - API: http://localhost:5000/api
   - API Docs: http://localhost:5000/swagger

## Variables de Entorno

| Variable | Descripción | Ejemplo |
|----------|-------------|---------|
| `MYSQL_HOST` | Host de MySQL | `mysql` |
| `MYSQL_PORT` | Puerto de MySQL | `3306` |
| `MYSQL_DATABASE` | Base de datos | `neon_templaris` |
| `MYSQL_USER` | Usuario | `optima_user` |
| `MYSQL_PASSWORD` | Contraseña | `***` |
| `AUTH_ADMIN_USER` | Usuario admin | `admin` |
| `AUTH_ADMIN_PASSWORD` | Password admin | `***` |
| `AUTH_OPERATOR_USER` | Usuario operador | `operator` |
| `AUTH_OPERATOR_PASSWORD` | Password operador | `***` |

## Roles y Permisos

| Rol | Permisos |
|-----|----------|
| **ADMIN** | Todo + Preset Designer + Gestión de esquemas |
| **OPERATOR** | Crear jobs + Ver resultados + Exportar |
| **READER** | Ver jobs propios + Ver resultados |

## Presets V1 (Hardcoded)

1. **tss_top5_por_cedula**: Últimos 5 registros TSS por cédula
2. **companeros_salario_similar_top10**: Top 10 compañeros con salario similar
3. **vehiculo_existe_y_listado**: Verifica existencia en tabla vehi

## Seguridad

- ❌ NO se permite SQL libre del usuario final
- ✅ Todos los presets usan whitelist de tablas/columnas
- ✅ Parámetros siempre parametrizados (sin concatenación)
- ✅ IDs validados: trim, deduplicación, límite máximo
- ✅ Bulk IDs via tabla temporal + JOIN (no IN gigante)

## Arquitectura de Ejecución

```
Usuario → Selecciona Preset → Carga Cédulas → Crea Job
                                                  ↓
                                            Worker procesa
                                                  ↓
                                   AST → SQL Seguro (whitelist)
                                                  ↓
                                       Resultados paginados
                                                  ↓
                                          Export CSV/XLSX/JSON
```

## Licencia

Propietario - Todos los derechos reservados
