@echo off
setlocal

REM Carga los datos abiertos que completan el detalle de las zonas (censo 2022, delitos de CABA y
REM frecuencias de transporte publico de los GTFS) en el schema geo, y prepara el motor de ruteo MOTIS
REM (tiempos de viaje a pie, en bici, en auto y en transporte publico). Requiere la base con el mapa de OSM
REM ya importado (OSM-DATA-UPDATER-DOCKER.bat), Python 3 y el paquete duckdb (pip install duckdb).
REM Es seguro volver a ejecutarlo: borra y recalcula las tablas. Si los CSV ya estan en datos\, no los vuelve
REM a descargar (borrarlos para actualizar). MOTIS se importa una sola vez; para reimportarlo (por ejemplo con
REM GTFS nuevos) ejecutar: DATOS-ZONAS-DOCKER.bat reimportar

set DB_USER=zonamatch
set DB_NAME=zonamatch_docker
set PSQL=docker compose exec -T db psql -U %DB_USER% -d %DB_NAME% -q -v ON_ERROR_STOP=1

echo ==========================================
echo Datos abiertos de las zonas de ZonaMatch
echo ==========================================

echo.
echo [1/5] Levantando la base de datos...
docker compose up -d
if errorlevel 1 goto :err_docker

set /a TRIES=0
:wait_db
docker compose exec -T db pg_isready -h 127.0.0.1 -U %DB_USER% -d %DB_NAME% >nul 2>&1
if not errorlevel 1 goto :db_ready
set /a TRIES+=1
if %TRIES% geq 45 goto :err_timeout
timeout /t 2 /nobreak >nul
goto :wait_db
:db_ready

echo.
echo [2/5] Descargando censo 2022, delitos de CABA y GTFS de transporte en datos\ ...
python -c "import duckdb" >nul 2>&1
if errorlevel 1 (
    echo Instalando duckdb...
    python -m pip install duckdb
    if errorlevel 1 goto :err_python
)
if exist "datos\censo_radios_amba.csv" if exist "datos\censo_indicadores_amba.csv" if exist "datos\delitos_2024.csv" (
    echo Censo y delitos ya descargados. Para actualizarlos, borrar sus CSV de datos\ y volver a ejecutar.
    goto :gtfs
)
python etl\descargar_datos.py
if errorlevel 1 goto :err_download

:gtfs
if exist "datos\transporte_salidas.csv" (
    echo Transporte ya procesado. Para actualizarlo, borrar datos\transporte_salidas.csv.
    goto :load
)
echo Resumiendo los GTFS de colectivos, trenes y subte (descarga unos 210 MB la primera vez)...
python etl\procesar_gtfs.py
if errorlevel 1 goto :err_download

:load
echo.
echo [3/5] Copiando los CSV a la base...
%PSQL% < etl\crear_tablas.sql
if errorlevel 1 goto :err_load
%PSQL% -c "\copy geo.carga_censo_radios FROM STDIN WITH (FORMAT csv, HEADER true)" < datos\censo_radios_amba.csv
if errorlevel 1 goto :err_load
%PSQL% -c "\copy geo.carga_censo_indicadores FROM STDIN WITH (FORMAT csv, HEADER true)" < datos\censo_indicadores_amba.csv
if errorlevel 1 goto :err_load
%PSQL% -c "\copy geo.carga_delitos FROM STDIN WITH (FORMAT csv, HEADER true, NULL 'NULL')" < datos\delitos_2024.csv
if errorlevel 1 goto :err_load
%PSQL% -c "\copy geo.carga_transporte_salidas FROM STDIN WITH (FORMAT csv, HEADER true)" < datos\transporte_salidas.csv
if errorlevel 1 goto :err_load

echo.
echo [4/5] Calculando los indicadores de cada zona (tarda alrededor de un minuto)...
%PSQL% < etl\calcular_zonas.sql
if errorlevel 1 goto :err_calc
%PSQL% < etl\calcular_transporte.sql
if errorlevel 1 goto :err_calc
rem Alquileres estimados por zona (temporal, hasta el scraping de Argenprop)
%PSQL% < etl\estimar_alquileres.sql
if errorlevel 1 goto :err_calc

