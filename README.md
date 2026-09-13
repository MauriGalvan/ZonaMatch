# MapProyect

Mapa interactivo con React + Vite, Leaflet y OpenStreetMap. Muestra los partidos de Buenos Aires y las comunas de CABA con selección interactiva (hover y click).

## Requisitos

- Node.js 18 o superior
- npm

## Instalación

```bash
npm install
```

## Dependencias

### Dependencias de producción

- `react` y `react-dom` — biblioteca de React.
- `leaflet` — librería de mapas.
- `react-leaflet` — componentes de React para Leaflet (versión 5.x).
- `@turf/square-grid` — usado previamente para generar cuadrículas. Actualmente el código no lo usa, se puede eliminar de `package.json`.

### Dependencias de desarrollo

- `vite` — bundler y dev server.
- `@vitejs/plugin-react` — plugin de React para Vite.
- `eslint` y plugins (`eslint-plugin-react-hooks`, `eslint-plugin-react-refresh`, `@eslint/js`, `globals`) — linting.
- `@babel/core`, `@rolldown/plugin-babel` y `babel-plugin-react-compiler` — compilador de React.
- `@types/react` y `@types/react-dom` — tipos para desarrollo.

## Archivos de datos (GeoJSON)

Los archivos se importan directamente en `src/App.jsx` (se embeben en el bundle al compilar). Deben existir en la raíz del proyecto:

- `departamentos-buenos_aires.json` — partidos de la Provincia de Buenos Aires (134 polígonos).
- `departamentos-ciudad_autonoma_de_buenos_aires.json` — comunas de CABA (15 polígonos).

## Scripts

```bash
npm run dev      # inicia el servidor de desarrollo
npm run build    # genera la versión de producción en /dist
npm run preview  # sirve la versión compilada localmente
npm run lint     # ejecuta ESLint
```

## Uso

1. Ejecuta `npm install`.
2. Ejecuta `npm run dev`.
3. Abre la URL que muestra Vite (por defecto http://localhost:5173).

El mapa se centra en Buenos Aires. Pasa el cursor sobre un partido/comuna para resaltarlo y haz clic para seleccionarlo (se marca en verde; un nuevo clic la desmarca). La capa es fija: no se subdivide ni cambia según el zoom.