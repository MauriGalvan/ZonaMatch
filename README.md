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

3. Cargar los datos GIS (schema geo)

Los datos de partidos, comunas, radios censales y escuelas vienen en el archivo zonamatch.sql (se comparte aparte, no está en el repositorio). Se restauran con:

docker-compose exec -T db psql -U zonamatch -d zonamatch_docker < zonamatch.sql

El dump crea sus tablas en un schema llamado zm. Renombralo para que quede como geo:

docker-compose exec db psql -U zonamatch -d zonamatch_docker -c "ALTER SCHEMA zm RENAME TO geo;"

Pueden aparecer errores inofensivos (comandos \restrict, transaction_timeout y filas duplicadas de spatial_ref_sys).

4. Ejecutar las Migraciones (Entity Framework, schema app)

Entity Framework solo administra el schema app (el modelo propio de la aplicación). Las tablas de geo y osm no se migran. Desde la Consola del Administrador de paquetes de Visual Studio:

Update-Database


5. Cargar los datos de OpenStreetMap (schema osm)

Para poblar la base con la cartografía de CABA y GBA, un script descarga los datos de OpenStreetMap y los inyecta en el schema osm.

Navegá a la carpeta raíz del proyecto desde tu explorador de archivos de Windows.

Hacé doble clic en el archivo OSM-DATA-UPDATER-DOCKER.bat.

Aguardá a que finalice: El proceso descargará el mapa completo de Argentina (~400MB), recortará la zona del AMBA y cargará las geometrías en la base de datos. Tomará unos minutos dependiendo de tu conexión.


Esquemas de la base: app (modelo de la aplicación, EF Core), geo (datos GIS externos), osm (importación cruda de OpenStreetMap). PostGIS vive en public.
