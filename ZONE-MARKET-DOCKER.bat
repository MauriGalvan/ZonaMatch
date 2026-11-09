@echo off
setlocal

REM Real estate market data by zone from every Zonaprop listing: sale and rental listing counts by property type
REM (apartments, houses, PH) and by rooms (1 to 5 or more), rent in pesos, sale prices in US dollars, the price
REM range and distribution, price per square meter, typical surface, and the share eligible for a mortgage, published
REM by the owner or accepting pets.
REM Requires the database with calculated zones (ZONE-DATA-DOCKER.bat), the migrations applied
REM (AddListingFeaturesToZoneMarket, AddZoneMarketByRooms, AddPriceDecilesToZoneMarket) and Python 3 (no extra
REM packages).
REM
REM Scraping is intentionally slow (one request every 3 or 4 seconds, about 15 hours for all zones). If Zonaprop
REM blocks requests, it waits and retries; if it keeps blocking, it stops: run it again to resume, because every
REM page is cached in datos\zonaprop\cache\YYYY-MM-DD (the date the run started).
REM Output: app.zone_market and app.zone_market_rooms (read by the API), a CSV copy at
REM datos\zonaprop\market_by_zone.csv and the Zonaprop slugs of the SPA link (datos\zonaprop\zonaprop-slugs.json).
REM The slugs are copied to frontend\ZonaMatchSPA\src\features\zone-detail\data\ when the SPA sits next to the
REM backend (..\..\frontend\ZonaMatchSPA); otherwise run "set FRONT_DIR=path\to\ZonaMatchSPA" first.
REM
REM Options (passed to the scraper): ZONE-MARKET-DOCKER.bat --only palermo,adrogue   only those zones
REM                                  ZONE-MARKET-DOCKER.bat --export-only  rebuild without downloading
REM                                  ZONE-MARKET-DOCKER.bat --export-only --run 2026-11-01  from that run

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
REM Credit-eligible sales by type, rooms and price: optional, runs before 2026-11 do not have it
if exist datos\zonaprop\credit_segments.csv (
    %PSQL% -c "\copy geo.load_zonaprop_credit_segments FROM STDIN WITH (FORMAT csv, HEADER true)" < datos\zonaprop\credit_segments.csv
    if errorlevel 1 goto :err_load
)
%PSQL% < etl\zonaprop\calculate_market.sql
if errorlevel 1 goto :err_calc
rem Zones still missing Zonaprop data receive an estimated rent based on census data.
%PSQL% < etl\estimate_rents.sql
if errorlevel 1 goto :err_calc
%PSQL% --csv < etl\zonaprop\export_market.sql > datos\zonaprop\market_by_zone.csv
if errorlevel 1 goto :err_calc
%PSQL% -At < etl\zonaprop\export_front_slugs.sql > datos\zonaprop\zonaprop-slugs.json
if errorlevel 1 goto :err_calc
rem Slugs to the SPA, when it sits next to the backend (or in FRONT_DIR)
if not defined FRONT_DIR set FRONT_DIR=..\..\frontend\ZonaMatchSPA
set FRONT_SLUGS=%FRONT_DIR%\src\features\zone-detail\data\zonaprop-slugs.json
if exist "%FRONT_SLUGS%" (
    copy /Y datos\zonaprop\zonaprop-slugs.json "%FRONT_SLUGS%" > nul
    echo Slugs copied to %FRONT_SLUGS%
) else (
    echo SPA not found at %FRONT_DIR%: copy datos\zonaprop\zonaprop-slugs.json by hand.
)

echo.
echo ==========================================
echo Done: datos\zonaprop\market_by_zone.csv and datos\zonaprop\zonaprop-slugs.json
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
