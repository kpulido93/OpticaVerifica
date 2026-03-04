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

## Licencia

Este proyecto se distribuye bajo la licencia **GNU General Public License v3.0 (GPL-3.0)**.
Consulta el archivo `LICENSE` para el texto completo.
