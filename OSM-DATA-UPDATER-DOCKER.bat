@echo off
setlocal
cd /d "%~dp0"

set DB_USER=zonamatch
set DB_NAME=zonamatch_docker
set DUMP_FILE=zonamatch.sql
set RESTORE_LOG=restore-geo.log
set PSQL=docker compose exec -T db psql -U %DB_USER% -d %DB_NAME%

echo ==========================================
echo Unified ZonaMatch data import
echo Environment: Docker Compose
echo ==========================================

echo.
echo [1/7] Starting the database...
docker compose up -d
if errorlevel 1 goto :err_docker

echo Waiting for PostgreSQL to accept connections...
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
echo [2/7] Enabling PostGIS and creating app, geo, and osm schemas if needed...
rem db/init runs only when the volume is first created; ensure PostGIS is enabled for existing databases too.
%PSQL% -v ON_ERROR_STOP=1 -c "CREATE EXTENSION IF NOT EXISTS postgis; CREATE SCHEMA IF NOT EXISTS app; CREATE SCHEMA IF NOT EXISTS geo; CREATE SCHEMA IF NOT EXISTS osm;"
if errorlevel 1 goto :err_schemas

echo.
echo [3/7] Loading GIS data into the geo schema...
if not exist "%DUMP_FILE%" (
    echo WARNING: %DUMP_FILE% was not found in this directory. Skipping the geo schema import.
    goto :ef
)

rem Check for a table from the dump (geo.escuelas), not just whether geo has tables: DATOS-ZONAS-DOCKER.bat also
rem creates tables in geo, which previously caused the dump to be skipped if that script ran first.
set HAS_DUMP=
for /f "usebackq" %%i in (`%PSQL% -tAc "SELECT to_regclass('geo.escuelas') IS NOT NULL"`) do set HAS_DUMP=%%i
if "%HAS_DUMP%"=="" goto :err_geocheck
if "%HAS_DUMP%"=="t" (
    echo Tables from %DUMP_FILE% are already in the geo schema. Skipping the restore.
    goto :ef
)

echo Restoring %DUMP_FILE% (this may take a few minutes)...
%PSQL% < "%DUMP_FILE%" > "%RESTORE_LOG%" 2>&1

set HAS_ZM=
for /f "usebackq" %%i in (`%PSQL% -tAc "SELECT count(*) FROM information_schema.schemata WHERE schema_name='zm'"`) do set HAS_ZM=%%i
if "%HAS_ZM%"=="1" goto :rename_zm

rem No zm schema was found; the dump may have created its tables directly in geo.
set HAS_DUMP=
for /f "usebackq" %%i in (`%PSQL% -tAc "SELECT to_regclass('geo.escuelas') IS NOT NULL"`) do set HAS_DUMP=%%i
if not "%HAS_DUMP%"=="t" goto :err_restore
goto :restore_done

:rename_zm
rem The dump creates tables in schema zm: move them one by one to geo (which may already contain other tables), then drop zm.
%PSQL% -v ON_ERROR_STOP=1 -c "DO $DECLARE t text; BEGIN CREATE SCHEMA IF NOT EXISTS geo; FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'zm' LOOP EXECUTE format('ALTER TABLE zm.%%I SET SCHEMA geo', t); END LOOP; DROP SCHEMA zm; END$;"
if errorlevel 1 goto :err_rename

:restore_done
echo Restore completed. Harmless errors (\restrict, transaction_timeout, duplicate spatial_ref_sys) are in %RESTORE_LOG%.

:ef
echo.
echo [4/7] Applying Entity Framework migrations (app schema)...
if not exist "ZonaMatch.Infrastructure\Migrations" (
    echo WARNING: no Migrations directory was found in ZonaMatch.Infrastructure. Skipping Update-Database.
    goto :osm
)
dotnet ef --version >nul 2>&1
if errorlevel 1 (
    echo dotnet-ef is not installed. Installing it as a global tool...
    dotnet tool install --global dotnet-ef --version "10.*"
    if errorlevel 1 goto :err_ef
)
rem DbContextFactory reads appsettings.json relative to the current directory, so run it from Infrastructure.
pushd ZonaMatch.Infrastructure
dotnet ef database update --startup-project ..\ZonaMatch.Api
set EF_RESULT=%errorlevel%
popd
if not "%EF_RESULT%"=="0" goto :err_ef

:osm
echo.
echo [5/7] Downloading the latest Argentina map...
curl -L -O https://download.geofabrik.de/south-america/argentina-latest.osm.pbf
if errorlevel 1 goto :err_download

echo.
echo [6/7] Extracting the CABA and Greater Buenos Aires area...
docker run --rm -v "%cd%":/data -w /data ubuntu:22.04 bash -c "apt-get update > /dev/null && apt-get install -y osmium-tool > /dev/null && osmium extract -b -58.99,-34.93,-58.01,-34.33 argentina-latest.osm.pbf -o amba.osm.pbf"
if errorlevel 1 goto :err_osm

echo.
echo [7/7] Importing OSM data into the osm schema...
docker run --rm -v "%cd%":/data -e PGPASSWORD=zonamatch -e DEBIAN_FRONTEND=noninteractive ubuntu:22.04 bash -c "apt-get update > /dev/null && apt-get install -y osm2pgsql > /dev/null && osm2pgsql -c --output-pgsql-schema=osm -d %DB_NAME% -U %DB_USER% -H host.docker.internal -P 5433 /data/amba.osm.pbf"
if errorlevel 1 goto :err_osm

echo.
echo Cleaning up temporary files...
del /q argentina-latest.osm.pbf amba.osm.pbf 2>nul

echo.
echo ==========================================
echo Data import completed successfully.
echo ==========================================
pause
exit /b 0

:err_docker
echo ERROR: could not start Docker Compose. Check that Docker Desktop is running.
goto :fail
:err_timeout
echo ERROR: PostgreSQL did not respond in time. Check: docker compose logs db
goto :fail
:err_schemas
echo ERROR: could not create the schemas.
goto :fail
:err_geocheck
echo ERROR: could not query the geo schema.
goto :fail
:err_restore
echo ERROR: the restore created no tables in either schema zm or geo. Check %RESTORE_LOG%.
goto :fail
:err_rename
echo ERROR: could not move tables from zm to geo. Check %RESTORE_LOG%.
goto :fail
:err_ef
echo ERROR: dotnet ef failed (Update-Database). See the message above.
goto :fail
:err_download
echo ERROR: failed to download the Argentina map.
goto :fail
:err_osm
echo ERROR: OSM processing/import failed.
goto :fail
:fail
echo.
pause
exit /b 1
