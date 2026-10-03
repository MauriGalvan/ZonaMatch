🚀 Guía de Instalación y Ejecución

Seguí estos pasos en orden para configurar el entorno de desarrollo local:

1. Clonar el repositorio

git clone https://github.com/MauriGalvan/ZonaMatch.git
cd ZonaMatch


2. Levantar la Base de Datos (Docker)

El proyecto incluye un archivo docker-compose.yml configurado con la imagen de PostgreSQL + PostGIS.
Abrí una terminal en la raíz del proyecto y ejecutá:

docker-compose up -d

Puerto : La base de datos se expone en el puerto local 5433 (para evitar conflictos con instalaciones locales de PostgreSQL).

3. Ejecutar las Migraciones (Entity Framework)

Para crear el modelo canónico (esquema `zonamatch`) y habilitar las extensiones `postgis` y `hstore`, abrí la solución en Visual Studio, andá a Herramientas > Administrador de paquetes NuGet > Consola del Administrador de paquetes y ejecutá:

Update-Database

Desde una terminal también se puede: `dotnet tool restore` y `dotnet ef database update --project ZonaMatch.Infrastructure --startup-project ZonaMatch.Api`.
El mismo esquema está como script SQL idempotente en [`db/ddl/zonamatch_schema.sql`](db/ddl/zonamatch_schema.sql).

4. Cargar los Datos Espaciales (Mapas)

Para poblar la base de datos con la cartografía real de CABA y GBA, utilizamos un script automatizado que descarga los datos de OpenStreetMap y los inyecta en el contenedor.

Navegá a la carpeta raíz del proyecto desde tu explorador de archivos de Windows.

Hacé doble clic en el archivo OSM-DATA-UPDATER-DOCKER.bat.

Aguardá a que finalice: El proceso descargará el mapa completo de Argentina (~400MB), recortará la zona del AMBA y cargará las geometrías en la base de datos. Tomará unos minutos dependiendo de tu conexión.

> El script importa con `--hstore` (conserva todas las etiquetas de OSM) y `--multi-geometry`
> (un multipolígono por elemento). Requiere haber corrido las migraciones antes, porque ahí se
> crea la extensión `hstore`.

5. Restaurar la base compartida (`zm.*`)

Restaurá `BaseDatos-ZonaMatch/zonamatch.sql` sobre la misma base (comunas, partidos, radios censales y padrón de escuelas). Ver el README de esa carpeta.

6. Pasar las fuentes al modelo canónico

Con `Admin:IngestionEndpointsEnabled = true` (por ejemplo, la variable de entorno `Admin__IngestionEndpointsEnabled=true`), levantá la API y llamá en este orden:

```
POST /api/admin/ingestion/territorial   comunas, partidos, radios, barrios y localidades
POST /api/admin/ingestion/zones         zonas de análisis + adyacencias
POST /api/admin/ingestion/pois          padrón educativo + POIs de OSM
POST /api/admin/ingestion/deduplicate   vincula el mismo lugar informado por dos fuentes
```

Todo es idempotente: se puede volver a correr cada vez que se actualizan las fuentes.

## Arquitectura

| Proyecto | Responsabilidad |
|---|---|
| `ZonaMatch.Domain` | Entidades y reglas puras: unidades territoriales, zonas, POIs, normalización de geometrías y nombres, clasificación de POIs, deduplicación, motor de puntaje. |
| `ZonaMatch.Application` | Casos de uso (ingesta, consultas, ranking) y puertos (`I*Repository`, `I*SourceReader`, `IRoutingService`). |
| `ZonaMatch.Infrastructure` | EF Core + PostGIS, migraciones, repositorios, lectores de las tablas de origen y el estimador de tiempos (hasta tener OSRM). |
| `ZonaMatch.Api` | Controladores REST, manejo de errores (ProblemDetails), Swagger, CORS. |

Modelo de datos y decisiones: [`docs/01-relevamiento-unidades-territoriales.md`](docs/01-relevamiento-unidades-territoriales.md) y [`docs/02-modelo-datos-der.md`](docs/02-modelo-datos-der.md).

Cómo se lee cada fuente (tabla, columnas, filtros, reglas de padre) está en `appsettings.json`, sección `Ingestion`. Las categorías de POIs y las reglas que traducen cada fuente a esas categorías están en las tablas `poi_categories` y `poi_mapping_rules`.

## Tests

```
dotnet test
```

Corre las pruebas unitarias y las de integración (estas últimas levantan su propio PostGIS con Testcontainers, así que Docker tiene que estar corriendo). La cobertura es obligatoria: el comando falla si cualquier capa baja del 100 % de líneas, ramas o métodos. Las migraciones (código generado) se validan aplicándolas y revirtiéndolas sobre una base vacía.
