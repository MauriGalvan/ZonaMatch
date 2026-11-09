🚀 Guía de Instalación y Ejecución

Seguí estos pasos en orden para configurar el entorno de desarrollo local:

1. Clonar el repositorio

git clone https://github.com/MauriGalvan/ZonaMatch.git
cd ZonaMatch


2. Levantar la Base de Datos (Docker)

El proyecto incluye un archivo docker-compose.yml configurado con la imagen de PostgreSQL + PostGIS.
Antes de levantar los servicios, creá el archivo local `.env` desde la plantilla y configurá una contraseña para PostgreSQL:

```powershell
Copy-Item .env.example .env
notepad .env
```

En `.env`, completá `POSTGRES_PASSWORD` con una contraseña local. Este archivo está ignorado por Git y Docker Compose lo carga automáticamente. No lo subas al repositorio. Si ya tenías creado el volumen `db_data`, conservá la contraseña con la que se inicializó: cambiar `POSTGRES_PASSWORD` no cambia la contraseña de una base existente.

Después, desde una terminal en la raíz del proyecto, ejecutá:

```powershell
docker-compose up -d
```

Puerto : La base de datos se expone en el puerto local 5433 (para evitar conflictos con instalaciones locales de PostgreSQL).

### Configuración local de la API (User Secrets)

Las credenciales de la API no se guardan en `appsettings.json`. Para ejecutar la API desde Visual Studio o con `dotnet run` en el entorno `Development`, configurá los siguientes valores con User Secrets. El proyecto ya tiene un `UserSecretsId`.

