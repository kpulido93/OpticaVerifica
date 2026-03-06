# Optima Verifica

Este repositorio corresponde a **Optima Verifica**.

## Quickstart

```bash
cp .env.example .env
docker compose up -d --build
```

## Basic Auth con roles

La API usa autenticación **Basic Auth** con 3 roles:

- `ADMIN`: acceso total.
- `OPERATOR`: puede crear y gestionar jobs operativos.
- `READER`: solo lectura.

Por ahora puedes operar únicamente con credenciales de `ADMIN`; las cuentas `OPERATOR` y `READER` se mantienen disponibles para habilitación posterior.

Variables de autenticación esperadas en `.env`:

- `AUTH_ADMIN_USER`
- `AUTH_ADMIN_PASSWORD`
- `AUTH_OPERATOR_USER`
- `AUTH_OPERATOR_PASSWORD`
- `AUTH_READER_USER`
- `AUTH_READER_PASSWORD`

### Roles y variables `AUTH_*`

- Cada rol usa un par `USER` + `PASSWORD`:
  - `ADMIN`: `AUTH_ADMIN_USER`, `AUTH_ADMIN_PASSWORD`
  - `OPERATOR`: `AUTH_OPERATOR_USER`, `AUTH_OPERATOR_PASSWORD`
  - `READER`: `AUTH_READER_USER`, `AUTH_READER_PASSWORD`
- Si una variable de usuario o contraseña está vacía para un rol, ese rol queda deshabilitado.
- En `Production`, `ADMIN` es obligatorio: la API no arranca si faltan `AUTH_ADMIN_USER` o `AUTH_ADMIN_PASSWORD`.

### Mapeo en docker-compose

`docker-compose.yml` lee variables `AUTH_*` desde `.env` y las mapea a configuración .NET `Auth__*`:

- `AUTH_ADMIN_USER` -> `Auth__AdminUser`
- `AUTH_ADMIN_PASSWORD` -> `Auth__AdminPassword`
- `AUTH_OPERATOR_USER` -> `Auth__OperatorUser`
- `AUTH_OPERATOR_PASSWORD` -> `Auth__OperatorPassword`
- `AUTH_READER_USER` -> `Auth__ReaderUser`
- `AUTH_READER_PASSWORD` -> `Auth__ReaderPassword`

## ENV

Usa `.env.example` como base. Incluye, entre otras, estas variables:

- `TZ=America/Santo_Domingo`
- `MYSQL_HOST`, `MYSQL_PORT`, `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD`, `MYSQL_ROOT_PASSWORD`
- `API_PORT`, `API_ENVIRONMENT`
- `NEXT_PUBLIC_API_URL`, `NEXT_PUBLIC_APP_NAME`
- `WORKER_MAX_CONCURRENT_JOBS`, `WORKER_BATCH_SIZE`, `WORKER_MAX_IDS_PER_JOB`
- `CORS_ALLOWED_ORIGINS`
- `RATE_LIMIT_PERMIT_LIMIT`, `RATE_LIMIT_WINDOW_SECONDS`, `RATE_LIMIT_QUEUE_LIMIT`

Reglas de hardening en API:

- `CORS_ALLOWED_ORIGINS` se interpreta como CSV (`https://app.example.com,https://admin.example.com`).
- En `Development`, si no defines `CORS_ALLOWED_ORIGINS`, se permiten por defecto `http://localhost:3000` y `http://127.0.0.1:3000`.
- Fuera de `Development`, la API falla al iniciar si no existe `CORS_ALLOWED_ORIGINS` (fail-fast).
- Rate limit FixedWindow para rutas `/api/*`:
  - `RATE_LIMIT_PERMIT_LIMIT` (default `60`).
  - `RATE_LIMIT_WINDOW_SECONDS` (default `60`).
  - `RATE_LIMIT_QUEUE_LIMIT` (default `0`).
- Se habilitan `X-Forwarded-For` y `X-Forwarded-Proto` para despliegues detrás de proxy/reverse proxy.
- Se aplica `UseHttpsRedirection()` fuera de `Development` y `UseHsts()` en `Production`.

## API

Las políticas de autorización disponibles son:

- `AdminOnly`: `ADMIN`
- `OperatorOrAbove`: `ADMIN`, `OPERATOR`
- `AnyRole`: `ADMIN`, `OPERATOR`, `READER`

### Health checks

- `GET /health/live`: liveness del proceso (siempre `200 OK` cuando la API está levantada).
- `GET /health/ready`: readiness con validación de MySQL (`SELECT 1`).
- Alias mantenidos:
  - `GET /health` -> liveness
  - `GET /api/health` -> readiness

## Migraciones de base de datos (automáticas)

El proyecto usa migraciones SQL por scripts en `db/migrations/` y un servicio dedicado `src/Migrator` (DbUp + Dapper-friendly) para ejecutarlas **automáticamente** en cada arranque de stack.

Flujo en `docker-compose`:

1. `mysql` levanta y espera healthcheck en estado saludable.
2. `migrator` ejecuta todos los scripts pendientes en orden alfabético (`001_`, `002_`, `003_`, ...).
3. DbUp registra scripts aplicados en su tabla de versionado y no re-ejecuta scripts ya aplicados.
4. `api` y `worker` arrancan **solo** si `migrator` termina con éxito (`service_completed_successfully`).

### Estructura y convención

- Carpeta de scripts: `db/migrations/`
- Nombres recomendados: `NNN_descripcion.sql` (ejemplos: `001_initial_schema.sql`, `002_performance_indexes.sql`, `003_add_x_table.sql`).
- No editar scripts ya aplicados en ambientes compartidos; crear un nuevo script incremental.

### Cómo agregar una nueva migración

1. Crea un nuevo archivo SQL en `db/migrations/` con el siguiente prefijo numérico.
2. Agrega solo cambios incrementales (DDL/DML de migración).
3. Al levantar el stack, el servicio `migrator` la aplicará automáticamente y dejará trazabilidad en la tabla de versionado de DbUp.

## Licencia

Este proyecto se distribuye bajo la licencia **GNU General Public License v3.0 (GPL-3.0)**.
Consulta el archivo `LICENSE` para el texto completo.
