@echo off
setlocal
cd /d "%~dp0"

set DB_USER=zonamatch
set DB_NAME=zonamatch_docker
set DUMP_FILE=zonamatch.sql
set RESTORE_LOG=restore-geo.log
set PSQL=docker compose exec -T db psql -U %DB_USER% -d %DB_NAME%

echo ==========================================
echo Carga unificada de datos de ZonaMatch
echo Entorno: Docker Compose
echo ==========================================

echo.
echo [1/7] Levantando la base de datos...
docker compose up -d
if errorlevel 1 goto :err_docker

echo Esperando a que PostgreSQL acepte conexiones...
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
echo [2/7] Creando schemas app, geo y osm (si no existen)...
%PSQL% -v ON_ERROR_STOP=1 -c "CREATE SCHEMA IF NOT EXISTS app; CREATE SCHEMA IF NOT EXISTS geo; CREATE SCHEMA IF NOT EXISTS osm;"
if errorlevel 1 goto :err_schemas

echo.
echo [3/7] Cargando datos GIS en el schema geo...
if not exist "%DUMP_FILE%" (
    echo ADVERTENCIA: no se encontro %DUMP_FILE% en esta carpeta. Se omite la carga del schema geo.
    goto :ef
)

set GEO_TABLES=
for /f "usebackq" %%i in (`%PSQL% -tAc "SELECT count(*) FROM information_schema.tables WHERE table_schema='geo'"`) do set GEO_TABLES=%%i
if "%GEO_TABLES%"=="" goto :err_geocheck
if not "%GEO_TABLES%"=="0" (
    echo El schema geo ya tiene %GEO_TABLES% tablas. Se omite el restore.
    goto :ef
)

echo Restaurando %DUMP_FILE% (puede tardar unos minutos)...
rem El dump crea sus tablas en el schema zm: se descarta el geo vacio y se renombra zm a geo.
%PSQL% -c "DROP SCHEMA IF EXISTS geo;" >nul
%PSQL% < "%DUMP_FILE%" > "%RESTORE_LOG%" 2>&1

set HAS_ZM=
for /f "usebackq" %%i in (`%PSQL% -tAc "SELECT count(*) FROM information_schema.schemata WHERE schema_name='zm'"`) do set HAS_ZM=%%i
if "%HAS_ZM%"=="1" goto :rename_zm

rem No hay schema zm: puede que el dump ya cree las tablas en geo directamente.
%PSQL% -c "CREATE SCHEMA IF NOT EXISTS geo;" >nul
set GEO_TABLES=
for /f "usebackq" %%i in (`%PSQL% -tAc "SELECT count(*) FROM information_schema.tables WHERE table_schema='geo'"`) do set GEO_TABLES=%%i
if "%GEO_TABLES%"=="" goto :err_restore
if "%GEO_TABLES%"=="0" goto :err_restore
goto :restore_done

:rename_zm
%PSQL% -v ON_ERROR_STOP=1 -c "ALTER SCHEMA zm RENAME TO geo;"
if errorlevel 1 goto :err_rename

:restore_done
echo Restore finalizado. Los errores inofensivos (\restrict, transaction_timeout, spatial_ref_sys duplicada) quedaron en %RESTORE_LOG%.

:ef
echo.
echo [4/7] Aplicando migraciones de Entity Framework (schema app)...
if not exist "ZonaMatch.Infrastructure\Migrations" (
    echo ADVERTENCIA: no hay carpeta Migrations en ZonaMatch.Infrastructure. Se omite Update-Database.
    goto :osm
)
dotnet ef --version >nul 2>&1
if errorlevel 1 (
    echo dotnet-ef no esta instalado. Instalando como herramienta global...
    dotnet tool install --global dotnet-ef --version "10.*"
    if errorlevel 1 goto :err_ef
)
rem El DbContextFactory lee appsettings.json relativo a la carpeta actual, por eso se ejecuta desde Infrastructure.
pushd ZonaMatch.Infrastructure
dotnet ef database update --startup-project ..\ZonaMatch.Api
set EF_RESULT=%errorlevel%
popd
if not "%EF_RESULT%"=="0" goto :err_ef

:osm
echo.
echo [5/7] Descargando el ultimo mapa de Argentina...
curl -L -O https://download.geofabrik.de/south-america/argentina-latest.osm.pbf
if errorlevel 1 goto :err_download

echo.
echo [6/7] Recortando el area de CABA y Conurbano...
docker run --rm -v "%cd%":/data -w /data ubuntu:22.04 bash -c "apt-get update > /dev/null && apt-get install -y osmium-tool > /dev/null && osmium extract -b -58.99,-34.93,-58.01,-34.33 argentina-latest.osm.pbf -o amba.osm.pbf"
if errorlevel 1 goto :err_osm

echo.
echo [7/7] Importando los datos de OSM al schema osm...
docker run --rm -v "%cd%":/data -e PGPASSWORD=zonamatch -e DEBIAN_FRONTEND=noninteractive ubuntu:22.04 bash -c "apt-get update > /dev/null && apt-get install -y osm2pgsql > /dev/null && osm2pgsql -c --output-pgsql-schema=osm -d %DB_NAME% -U %DB_USER% -H host.docker.internal -P 5433 /data/amba.osm.pbf"
if errorlevel 1 goto :err_osm

echo.
echo Limpiando archivos temporales...
del /q argentina-latest.osm.pbf amba.osm.pbf 2>nul

echo.
echo ==========================================
echo Carga finalizada con exito.
echo ==========================================
pause
exit /b 0

:err_docker
echo ERROR: no se pudo levantar docker compose. Verifica que Docker Desktop este corriendo.
goto :fail
:err_timeout
echo ERROR: PostgreSQL no respondio a tiempo. Revisa: docker compose logs db
goto :fail
:err_schemas
echo ERROR: no se pudieron crear los schemas.
goto :fail
:err_geocheck
echo ERROR: no se pudo consultar el schema geo.
goto :fail
:err_restore
echo ERROR: el restore no creo tablas ni en el schema zm ni en geo. Revisa %RESTORE_LOG%.
goto :fail
:err_rename
echo ERROR: no se pudo renombrar zm a geo. Revisa %RESTORE_LOG%.
goto :fail
:err_ef
echo ERROR: fallo dotnet ef (Update-Database). Revisa el mensaje de arriba.
goto :fail
:err_download
echo ERROR: fallo la descarga del mapa de Argentina.
goto :fail
:err_osm
echo ERROR: fallo el procesamiento/importacion de OSM.
goto :fail
:fail
echo.
pause
exit /b 1
