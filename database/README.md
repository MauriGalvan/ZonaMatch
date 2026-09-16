# Base de Datos Geoespacial - ZonaMatch (PostgreSQL + PostGIS)

Esta carpeta contiene la configuración para levantar la base de datos de ZonaMatch con soporte geoespacial nativo mediante **PostGIS**.

## 🚀 Inicio Rápido con Docker

1. Asegúrate de tener **Docker Desktop** abierto e iniciado.
2. Desde la raíz del proyecto (`c:\mau\Universidad\TPI`), ejecuta:

```powershell
docker compose up -d
```

3. Verifica que el contenedor esté corriendo:

```powershell
docker ps
```

El script de inicialización montado en `./database/init` se ejecutará automáticamente la primera vez creando:
* Extensiones `postgis`, `uuid-ossp` y `pg_trgm`.
* Tablas espaciales `locations`, `pois`, `weather_cache`, `user_profiles`, `user_destinations`, `zone_evaluations`.
* Índices espaciales `GiST` y funciones de consulta en metros (`fn_count_pois_in_radius`, `fn_nearest_poi_distance`).

---

## 🔑 Credenciales por Defecto (definidas en `.env`)

* **Host:** `localhost`
* **Puerto:** `5432`
* **Base de datos:** `zonamatch_db`
* **Usuario:** `zonamatch_user`
* **Contraseña:** `zonamatch_pass`

---

## 🗺️ Estructura de Tablas Espaciales

| Tabla | Propósito | Geometría | Fuente |
|---|---|---|---|
| `locations` | Zonas normalizadas y geocodificadas | `Point (4326)` | Georef Argentina |
| `pois` | Infraestructura, salud, colegios, súper, transporte | `Point (4326)` | Overpass (OpenStreetMap) |
| `weather_cache` | Caché climático por cuadrícula | `Point (4326)` | Open-Meteo |
| `user_destinations` | Destinos del usuario (trabajo, facultad) | `Point (4326)` | Usuario |
| `zone_evaluations` | Historial de scores y diagnósticos calculados | `Point (4326)` | Motor ZonaMatch Score |

---

## 🧪 Comandos Útiles de Verificación

Verificar extensiones activas:
```sql
SELECT extname, extversion FROM pg_extension;
```

Probar conteo de POIs en radio de 1000m:
```sql
SELECT * FROM fn_count_pois_in_radius(-34.6037, -58.3816, 1000);
```
