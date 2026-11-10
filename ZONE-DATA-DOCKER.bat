@echo off
setlocal

REM Loads the open data used in zone details (2022 census, CABA crime data, district-level SNIC crime data,
REM and public transit frequencies from GTFS) into the geo schema, then prepares the MOTIS routing engine
REM (walking, cycling, driving, and public transit travel times). Requires the OSM map to be imported already
REM (OSM-DATA-UPDATER-DOCKER.bat), Python 3, and the duckdb package (pip install duckdb).
REM It is safe to run this again: tables are recalculated. Existing CSV files in datos\ are reused; delete them
REM to download fresh data. MOTIS is imported only once. To reimport it (for example, with updated GTFS feeds), run:
REM ZONE-DATA-DOCKER.bat reimport

set DB_USER=zonamatch
set DB_NAME=zonamatch_docker
set PSQL=docker compose exec -T db psql -U %DB_USER% -d %DB_NAME% -q -v ON_ERROR_STOP=1

echo ==========================================
echo ZonaMatch open data
echo ==========================================

echo.
echo [1/5] Starting the database...
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
echo [2/5] Downloading the 2022 census, CABA crime data, SNIC district crime data, and GTFS feeds to datos\ ...
python -c "import duckdb" >nul 2>&1
if errorlevel 1 (
    echo Installing duckdb...
    python -m pip install duckdb
    if errorlevel 1 goto :err_python
)
if exist "datos\censo_radios_amba.csv" if exist "datos\censo_indicadores_amba.csv" if exist "datos\delitos_2024.csv" (
    echo Census and crime data already downloaded. Delete their CSV files in datos\ to refresh them.
    goto :gtfs
)
python etl\download_open_data.py
if errorlevel 1 goto :err_download

:gtfs
if exist "datos\snic_departamentos.csv" (
    echo SNIC district crime data already downloaded. Delete datos\snic_departamentos.csv to refresh it.
    goto :transit
)
python etl\download_snic.py
if errorlevel 1 goto :err_download

:transit
if exist "datos\transporte_salidas.csv" (
    echo Transit data already processed. Delete datos\transporte_salidas.csv to refresh it.
    goto :transit_routes
)
echo Summarizing bus, train, and subway GTFS feeds (about 210 MB downloaded on the first run)...
python etl\process_gtfs.py
if errorlevel 1 goto :err_download

:transit_routes
rem Route shapes for the map layer; older runs only produced the departures summary.
if not exist "datos\transporte_recorridos.csv" (
    python etl\process_gtfs.py --routes-only
    if errorlevel 1 goto :err_download
)

:load
echo.
echo [3/5] Copying CSV files into the database...
%PSQL% < etl\create_geographic_tables.sql
if errorlevel 1 goto :err_load
%PSQL% -c "\copy geo.carga_censo_radios FROM STDIN WITH (FORMAT csv, HEADER true)" < datos\censo_radios_amba.csv
if errorlevel 1 goto :err_load
%PSQL% -c "\copy geo.carga_censo_indicadores FROM STDIN WITH (FORMAT csv, HEADER true)" < datos\censo_indicadores_amba.csv
if errorlevel 1 goto :err_load
%PSQL% -c "\copy geo.carga_delitos FROM STDIN WITH (FORMAT csv, HEADER true, NULL 'NULL')" < datos\delitos_2024.csv
if errorlevel 1 goto :err_load
%PSQL% -c "\copy geo.carga_snic FROM STDIN WITH (FORMAT csv, HEADER true)" < datos\snic_departamentos.csv
if errorlevel 1 goto :err_load
%PSQL% -c "\copy geo.carga_transporte_salidas FROM STDIN WITH (FORMAT csv, HEADER true)" < datos\transporte_salidas.csv
if errorlevel 1 goto :err_load
%PSQL% -c "\copy geo.carga_transporte_recorridos FROM STDIN WITH (FORMAT csv, HEADER true)" < datos\transporte_recorridos.csv
if errorlevel 1 goto :err_load