La contraseña de esta cadena debe coincidir con `POSTGRES_PASSWORD` de `.env`:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=zonamatch_docker;Username=zonamatch;Password=TU_PASSWORD_LOCAL;Keepalive=30;Connection Idle Lifetime=30" --project ZonaMatch.Api
```

Generá una clave JWT aleatoria para el entorno local:

```powershell
$jwtKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set "Jwt:SigningKey" $jwtKey --project ZonaMatch.Api
Remove-Variable jwtKey
```

Para habilitar los flujos que envían correos, configurá la contraseña de aplicación SMTP que les hayan compartido por un canal seguro:

```powershell
dotnet user-secrets set "EmailSettings:Password" "CONTRASEÑA_SMTP" --project ZonaMatch.Api
```

No incluyas contraseñas, claves JWT ni cadenas de conexión reales en `appsettings.json`, `.env.example`, commits o mensajes. En otros entornos, configurá `ConnectionStrings__DefaultConnection`, `Jwt__SigningKey` y `EmailSettings__Password` como variables de entorno o ajustes del servicio de hosting.

### Imágenes de las reseñas (Azurite)

Las imágenes de las reseñas se guardan en Azure Blob Storage. En local se usa el emulador Azurite, que está en el perfil `azurite` del docker-compose, así que no arranca con los `docker compose up -d` habituales. Desde la raíz del proyecto, ejecutá una sola vez:

```powershell
docker compose --profile azurite up -d azurite
az storage container create --name imagenes --connection-string "UseDevelopmentStorage=true"
dotnet user-secrets set "Storage:ConnectionString" "UseDevelopmentStorage=true" --project ZonaMatch.Api
```

- El contenedor `imagenes` se crea a mano porque en Azure lo crea Terraform y Azurite no lo trae. Los archivos se conservan en el volumen `azurite_data`; si lo borrás, las reseñas locales quedan apuntando a imágenes que ya no existen.
- Si no configurás `Storage:ConnectionString`, la API usa ese mismo valor por defecto. Sin Azurite levantado, la API arranca igual y solo falla subir una imagen (`POST /api/v1/images`).
- `UseDevelopmentStorage=true` no es un secreto: Azurite usa una cuenta y una clave públicas. En Azure, `Storage__ConnectionString` la define Terraform como ajuste del App Service.
- El comando `az` requiere Azure CLI. Como alternativa, creá el contenedor desde Azure Storage Explorer conectado a "Local storage emulator".

3. Cargar los datos (schemas geo y osm) con un solo script

Copiá el archivo zonamatch.sql (se comparte aparte, no está en el repositorio) en la carpeta raíz del proyecto, al lado de OSM-DATA-UPDATER-DOCKER.bat, y hacé doble clic en el .bat. El script, en orden:

- levanta la base con docker compose y espera a que esté lista,
- crea los schemas app, geo y osm si no existen,
- restaura zonamatch.sql y pasa sus tablas del schema zm a geo (si geo.escuelas ya existe, omite este paso; si no encuentra zonamatch.sql, avisa y sigue),
- aplica las migraciones de EF (dotnet ef database update; instala dotnet-ef si falta y omite el paso si no hay carpeta Migrations),
- descarga el mapa de Argentina (~400MB), recorta el AMBA y lo importa al schema osm.

Los errores inofensivos del restore (\restrict, transaction_timeout y filas duplicadas de spatial_ref_sys) quedan en restore-geo.log. Es seguro volver a ejecutarlo para actualizar solo los datos de OSM.

4. Migraciones de Entity Framework (schema app)

Ya las aplica el script del paso 3. Si preferís hacerlo a mano, desde la Consola del Administrador de paquetes de Visual Studio: Update-Database. Entity Framework solo administra el schema app; las tablas de geo y osm no se migran.

5. Datos abiertos de las zonas (censo, seguridad y transporte)

Con el mapa de OSM ya importado (paso 3), hacé doble clic en ZONE-DATA-DOCKER.bat. Necesita Python 3 (instala el paquete duckdb si falta). El script:

- descarga a la carpeta datos\ (no se sube al repositorio) el Censo 2022 de INDEC por radio censal para el AMBA (población, hogares, internet, computadora, NBI, hacinamiento, alquiler, cobertura de salud, educación) los delitos 2024 del Mapa del Delito de CABA y los GTFS de colectivos, trenes y subte (Buenos Aires Data, de los que se guarda solo un resumen de salidas por parada y franja horaria),
- los carga en el schema geo y asigna cada radio censal y cada delito, por ubicación, a las zonas de OSM que lo contienen. Las comunas y los partidos suman lo de su superficie,
- prepara el motor de ruteo MOTIS (servicio motis del docker-compose, puerto 18080; se cambia con la variable MOTIS_PORT), que calcula tiempos de viaje a pie, en bici, en auto y en transporte público con el mapa del AMBA y los mismos GTFS. Los importa una sola vez al volumen motis_data; para reimportarlo (por ejemplo con GTFS nuevos), ejecutá ZONE-DATA-DOCKER.bat reimport.

Tarda unos minutos y se puede volver a ejecutar. Los datos se consultan en GET /api/v1/zones/{slug}/indicators y GET /api/v1/zones/{slug}/transit. Fuera de CABA todavía no hay delitos georreferenciados.

Los GTFS públicos son de 2019 (colectivos y subte) y 2020 (trenes) y sus fechas no se superponen, así que MOTIS los usa con un calendario semanal proyectado a una semana de referencia (2 al 8 de marzo de 2026, ver etl\motis\config.yml): los viajes en transporte público se consultan en esas fechas. Hasta que se corre la importación, el contenedor de MOTIS queda esperando sin fallar.

6. Buscador de direcciones con Nominatim local

La búsqueda de calles, alturas y lugares usa una instancia local de Nominatim con el recorte OSM del AMBA. No requiere un servicio de geocodificación externo. El servicio está en el perfil `nominatim`, así que los `docker compose up -d` de los pasos anteriores no lo arrancan antes de que el archivo del mapa esté preparado.

Después de ejecutar `ZONE-DATA-DOCKER.bat`, verificá que exista `datos\motis\amba.osm.pbf`. Si no existe, revisá que la preparación de MOTIS del paso anterior haya terminado. Desde PowerShell, en la raíz del repositorio, ejecutá estos comandos para crear automáticamente la carpeta de Nominatim si hace falta y copiar el recorte:

```powershell
New-Item -ItemType Directory -Force .\datos\nominatim
Copy-Item .\datos\motis\amba.osm.pbf .\datos\nominatim\amba.osm.pbf -Force
```

Iniciá Nominatim y seguí los logs de la importación inicial:

```powershell
docker compose up -d nominatim
docker compose logs -f nominatim
```

La importación puede tardar varios minutos. Cuando aparezca que el servidor está listo, salí de la vista de logs con `Ctrl+C`; eso no detiene el contenedor. El índice se conserva en el volumen `nominatim_data`, así que los próximos inicios reutilizan la importación.

Podés comprobar la búsqueda local abriendo:

```text
http://localhost:8080/search?format=jsonv2&addressdetails=1&q=Gutenberg%20666%2C%20Villa%20Luzuriaga
```

La API, ejecutada desde Visual Studio en la computadora, consulta `http://localhost:8080/`. Mantené el servicio de Nominatim activo mientras usás la búsqueda de direcciones en la SPA. Si la API se ejecuta dentro de Docker, configurá `Nominatim:BaseUrl` como `http://nominatim:8080/`.

