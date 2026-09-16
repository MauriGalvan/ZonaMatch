# 🗺️ ZonaMatch — Guía para Levantar el Proyecto

Plataforma web geoespacial para evaluar, comparar y encontrar zonas en Argentina según necesidades de vivienda, movilidad, servicios (hospitales, escuelas, comercios) y estilo de vida.

---

## 📋 Requisitos Previos

Antes de comenzar, asegúrate de tener instalado en tu computadora:

* [Git](https://git-scm.com/)
* [Docker Desktop](https://www.docker.com/products/docker-desktop/) *(debe estar abierto y en ejecución)*
* [.NET SDK (v8 o superior)](https://dotnet.microsoft.com/download)
* [Node.js (v18 o superior)](https://nodejs.org/) con `npm`

---

## 🚀 Pasos para Levantar el Entorno Local

Sigue este orden para levantar el sistema completo (Base de Datos ➔ Backend ➔ Frontend):

### 1. Clonar el repositorio y configurar entorno

1. Clona el repositorio y entra a la carpeta:
   ```bash
   git clone https://github.com/MauriGalvan/ZonaMatch.git
   cd ZonaMatch
   ```

2. Crea tu archivo de variables de entorno `.env` a partir del ejemplo:
   * **En Windows (PowerShell):**
     ```powershell
     Copy-Item .env.example .env
     ```
   * **En Linux / macOS / Git Bash:**
     ```bash
     cp .env.example .env
     ```

---

### 2. Levantar la Base de Datos (Docker + PostGIS)

1. Abre **Docker Desktop** y espera a que el servicio esté corriendo (ícono verde).
2. En la raíz del proyecto, ejecuta:
   ```powershell
   docker compose up -d
   ```
   > 💡 *La primera vez descargará la imagen oficial de PostGIS y ejecutará automáticamente los scripts SQL de inicialización creando las extensiones espaciales, tablas e índices.*

3. *(Opcional)* Para verificar que el contenedor esté corriendo:
   ```powershell
   docker ps
   ```

---

### 3. Levantar el Backend (.NET Web API)

1. Abre una **nueva terminal** en la raíz del proyecto y corre:
   ```powershell
   dotnet run --project src/ZonaMatch.Api
   ```
2. La API quedará escuchando en:
   * **URL API:** `http://localhost:5222`
3. Puedes verificar que la API y la conexión a PostGIS estén funcionando entrando a:
   * 👉 **[http://localhost:5222/api/health](http://localhost:5222/api/health)**  
     *(Deberías ver un JSON con `"status": "Healthy"` y `"databaseConnected": true`)*.

---

### 4. Levantar el Frontend (React + Vite)

1. Abre una **tercera terminal**, entra a la carpeta `frontend` e instala las dependencias:
   ```powershell
   cd frontend
   npm install
   ```
2. Inicia el servidor de desarrollo:
   ```powershell
   npm run dev
   ```
3. Abre tu navegador e ingresa a:
   * 👉 **[http://localhost:5173](http://localhost:5173)**

---

## 📊 Resumen de Puertos y Servicios

| Servicio | Tecnología | Puerto Local | URL / Endpoint |
|---|---|---|---|
| **Base de Datos** | PostgreSQL 16 + PostGIS | `5432` | `localhost:5432` |
| **Backend API** | ASP.NET Core (.NET) | `5222` | `http://localhost:5222` |
| **Healthcheck** | Verificación API + BD | `5222` | `http://localhost:5222/api/health` |
| **Frontend Web** | React 19 + Vite | `5173` | `http://localhost:5173` |

---

## 🛠️ Solución de Problemas Frecuentes

### 1. Error: `failed to connect to the docker API / The system cannot find the file specified`
* **Causa:** Docker Desktop está cerrado o aún iniciándose.
* **Solución:** Abre la aplicación **Docker Desktop** en Windows, espera 15 segundos a que el motor inicie y vuelve a ejecutar `docker compose up -d`.

### 2. El puerto 5432 ya está en uso
* **Causa:** Tienes otra instancia local de PostgreSQL corriendo en tu máquina.
* **Solución:** Puedes detener el servicio local de PostgreSQL en los servicios de Windows, o cambiar el puerto en tu archivo `.env`:
  ```env
  POSTGRES_PORT=5433
  ```

### 3. Detener los servicios
* Para apagar el contenedor de la base de datos:
  ```powershell
  docker compose down
  ```
* Para detener el Backend o el Frontend, simplemente presiona `Ctrl + C` en sus respectivas terminales.
