@echo off
setlocal

REM Real estate market data by zone from Zonaprop listings: sale and rental listing counts by property type
REM (apartments, houses, PH), average rent in pesos, and sale price per square meter in US dollars.
REM Requires the database with calculated zones (ZONE-DATA-DOCKER.bat) and Python 3 (no extra packages).
REM
REM Scraping is intentionally slow (one request every few seconds, a few hours for all zones). If Zonaprop blocks
REM requests, the process stops; run it again to resume because each page is cached in datos\zonaprop\cache.
REM Output: app.zone_market (read by the API) and a CSV copy at datos\zonaprop\market_by_zone.csv.
REM
REM Options (passed to the scraper): ZONE-MARKET-DOCKER.bat --pages 1      sample 30 listings per combination
REM                                  ZONE-MARKET-DOCKER.bat --export-only  rebuild without downloading

set DB_USER=zonamatch
set DB_NAME=zonamatch_docker
set PSQL=docker compose exec -T db psql -U %DB_USER% -d %DB_NAME% -q -v ON_ERROR_STOP=1

echo ==========================================
echo Zone real estate market (Zonaprop)
echo ==========================================

echo.
echo [1/4] Starting the database...
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
echo [2/4] Exporting zones to search...
if not exist "datos\zonaprop" mkdir "datos\zonaprop"
%PSQL% --csv < etl\zonaprop\target_zones.sql > datos\zonaprop\zones.csv
if errorlevel 1 goto :err_calc

echo.
echo [3/4] Reading Zonaprop listings (press Ctrl+C to stop and resume later)...
python etl\zonaprop\scrape_zonaprop.py %*
if errorlevel 1 goto :err_scrape

echo.
echo [4/4] Calculating each zone's market data...
%PSQL% < etl\zonaprop\create_load_tables.sql
if errorlevel 1 goto :err_load
%PSQL% -c "\copy geo.load_zonaprop_totals FROM STDIN WITH (FORMAT csv, HEADER true)" < datos\zonaprop\totals.csv
if errorlevel 1 goto :err_load
%PSQL% -c "\copy geo.load_zonaprop_listings FROM STDIN WITH (FORMAT csv, HEADER true)" < datos\zonaprop\listings.csv
if errorlevel 1 goto :err_load
%PSQL% < etl\zonaprop\calculate_market.sql
if errorlevel 1 goto :err_calc
rem Zones still missing Zonaprop data receive an estimated rent based on census data.
%PSQL% < etl\estimate_rents.sql
if errorlevel 1 goto :err_calc
%PSQL% --csv < etl\zonaprop\export_market.sql > datos\zonaprop\market_by_zone.csv
if errorlevel 1 goto :err_calc

echo.
echo ==========================================
echo Done: datos\zonaprop\market_by_zone.csv
echo ==========================================
goto :eof

:err_docker
echo ERROR: could not start Docker Compose. Check that Docker Desktop is running.
exit /b 1
:err_timeout
echo ERROR: PostgreSQL did not respond in time.
exit /b 1
:err_scrape
echo ERROR: scraping failed. See the message above; run again to resume.
exit /b 1
:err_load
echo ERROR: could not copy CSV files into the database.
exit /b 1
:err_calc
echo ERROR: calculation failed. Check that zones have been calculated (ZONE-DATA-DOCKER.bat).
exit /b 1
