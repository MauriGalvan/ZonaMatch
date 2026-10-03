@echo off
echo ==========================================
echo Actualizando datos espaciales de ZonaMatch
echo Entorno: Docker Compose
echo ==========================================

echo.
echo 1. Descargando el ultimo mapa de Argentina...
curl -L -O https://download.geofabrik.de/south-america/argentina-latest.osm.pbf

echo.
echo 2. Recortando el area de CABA y Conurbano...
docker run --rm -v "%cd%":/data -w /data ubuntu:22.04 bash -c "apt-get update > /dev/null && apt-get install -y osmium-tool > /dev/null && osmium extract -b -58.99,-34.93,-58.01,-34.33 argentina-latest.osm.pbf -o amba.osm.pbf"

echo.
echo 3. Importando los datos al contenedor de PostgreSQL...
docker run --rm -v "%cd%":/data -e PGPASSWORD=zonamatch -e DEBIAN_FRONTEND=noninteractive ubuntu:22.04 bash -c "apt-get update > /dev/null && apt-get install -y osm2pgsql > /dev/null && osm2pgsql -c --hstore --multi-geometry -d zonamatch_docker -U zonamatch -H host.docker.internal -P 5433 /data/amba.osm.pbf"

echo.
echo 4. Limpiando archivos temporales...
del argentina-latest.osm.pbf
del amba.osm.pbf

echo.
echo ==========================================
echo Carga finalizada con exito.
echo ==========================================
pause