echo.
echo [4/5] Calculating zone indicators (takes about one minute)...
%PSQL% < etl\calculate_zones.sql
if errorlevel 1 goto :err_calc
%PSQL% < etl\calculate_transit.sql
if errorlevel 1 goto :err_calc
%PSQL% < etl\calculate_transit_routes.sql
if errorlevel 1 goto :err_calc
rem Province crime data is distributed across each district's localities using a model fitted on CABA data.
python -c "import numpy" >nul 2>&1
if errorlevel 1 (
    echo Installing numpy...
    python -m pip install numpy
    if errorlevel 1 goto :err_python
)
%PSQL% --csv < etl\crime\fraction_features.sql > datos\delitos_fracciones_entrada.csv
if errorlevel 1 goto :err_calc
python etl\crime\fit_crime_model.py datos\delitos_fracciones_entrada.csv datos\snic_departamentos.csv datos\delitos_fracciones.csv
if errorlevel 1 goto :err_calc
%PSQL% -c "\copy geo.carga_delitos_fracciones FROM STDIN WITH (FORMAT csv, HEADER true)" < datos\delitos_fracciones.csv
if errorlevel 1 goto :err_load
%PSQL% < etl\crime\spread_crimes.sql
if errorlevel 1 goto :err_calc
rem Estimated rents for zones without Zonaprop data (ZONE-MARKET-DOCKER.bat).
%PSQL% < etl\estimate_rents.sql
if errorlevel 1 goto :err_calc
rem sitemap.xml with every zone page, copied to the SPA when it sits next to the backend (or in FRONT_DIR).
%PSQL% -At < etl\export_sitemap.sql > datos\sitemap.xml
if errorlevel 1 goto :err_calc
if not defined FRONT_DIR set FRONT_DIR=..\..\frontend\ZonaMatchSPA
if exist "%FRONT_DIR%\public" (
    copy /Y datos\sitemap.xml "%FRONT_DIR%\public\sitemap.xml" > nul
    echo Sitemap copied to %FRONT_DIR%\public\sitemap.xml
) else (
    echo SPA not found at %FRONT_DIR%: copy datos\sitemap.xml to its public folder by hand.
)

echo.
echo [5/5] Preparing the MOTIS routing engine...
set MOTIS_RUN=docker compose run --rm --no-deps --entrypoint /bin/sh motis -c
if /i "%~1"=="reimport" goto :motis_import
if /i "%~1"=="reimportar" goto :motis_import
%MOTIS_RUN% "test -f /data/importacion-ok" >nul 2>&1
if errorlevel 1 goto :motis_import
echo MOTIS is already imported. To reimport it: ZONE-DATA-DOCKER.bat reimport
goto :done

:motis_import
if not exist "datos\motis" mkdir "datos\motis"
if exist "datos\motis\amba.osm.pbf" goto :motis_gtfs
if exist "amba.osm.pbf" (
    copy /y amba.osm.pbf datos\motis\ >nul
    goto :motis_gtfs
)
rem Use the same AMBA extract as OSM-DATA-UPDATER-DOCKER.bat.
echo Downloading the Argentina map (about 430 MB) to extract the AMBA area...
curl -L -o datos\motis\argentina-latest.osm.pbf https://download.geofabrik.de/south-america/argentina-latest.osm.pbf
if errorlevel 1 goto :err_motis
docker run --rm -v "%cd%\datos\motis":/data -w /data ubuntu:22.04 bash -c "apt-get update > /dev/null && apt-get install -y osmium-tool > /dev/null && osmium extract -b -58.99,-34.93,-58.01,-34.33 argentina-latest.osm.pbf -o amba.osm.pbf"
if errorlevel 1 goto :err_motis
del /q datos\motis\argentina-latest.osm.pbf

:motis_gtfs
rem MOTIS uses the extracted GTFS files in datos\gtfs; if they were deleted, extract them again from the ZIP files.
if not exist "datos\gtfs\colectivo\stop_times.txt" (
    python etl\process_gtfs.py
    if errorlevel 1 goto :err_motis
)
rem Public GTFS feeds (2019 and 2020) have non-overlapping dates; each feed is imported with its own weekly calendar
rem projected onto the reference week in etl\motis\config.yml.
for %%m in (colectivo tren subte) do (
    robocopy "datos\gtfs\%%m" "datos\motis\gtfs\%%m" *.txt /XF calendar.txt calendar_dates.txt feed_info.txt /NFL /NDL /NJH /NJS /NP >nul
    if errorlevel 8 goto :err_motis
    copy /y "etl\motis\calendar-%%m.txt" "datos\motis\gtfs\%%m\calendar.txt" >nul
)
copy /y etl\motis\config.yml datos\motis\config.yml >nul

echo Importing the map and transit schedules (takes several minutes)...
docker compose stop motis >nul 2>&1
%MOTIS_RUN% "cd /entrada && rm -f /data/importacion-ok && /motis import -c config.yml -d /data && touch /data/importacion-ok"
if errorlevel 1 goto :err_motis
rem Once imported, the server no longer needs the GTFS copy.
rd /s /q datos\motis\gtfs
docker compose up -d motis
if errorlevel 1 goto :err_motis

:done
echo.
echo ==========================================
echo Data load completed successfully.
echo ==========================================
goto :eof

:err_docker
echo ERROR: could not start Docker Compose. Check that Docker Desktop is running.
exit /b 1
:err_timeout
echo ERROR: PostgreSQL did not respond in time.
exit /b 1
:err_python
echo ERROR: could not install duckdb or numpy. Check that Python 3 is installed and on PATH.
exit /b 1
:err_download
echo ERROR: failed to download open data. Check the connection and try again.
exit /b 1
:err_load
echo ERROR: could not copy CSV files into the database.
exit /b 1
:err_calc
echo ERROR: indicator calculation failed. Check that the OSM map is imported (OSM-DATA-UPDATER-DOCKER.bat).
exit /b 1
:err_motis
echo ERROR: MOTIS preparation failed. See the message above.
exit /b 1
