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

- `data/partidos-pba.json` — partidos de la Provincia de Buenos Aires (143 polígonos, fuente ARBA). Se importa en `src/App.jsx` y se embebe en el bundle.
- `data/departamentos-ciudad_autonoma_de_buenos_aires.json` — comunas de CABA (15 polígonos). Ídem.
- `public/radios-censales-pba-2022.json` — radios censales Censo 2022 de PBA (23.901, fuente datos.gba.gob.ar). Se sirve como estático y se filtra por código `DEPTO` al seleccionar un partido.
- `public/radios-censales-caba-2022.json` — radios censales Censo 2022 de CABA (3.820, fuente IDEEC, capa `RC_CNPHyV2022`). Se filtra por comuna (`ncom`).
- `src/data/partidoCenso2022.js` — mapeo generado: nombre de partido → código `DEPTO` (INDEC).
- `src/data/cabaComunaCenso2022.js` — mapeo generado: comuna → código de radio (`COMUNA NN`).

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

El mapa se centra en Buenos Aires. Pasa el cursor sobre un partido/comuna para resaltarlo y haz clic para seleccionarlo (se marca en verde; un nuevo clic la desmarca). Al seleccionar una zona se muestran sus radios censales del Censo 2022 como subcapa, con zoom automático y el contador de radios en el panel lateral. De la misma forma, haz clic sobre un radio para seleccionarlo (se resalta en azul) y ver su fracción, número de radio y código censal en el panel lateral; un nuevo clic lo desmarca.

### Punto en el mapa

Tras seleccionar un partido, hacé clic en cualquiera de sus radios (subcapa censal): se fija un marcador en el punto exacto del clic junto con los datos del radio (fracción/radio/código). Botón **Quitar punto** para limpiarlo. La información de servicios se consumirá de una base de datos PostgreSQL servida por un backend propio (en desarrollo).