echo.
echo [5/5] Preparando el motor de ruteo MOTIS...
set MOTIS_RUN=docker compose run --rm --no-deps --entrypoint /bin/sh motis -c
if /i "%~1"=="reimportar" goto :motis_entrada
%MOTIS_RUN% "test -f /data/importacion-ok" >nul 2>&1
if errorlevel 1 goto :motis_entrada
echo MOTIS ya importado. Para reimportarlo: DATOS-ZONAS-DOCKER.bat reimportar
goto :fin

:motis_entrada
if not exist "datos\motis" mkdir "datos\motis"
if exist "datos\motis\amba.osm.pbf" goto :motis_gtfs
if exist "amba.osm.pbf" (
    copy /y amba.osm.pbf datos\motis\ >nul
    goto :motis_gtfs
)
rem El mismo recorte del AMBA que OSM-DATA-UPDATER-DOCKER.bat
echo Descargando el mapa de Argentina (unos 430 MB) para recortar el AMBA...
curl -L -o datos\motis\argentina-latest.osm.pbf https://download.geofabrik.de/south-america/argentina-latest.osm.pbf
if errorlevel 1 goto :err_motis
docker run --rm -v "%cd%\datos\motis":/data -w /data ubuntu:22.04 bash -c "apt-get update > /dev/null && apt-get install -y osmium-tool > /dev/null && osmium extract -b -58.99,-34.93,-58.01,-34.33 argentina-latest.osm.pbf -o amba.osm.pbf"
if errorlevel 1 goto :err_motis
del /q datos\motis\argentina-latest.osm.pbf

:motis_gtfs
rem MOTIS usa los GTFS descomprimidos en datos\gtfs; si se borraron, se vuelven a extraer de los zip
if not exist "datos\gtfs\colectivo\stop_times.txt" (
    python etl\procesar_gtfs.py
    if errorlevel 1 goto :err_motis
)
rem Los GTFS publicos (2019 y 2020) no comparten fechas: cada feed se importa con su calendario semanal
rem proyectado a la semana de referencia de etl\motis\config.yml
for %%m in (colectivo tren subte) do (
    robocopy "datos\gtfs\%%m" "datos\motis\gtfs\%%m" *.txt /XF calendar.txt calendar_dates.txt feed_info.txt /NFL /NDL /NJH /NJS /NP >nul
    if errorlevel 8 goto :err_motis
    copy /y "etl\motis\calendario-%%m.txt" "datos\motis\gtfs\%%m\calendar.txt" >nul
)
copy /y etl\motis\config.yml datos\motis\config.yml >nul

echo Importando el mapa y los horarios (tarda varios minutos)...
docker compose stop motis >nul 2>&1
%MOTIS_RUN% "cd /entrada && rm -f /data/importacion-ok && /motis import -c config.yml -d /data && touch /data/importacion-ok"
if errorlevel 1 goto :err_motis
rem Ya importado, el servidor no necesita la copia de los GTFS
rd /s /q datos\motis\gtfs
docker compose up -d motis
if errorlevel 1 goto :err_motis

:fin
echo.
echo ==========================================
echo Carga finalizada con exito.
echo ==========================================
goto :eof

:err_docker
echo ERROR: no se pudo levantar docker compose. Verifica que Docker Desktop este corriendo.
exit /b 1
:err_timeout
echo ERROR: PostgreSQL no respondio a tiempo.
exit /b 1
:err_python
echo ERROR: no se pudo instalar duckdb. Verifica que Python 3 este instalado y en el PATH.
exit /b 1
:err_download
echo ERROR: fallo la descarga de los datos abiertos. Revisa la conexion y volve a intentar.
exit /b 1
:err_load
echo ERROR: no se pudieron copiar los CSV a la base.
exit /b 1
:err_calc
echo ERROR: fallo el calculo de los indicadores. Verifica que el mapa de OSM este importado (OSM-DATA-UPDATER-DOCKER.bat).
exit /b 1
:err_motis
echo ERROR: fallo la preparacion de MOTIS. Revisa el mensaje de arriba.
exit /b 1
