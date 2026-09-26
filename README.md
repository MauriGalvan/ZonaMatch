🚀 Guía de Instalación y Ejecución

Seguí estos pasos en orden para configurar el entorno de desarrollo local:

1. Clonar el repositorio

git clone https://github.com/MauriGalvan/ZonaMatch.git
cd ZonaMatch


2. Levantar la Base de Datos (Docker)

El proyecto incluye un archivo docker-compose.yml configurado con la imagen de PostgreSQL + PostGIS.
Abrí una terminal en la raíz del proyecto y ejecutá:

docker-compose up -d

Puerto : La base de datos se expone en el puerto local 5433 (para evitar conflictos con instalaciones locales de PostgreSQL).

3. Ejecutar las Migraciones (Entity Framework)

Para crear las tablas base y habilitar la extensión espacial en la base de datos, abrí la solución en Visual Studio, andá a Herramientas > Administrador de paquetes NuGet > Consola del Administrador de paquetes y ejecutá:

Update-Database


4. Cargar los Datos Espaciales (Mapas)

Para poblar la base de datos con la cartografía real de CABA y GBA, utilizamos un script automatizado que descarga los datos de OpenStreetMap y los inyecta en el contenedor.

Navegá a la carpeta raíz del proyecto desde tu explorador de archivos de Windows.

Hacé doble clic en el archivo OSM-DATA-UPDATER-DOCKER.bat.

Aguardá a que finalice: El proceso descargará el mapa completo de Argentina (~400MB), recortará la zona del AMBA y cargará las geometrías en la base de datos. Tomará unos minutos dependiendo de tu conexión.


