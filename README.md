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

3. Cargar los datos (schemas geo y osm) con un solo script

Copiá el archivo zonamatch.sql (se comparte aparte, no está en el repositorio) en la carpeta raíz del proyecto, al lado de OSM-DATA-UPDATER-DOCKER.bat, y hacé doble clic en el .bat. El script, en orden:

- levanta la base con docker compose y espera a que esté lista,
- crea los schemas app, geo y osm si no existen,
- restaura zonamatch.sql y renombra el schema zm a geo (si geo ya tiene tablas, omite este paso; si no encuentra zonamatch.sql, avisa y sigue),
- aplica las migraciones de EF (dotnet ef database update; instala dotnet-ef si falta y omite el paso si no hay carpeta Migrations),
- descarga el mapa de Argentina (~400MB), recorta el AMBA y lo importa al schema osm.

Los errores inofensivos del restore (\restrict, transaction_timeout y filas duplicadas de spatial_ref_sys) quedan en restore-geo.log. Es seguro volver a ejecutarlo para actualizar solo los datos de OSM.

4. Migraciones de Entity Framework (schema app)

Ya las aplica el script del paso 3. Si preferís hacerlo a mano, desde la Consola del Administrador de paquetes de Visual Studio: Update-Database. Entity Framework solo administra el schema app; las tablas de geo y osm no se migran.

5. Datos abiertos de las zonas (censo, seguridad y transporte)

Con el mapa de OSM ya importado (paso 3), hacé doble clic en DATOS-ZONAS-DOCKER.bat. Necesita Python 3 (instala el paquete duckdb si falta). El script:

- descarga a la carpeta datos\ (no se sube al repositorio) el Censo 2022 de INDEC por radio censal para el AMBA (población, hogares, internet, computadora, NBI, hacinamiento, alquiler, cobertura de salud, educación) los delitos 2024 del Mapa del Delito de CABA y los GTFS de colectivos, trenes y subte (Buenos Aires Data, de los que se guarda solo un resumen de salidas por parada y franja horaria),
- los carga en el schema geo y asigna cada radio censal y cada delito, por ubicación, a las zonas de OSM que lo contienen. Las comunas y los partidos suman lo de su superficie,
- prepara el motor de ruteo MOTIS (servicio motis del docker-compose, puerto 18080; se cambia con la variable MOTIS_PORT), que calcula tiempos de viaje a pie, en bici, en auto y en transporte público con el mapa del AMBA y los mismos GTFS. Los importa una sola vez al volumen motis_data; para reimportarlo (por ejemplo con GTFS nuevos), ejecutá DATOS-ZONAS-DOCKER.bat reimportar.

Tarda unos minutos y se puede volver a ejecutar. Los datos se consultan en GET /Zonas/{slug}/indicadores y GET /Zonas/{slug}/transporte. Fuera de CABA todavía no hay delitos georreferenciados.

Los GTFS públicos son de 2019 (colectivos y subte) y 2020 (trenes) y sus fechas no se superponen, así que MOTIS los usa con un calendario semanal proyectado a una semana de referencia (2 al 8 de marzo de 2026, ver etl\motis\config.yml): los viajes en transporte público se consultan en esas fechas. Hasta que se corre la importación, el contenedor de MOTIS queda esperando sin fallar.

Esquemas de la base: app (modelo de la aplicación, EF Core), geo (datos GIS externos), osm (importación cruda de OpenStreetMap). PostGIS vive en public.
