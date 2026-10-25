@echo off
setlocal

REM Mercado inmobiliario de cada zona a partir de las publicaciones de Zonaprop: cantidad de avisos de venta y
REM alquiler por tipo (departamentos, casas, PH), alquiler promedio en pesos y precio por m2 de venta en dolares.
REM Requiere la base con las zonas calculadas (DATOS-ZONAS-DOCKER.bat) y Python 3 (sin paquetes extra).
REM
REM El scraping es lento a proposito (un pedido cada pocos segundos, unas horas para todas las zonas). Si Zonaprop
REM bloquea, se corta solo: volver a ejecutar retoma donde quedo, porque cada pagina queda en datos\zonaprop\cache.
REM Resultado: tabla app.zone_market (la que lee la API) y su copia en CSV, datos\zonaprop\market_by_zone.csv.
REM
REM Opciones (se pasan al scraper): MERCADO-ZONAS-DOCKER.bat --pages 1      muestra de 30 avisos por combinacion
REM                                 MERCADO-ZONAS-DOCKER.bat --export-only  recalcular sin descargar nada

set DB_USER=zonamatch
set DB_NAME=zonamatch_docker
set PSQL=docker compose exec -T db psql -U %DB_USER% -d %DB_NAME% -q -v ON_ERROR_STOP=1

echo ==========================================
echo Mercado inmobiliario de las zonas (Zonaprop)
echo ==========================================

echo.
echo [1/4] Levantando la base de datos...
docker compose up -d db
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
echo [2/4] Exportando las zonas a buscar...
if not exist "datos\zonaprop" mkdir "datos\zonaprop"
%PSQL% --csv < etl\zonaprop\target_zones.sql > datos\zonaprop\zones.csv
if errorlevel 1 goto :err_calc

echo.
echo [3/4] Leyendo las publicaciones de Zonaprop (se puede cortar con Ctrl+C y retomar despues)...
python etl\zonaprop\scrape_zonaprop.py %*
if errorlevel 1 goto :err_scrape

echo.
echo [4/4] Calculando el mercado de cada zona...
%PSQL% < etl\zonaprop\create_load_tables.sql
if errorlevel 1 goto :err_load
%PSQL% -c "\copy geo.load_zonaprop_totals FROM STDIN WITH (FORMAT csv, HEADER true)" < datos\zonaprop\totals.csv
if errorlevel 1 goto :err_load
%PSQL% -c "\copy geo.load_zonaprop_listings FROM STDIN WITH (FORMAT csv, HEADER true)" < datos\zonaprop\listings.csv
if errorlevel 1 goto :err_load
%PSQL% < etl\zonaprop\calculate_market.sql
if errorlevel 1 goto :err_calc
rem Zonas que siguen sin datos de Zonaprop: alquiler estimado con el censo
%PSQL% < etl\estimar_alquileres.sql
if errorlevel 1 goto :err_calc
%PSQL% --csv < etl\zonaprop\export_market.sql > datos\zonaprop\market_by_zone.csv
if errorlevel 1 goto :err_calc

echo.
echo ==========================================
echo Listo: datos\zonaprop\market_by_zone.csv
echo ==========================================
goto :eof

:err_docker
echo ERROR: no se pudo levantar docker compose. Verifica que Docker Desktop este corriendo.
exit /b 1
:err_timeout
echo ERROR: PostgreSQL no respondio a tiempo.
exit /b 1
:err_scrape
echo ERROR: fallo el scraping. Revisa el mensaje de arriba; volver a ejecutar retoma lo que falta.
exit /b 1
:err_load
echo ERROR: no se pudieron copiar los CSV a la base.
exit /b 1
:err_calc
echo ERROR: fallo el calculo. Verifica que las zonas esten calculadas (DATOS-ZONAS-DOCKER.bat).
exit /b 1
