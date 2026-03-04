# Optima Veriifica

Este repositorio corresponde a **Optima Veriifica**.

## Modo de autenticación (solo ADMIN)

La aplicación está documentada para operar en modo **solo ADMIN** con **Basic Auth**.
Cualquier acceso autenticado se considera con permisos de administración.

Variables esperadas:

- `ADMIN_USER`: usuario administrador para Basic Auth.
- `ADMIN_PASS`: contraseña del administrador para Basic Auth.

## Configuración de entorno

Usa el archivo de ejemplo para variables de entorno:

```bash
cp .env.example .env
```

Variables incluidas en el ejemplo:

- `TZ=America/Santo_Domingo`
- `ADMIN_USER=admin`
- `ADMIN_PASS=admin123`

## API

Los endpoints bajo `/api/admin/*` deben requerir autenticación Basic Auth en modo solo ADMIN.

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
