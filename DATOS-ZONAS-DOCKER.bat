@echo off
setlocal

REM Carga los datos abiertos que completan el detalle de las zonas (censo 2022, delitos de CABA y
REM frecuencias de transporte publico de los GTFS) en el
REM schema geo. Requiere la base con el mapa de OSM ya importado (OSM-DATA-UPDATER-DOCKER.bat), Python 3
REM y el paquete duckdb (pip install duckdb). Es seguro volver a ejecutarlo: borra y recalcula las tablas.
REM Si los CSV ya estan en datos\, no los vuelve a descargar (borrarlos para actualizar).

set DB_USER=zonamatch
set DB_NAME=zonamatch_docker
set PSQL=docker compose exec -T db psql -U %DB_USER% -d %DB_NAME% -q -v ON_ERROR_STOP=1

echo ==========================================
echo Datos abiertos de las zonas de ZonaMatch
echo ==========================================

echo.
echo [1/4] Levantando la base de datos...
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
echo [2/4] Descargando censo 2022, delitos de CABA y GTFS de transporte en datos\ ...
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
echo [3/4] Copiando los CSV a la base...
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
echo [4/4] Calculando los indicadores de cada zona (tarda alrededor de un minuto)...
%PSQL% < etl\calcular_zonas.sql
if errorlevel 1 goto :err_calc
%PSQL% < etl\calcular_transporte.sql
if errorlevel 1 goto :err_calc

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
