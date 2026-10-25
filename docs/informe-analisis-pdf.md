# Compartir el informe de análisis en PDF

## Cómo usarlo

Abrí un resultado en `/analisiszona` y presioná **Descargar informe PDF**. Funciona como visitante, con un análisis guardado y con un resultado grupal. El botón espera a que el ranking termine de calcularse o actualizarse.

Si el navegador ofrece compartir archivos, también aparece **Compartir PDF**. Abre el diálogo del sistema para elegir el destino. Si el navegador rechaza compartir el archivo, se descarga el PDF. Cancelar voluntariamente el diálogo no provoca una descarga ni se muestra como error.

El PDF contiene los resultados actuales del análisis completo: resumen, ranking ordenado, criterios y prioridades, presupuesto, movilidad y detalle de cada zona. El detalle incluye puntajes por criterio, límites y viajes a los lugares personales. En los grupos también incluye los participantes y sus pesos normalizados.

Las distancias del recorrido se muestran en kilómetros cuando están disponibles. Las estimaciones de tiempo se identifican como tales. Los datos faltantes se indican como no disponibles: no se convierten en ceros. Las distancias grupales no se inventan, porque el contrato actual de la API de grupos no las entrega.

## Diseño

Se usaron los valores del diseño existente de ZonaMatch: azul `#00367B`, turquesa `#00B3A9`, fondo claro `#F6F5F1`, texto azul oscuro y los colores de cada criterio. Se reutiliza el isotipo de `public/favicon.svg` y se incluyen las fuentes Nunito Sans Regular y ExtraBold en el PDF.

La primera parte muestra el resumen y el ranking. Cada zona comienza en una página nueva. Si una sección necesita más espacio, continúa en las páginas siguientes, conservando encabezado y número de página. El texto es seleccionable; no es una captura de pantalla.

El informe usa siempre la variante clara de la marca para mantener el contraste en pantalla e impresión, aunque la aplicación esté en modo oscuro. La fecha de generación y el nombre del archivo usan la zona horaria de Buenos Aires. La fecha corresponde a la generación del informe, no a la antigüedad de los datos de cada indicador.

## Qué cambié

Toda la implementación del feature está en `C:\mau\ZonaMatchSPA`. Los endpoints existentes ya entregan los resultados necesarios, por eso este feature no agrega endpoints, tablas ni migraciones en el backend.

| Archivo del frontend | Responsabilidad |
|---|---|
| `src/features/site-analysis/pdf/report-model.ts` | Convierte el flujo y la respuesta de puntajes en un modelo común para informes individuales y grupales. Ordena el ranking sin modificar los datos originales y conserva valores nulos y estimaciones. |
| `src/features/site-analysis/pdf/report-pdf.ts` | Dibuja el documento A4, aplica la paleta, incorpora las fuentes y controla los saltos de página. |
| `src/features/site-analysis/pdf/report-export.ts` | Carga las fuentes y el logo locales, genera un archivo PDF y permite descargarlo. Libera la URL temporal de la descarga. |
| `src/features/site-analysis/components/AnalysisPdfActions.tsx` | Maneja los botones, generación en progreso, errores, reintentos y la opción de compartir. Evita generaciones simultáneas. |
| `src/features/site-analysis/pages/ZoneAnalysisPage.tsx` | Agrega los botones al resultado individual y bloquea exportaciones durante cálculos o errores del ranking. |
| `src/features/group-search/components/GroupRankingAnalysisView.tsx` | Agrega los botones al resultado del grupo y los bloquea mientras se agrega una zona. |
| `src/assets/report/` | Incluye las dos fuentes y su licencia SIL Open Font License. |
| `package.json` y `package-lock.json` | Agregan `jspdf` en la versión exacta `4.2.1`. |

No borré funcionalidades ni reemplacé el cálculo de puntajes. En las dos pantallas existentes agregué un import y el componente de acciones del PDF. Las dependencias del generador se cargan al solicitar el informe, para que no formen parte de la carga inicial del resultado.

La generación ocurre en el navegador. No se agrega un servicio externo al que subir el informe ni un enlace público. El archivo contiene los lugares y direcciones presentes en el análisis y puede compartirse mediante descarga o el diálogo elegido por el usuario.

## Cómo funcionan los tests

Los tests están en `e2e/tests/analysis/` y se ejecutan con Playwright. Los datos de `report-fixtures.ts` son ejemplos controlados; no modifican la base de datos.

**11 tests del modelo y del generador**, en `report-model.spec.ts`:

1. Comprueban el orden del ranking, los tiempos, kilómetros, estimaciones y límites, y que la respuesta original no se modifique.
2. Verifican que un puntaje cero siga siendo cero y que los puntajes ausentes queden al final.
3. Comprueban los lugares sin recorrido y rechazan distancias inválidas.
4. Distinguen dos lugares que tienen el mismo nombre.
5. Verifican los participantes, pesos, presupuesto y medio de transporte de un grupo.
6. Señalan los análisis grupales antiguos cuyo desglose está incompleto.
7. Respetan un grupo sin límite de alquiler, aunque el flujo contenga un presupuesto anterior.
8. Impiden exportar un análisis sin resultados.
9. Comprueban un nombre de archivo seguro y la fecha argentina, incluyendo el cambio de día respecto de UTC.
10. Generan un PDF real, verifican su encabezado, sus fuentes embebidas y las páginas esperadas.
11. Generan un informe con un detalle extenso y verifican que se creen páginas adicionales.

**7 tests de navegador**, en `report-browser.spec.ts`, abren la aplicación real en Edge, con respuestas de API controladas:

1. Descargan el archivo y verifican que sea un PDF y tenga el nombre esperado.
2. Comprueban que no se pueda descargar mientras se calcula el ranking.
3. Simulan una falla al cargar las fuentes y verifican el reintento exitoso.
4. Descargan un informe grupal.
5. Simulan una API nativa de compartir compatible y verifican que reciba un archivo PDF.
6. Simulan la cancelación y comprueban que no haya descarga ni error.
7. Simulan un rechazo del sistema y verifican la descarga alternativa.

El diálogo nativo se simula: los tests no envían archivos a otra aplicación ni a una persona. La descarga sí se realiza en un navegador real.

Para ejecutarlos desde el frontend:

```powershell
node node_modules/@playwright/test/cli.js test --config playwright.report.config.ts
```

Para comprobar la compilación:

```powershell
npm run build
```

También se ejecutaron los 8 tests existentes de distancias:

```powershell
node node_modules/@playwright/test/cli.js test --config playwright.distance.config.ts
```

## Validación del documento

Además de los tests, se generó un PDF desde el botón de la aplicación, se extrajo su texto para verificar nombres, tiempos y kilómetros, y se renderizaron sus tres páginas para revisar visualmente márgenes, fuentes, colores y pies de página. Se revisó también el caso extenso para comprobar que el final del detalle no se pierda entre páginas.

El ejemplo está en `output/pdf/informe-zonamatch-ejemplo.pdf` en el repositorio del backend. Usa datos ilustrativos de los tests; los puntajes y viajes del ejemplo no son mediciones reales de esas zonas.

Fuentes de implementación: [jsPDF](https://github.com/parallax/jsPDF) y [Nunito Sans](https://github.com/googlefonts/NunitoSans), con su licencia incluida en el frontend.