7. Mercado inmobiliario de las zonas (Zonaprop)

Con las zonas calculadas (paso 5) y las migraciones aplicadas, hacé doble clic en ZONE-MARKET-DOCKER.bat. Necesita Python 3 (sin paquetes extra). El script:

- lee todas las publicaciones de departamentos, casas y PH en venta y en alquiler de cada zona del AMBA. Zonaprop muestra solo las primeras 9 páginas de cada búsqueda (270 avisos), así que las búsquedas más grandes se parten por franjas de precio hasta que cada una entra entera. La Ciudad completa no se lee: se calcula sumando sus barrios,
- calcula por zona, operación, tipo y cantidad de ambientes: cantidad de avisos, precios promedio y por m², expensas, superficie típica, qué parte es apta crédito, de dueño directo o acepta mascotas, y el rango de precios (21 puntos, uno cada 5 % de los avisos, con el porcentaje real de avisos hasta cada precio; sin precios basura como $ 9.999.999). Lo guarda en app.zone_market y app.zone_market_rooms, que es lo que lee la API,
- exporta datos\zonaprop\market_by_zone.csv y los slugs del enlace "Ver propiedades" (datos\zonaprop\zonaprop-slugs.json). Si la SPA está al lado del back (..\..\frontend\ZonaMatchSPA), los copia a src\features\zone-detail\data\; si está en otro lado, ejecutá `set FRONT_DIR=ruta\de\ZonaMatchSPA` antes del script. El archivo copiado hay que commitearlo en el repo del front.

La lectura es lenta a propósito (un pedido cada 3 o 4 segundos): unas 15 horas para todas las zonas. Avanza zona por zona y muestra `[zonas listas/total]`; en las zonas grandes puede pasar casi una hora sin que avance el contador. Si Zonaprop bloquea, espera y reintenta; si sigue bloqueando, se corta. Se puede cortar con Ctrl+C: volver a ejecutar el script retoma donde quedó, porque cada página se guarda en datos\zonaprop\cache\AAAA-MM-DD (la fecha en que empezó la corrida; una corrida se sigue completando durante 7 días).

Para recalcular sin volver a descargar, con la caché de una corrida (no está en el repositorio; se puede copiar de otra computadora para no repetir las 15 horas):

```powershell
.\ZONE-MARKET-DOCKER.bat --export-only --run 2026-11-01
```

Tarda unos 5 minutos. Para leer solo algunas zonas: `ZONE-MARKET-DOCKER.bat --only palermo,adrogue`.

Esquemas de la base: app (modelo de la aplicación, EF Core), geo (datos GIS externos), osm (importación cruda de OpenStreetMap). PostGIS vive en public.
