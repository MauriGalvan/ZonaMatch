import { useState, useEffect, useRef } from 'react';
import L from 'leaflet';

interface Poi {
  id: string;
  name: string;
  category: 'salud' | 'educacion' | 'abastecimiento' | 'espacios_verdes' | 'transporte' | 'seguridad' | 'finanzas' | 'gastronomia' | 'deportes';
  subcategory: string;
  lat: number;
  lon: number;
}

interface EvaluationResult {
  zoneName: string;
  lat: number;
  lon: number;
  totalScore: number;
  confidence: number;
  subscores: {
    salud: number;
    educacion: number;
    abastecimiento: number;
    espacios_verdes: number;
    movilidad: number;
    clima: number;
    seguridad: number;
    gastronomia: number;
  };
  metrics: {
    healthPoisCount: number;
    educationPoisCount: number;
    shopPoisCount: number;
    greenAreasCount: number;
    transitStopsCount: number;
    securityPoisCount: number;
    financePoisCount: number;
    gastronomyPoisCount: number;
    sportsPoisCount: number;
    travelDurationMin: number;
    travelDistanceKm: number;
    currentTemp: number;
    precipitationMm: number;
  };
  pros: string[];
  cons: string[];
  explanation: string;
  routeGeoJson?: any;
  pois?: Poi[];
}

// Límites de cobertura metropolitana exclusiva (CABA y AMBA)
const AMBA_BOUNDS_COORDS = {
  southWest: [-35.15, -59.35] as [number, number], // Cañuelas / Marcos Paz / La Plata
  northEast: [-34.15, -57.80] as [number, number], // Zárate / Campana / Tigre / Costanera
  center: [-34.6037, -58.3816] as [number, number]
};

const PRESET_ZONES = [
  { name: 'Palermo, CABA', lat: -34.5889, lon: -58.4306 },
  { name: 'Caballito, CABA', lat: -34.6201, lon: -58.4443 },
  { name: 'Belgrano, CABA', lat: -34.5627, lon: -58.4564 },
  { name: 'Vicente López, AMBA Norte', lat: -34.5298, lon: -58.4736 },
  { name: 'San Isidro, AMBA Norte', lat: -34.4718, lon: -58.5286 },
  { name: 'Ramos Mejía, AMBA Oeste', lat: -34.6469, lon: -58.5639 },
  { name: 'Quilmes Centro, AMBA Sur', lat: -34.7242, lon: -58.2608 },
  { name: 'Lanús Centro, AMBA Sur', lat: -34.7072, lon: -58.3934 }
];

export function App() {
  const mapContainer = useRef<HTMLDivElement>(null);
  const map = useRef<L.Map | null>(null);
  const currentTileLayer = useRef<L.TileLayer | null>(null);
  const markersLayer = useRef<L.LayerGroup | null>(null);
  const routeLayer = useRef<L.GeoJSON | null>(null);

  // Form State
  const [zoneQuery, setZoneQuery] = useState('Palermo, CABA');
  const [selectedCoords, setSelectedCoords] = useState<{ lat: number; lon: number }>({
    lat: -34.5889,
    lon: -58.4306
  });

  const [workAddress, setWorkAddress] = useState('Obelisco, Microcentro CABA');
  const [workCoords, setWorkCoords] = useState<{ lat: number; lon: number }>({
    lat: -34.6037,
    lon: -58.3816
  });
  const [transportMode, setTransportMode] = useState<'driving' | 'walking'>('driving');
  const [mapTheme, setMapTheme] = useState<'voyager' | 'dark' | 'satellite'>('voyager');

  // Preferences (Weights: 0 to 3)
  const [weights, setWeights] = useState({
    seguridad: 3,
    salud: 2,
    educacion: 2,
    abastecimiento: 2,
    gastronomia: 2,
    espacios_verdes: 3,
    movilidad: 3,
    clima: 1
  });

  // System & Evaluation State
  const [backendHealth, setBackendHealth] = useState<{ status: string; db: boolean } | null>(null);
  const [isEvaluating, setIsEvaluating] = useState(false);
  const [pipelineStep, setPipelineStep] = useState<string>('Listo');
  const [evaluation, setEvaluation] = useState<EvaluationResult | null>(null);
  const [currentPois, setCurrentPois] = useState<Poi[]>([]);
  const [activeCategoryFilter, setActiveCategoryFilter] = useState<string>('todos');
  const [coverageNotice, setCoverageNotice] = useState<string | null>(null);

  // 1. Check Backend Health
  useEffect(() => {
    fetch('http://localhost:5222/api/health')
      .then(res => res.json())
      .then(data => {
        setBackendHealth({ status: data.status, db: data.databaseConnected });
      })
      .catch(() => {
        setBackendHealth({ status: 'Local Mode', db: false });
      });
  }, []);

  // 2. Initialize Leaflet Map
  useEffect(() => {
    if (!mapContainer.current || map.current) return;

    const ambaBounds = L.latLngBounds(
      AMBA_BOUNDS_COORDS.southWest,
      AMBA_BOUNDS_COORDS.northEast
    );

    const leafletMap = L.map(mapContainer.current, {
      center: AMBA_BOUNDS_COORDS.center,
      zoom: 13,
      minZoom: 10,
      maxZoom: 19,
      maxBounds: ambaBounds,
      maxBoundsViscosity: 1.0,
      zoomControl: false
    });

    L.control.zoom({ position: 'topright' }).addTo(leafletMap);

    // Initial Street tile layer (High clarity streets with Spanish names, clean without watermarks)
    const layer = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}', {
      maxZoom: 19,
      attribution: '&copy; Esri, HERE, Garmin, OpenStreetMap'
    }).addTo(leafletMap);

    currentTileLayer.current = layer;
    markersLayer.current = L.layerGroup().addTo(leafletMap);
    map.current = leafletMap;

    // Trigger map invalidation for flex layout
    setTimeout(() => {
      leafletMap.invalidateSize();
    }, 150);
    setTimeout(() => {
      leafletMap.invalidateSize();
    }, 500);

    // Execute first evaluation
    runEvaluation('Palermo, CABA', 'Obelisco, Microcentro CABA');

    return () => {
      leafletMap.remove();
      map.current = null;
    };
  }, []);

  // Change Map Theme
  const switchTheme = (theme: 'voyager' | 'dark' | 'satellite') => {
    setMapTheme(theme);
    if (!map.current || !currentTileLayer.current) return;

    map.current.removeLayer(currentTileLayer.current);

    let url = 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}';
    let attr = '&copy; Esri &copy; OpenStreetMap';

    if (theme === 'dark') {
      url = 'https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Dark_Gray_Base/MapServer/tile/{z}/{y}/{x}';
      attr = '&copy; Esri, DeLorme, NAVTEQ';
    } else if (theme === 'satellite') {
      url = 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}';
      attr = '&copy; Esri, Maxar, Earthstar Geographics';
    }

    const newLayer = L.tileLayer(url, {
      maxZoom: 19,
      attribution: attr
    }).addTo(map.current);

    currentTileLayer.current = newLayer;

    if (evaluation) {
      drawOnMap(evaluation, evaluation.pois || currentPois, evaluation.routeGeoJson, workCoords, workAddress, activeCategoryFilter);
    }
  };

  // Filter POIs on map by category
  const handleFilterCategory = (category: string) => {
    setActiveCategoryFilter(category);
    if (evaluation) {
      drawOnMap(
        evaluation,
        evaluation.pois || currentPois,
        evaluation.routeGeoJson,
        workCoords,
        workAddress,
        category
      );
    }
  };

  // Helper: Verifica si una coordenada está dentro de CABA y AMBA
  const isInsideAmba = (lat: number, lon: number) => {
    return lat >= AMBA_BOUNDS_COORDS.southWest[0] &&
           lat <= AMBA_BOUNDS_COORDS.northEast[0] &&
           lon >= AMBA_BOUNDS_COORDS.southWest[1] &&
           lon <= AMBA_BOUNDS_COORDS.northEast[1];
  };

  // Geocode location acotada a CABA y AMBA (con viewbox y validación)
  const geocodeLocation = async (query: string): Promise<{ name: string; lat: number; lon: number } | null> => {
    if (!query || query.trim().length === 0) return null;
    const cleanQuery = query.trim();

    // 1. Nominatim acotado al AMBA (viewbox: lon_min,lat_max,lon_max,lat_min)
    const viewboxAmba = `${AMBA_BOUNDS_COORDS.southWest[1]},${AMBA_BOUNDS_COORDS.northEast[0]},${AMBA_BOUNDS_COORDS.northEast[1]},${AMBA_BOUNDS_COORDS.southWest[0]}`;
    try {
      const nomUrl = `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(cleanQuery)}&format=json&limit=1&countrycodes=ar&viewbox=${viewboxAmba}&bounded=1`;
      const res = await fetch(nomUrl, {
        headers: { 'Accept': 'application/json' }
      });
      const data = await res.json();
      if (data && data.length > 0) {
        return {
          name: data[0].display_name.split(',').slice(0, 3).join(','),
          lat: parseFloat(data[0].lat),
          lon: parseFloat(data[0].lon)
        };
      }
    } catch (e) {
      console.warn('Nominatim bounded error, trying standard search:', e);
    }

    // 2. Nominatim estándar (si no encontró en el viewbox estricto)
    try {
      const nomUrl = `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(cleanQuery + ', Buenos Aires')}&format=json&limit=1&countrycodes=ar`;
      const res = await fetch(nomUrl, {
        headers: { 'Accept': 'application/json' }
      });
      const data = await res.json();
      if (data && data.length > 0) {
        return {
          name: data[0].display_name.split(',').slice(0, 3).join(','),
          lat: parseFloat(data[0].lat),
          lon: parseFloat(data[0].lon)
        };
      }
    } catch (e) {
      console.warn('Nominatim standard fallback error:', e);
    }

    // 3. Georef Localidades
    try {
      const locUrl = `https://apis.datos.gob.ar/georef/api/localidades?nombre=${encodeURIComponent(cleanQuery)}&max=1`;
      const locRes = await fetch(locUrl);
      const locData = await locRes.json();
      if (locData.localidades && locData.localidades.length > 0) {
        const l = locData.localidades[0];
        return {
          name: `${l.nombre}, ${l.departamento?.nombre || ''}`,
          lat: l.centroide.lat,
          lon: l.centroide.lon
        };
      }
    } catch (e) {
      console.warn('Georef error:', e);
    }

    return null;
  };

  // Run Zone Evaluation Pipeline
  const runEvaluation = async (customZoneQuery?: string, customDestQuery?: string) => {
    setIsEvaluating(true);
    setCoverageNotice(null);

    const targetQuery = customZoneQuery !== undefined ? customZoneQuery : zoneQuery;
    const destQuery = customDestQuery !== undefined ? customDestQuery : workAddress;

    try {
      // Step 1: Georef (Normalización de Zona)
      setPipelineStep('Georef AR');
      let targetLat = selectedCoords.lat;
      let targetLon = selectedCoords.lon;
      let zoneName = targetQuery;

      const zoneGeo = await geocodeLocation(targetQuery);
      if (zoneGeo) {
        targetLat = zoneGeo.lat;
        targetLon = zoneGeo.lon;
        zoneName = zoneGeo.name;
        setSelectedCoords({ lat: targetLat, lon: targetLon });
      }

      // Validación de Cobertura CABA / AMBA (Frontend + Backend)
      let isInside = isInsideAmba(targetLat, targetLon);

      try {
        const valRes = await fetch('http://localhost:5222/api/geo/validate-coverage', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ latitude: targetLat, longitude: targetLon })
        });
        if (valRes.ok) {
          const valData = await valRes.json();
          isInside = valData.isInside;
        }
      } catch {
        // En modo local o standby de API, mantiene validación espacial local
      }

      if (!isInside) {
        setCoverageNotice(`⚠️ "${zoneName}" queda fuera de la cobertura de CABA y AMBA. ZonaMatch MVP opera exclusivamente en el área metropolitana de Buenos Aires.`);
        setIsEvaluating(false);
        setPipelineStep('Listo');
        return;
      }

      // Geocodificación de Destino Habitual (Trabajo/Estudio)
      let currentWorkLat = workCoords.lat;
      let currentWorkLon = workCoords.lon;
      let currentWorkName = destQuery;

      if (destQuery && destQuery.trim().length > 0) {
        const destGeo = await geocodeLocation(destQuery);
        if (destGeo) {
          currentWorkLat = destGeo.lat;
          currentWorkLon = destGeo.lon;
          currentWorkName = destGeo.name;
          setWorkCoords({ lat: currentWorkLat, lon: currentWorkLon });
        }
      }

      // Step 2: Overpass (OSM POIs reales de la zona)
      setPipelineStep('Overpass OSM');
      const pois: Poi[] = await fetchOverpassPois(targetLat, targetLon);
      setCurrentPois(pois);

      // Step 3: OSRM (Ruteo vial real y tiempos)
      setPipelineStep('OSRM Routing');
      const mobility = await fetchOsrmRoute(targetLat, targetLon, currentWorkLat, currentWorkLon, transportMode);

      // Step 4: Open-Meteo (Clima y precipitaciones)
      setPipelineStep('Open-Meteo');
      const weather = await fetchOpenMeteo(targetLat, targetLon);

      // Step 5: PostGIS & ZonaMatch Score Calculation
      setPipelineStep('ZonaMatch Score');
      const calculatedResult = computeScore(
        zoneName,
        targetLat,
        targetLon,
        pois,
        mobility,
        weather,
        weights
      );

      setEvaluation(calculatedResult);
      drawOnMap(calculatedResult, pois, mobility.routeGeoJson, { lat: currentWorkLat, lon: currentWorkLon }, currentWorkName);

    } catch (err) {
      console.error('Error during evaluation:', err);
    } finally {
      setIsEvaluating(false);
      setPipelineStep('Completado');
    }
  };

  // Helper: Real OpenStreetMap POI fetcher via Overpass API
  const fetchOverpassPois = async (lat: number, lon: number): Promise<Poi[]> => {
    // 1300m radius around target coordinates
    const query = `[out:json][timeout:10];
(
  // Salud
  node["amenity"~"hospital|clinic|pharmacy|doctors"](around:1300,${lat},${lon});
  way["amenity"~"hospital|clinic|pharmacy|doctors"](around:1300,${lat},${lon});

  // Educacion
  node["amenity"~"school|university|college|kindergarten"](around:1300,${lat},${lon});
  way["amenity"~"school|university|college|kindergarten"](around:1300,${lat},${lon});

  // Abastecimiento
  node["shop"~"supermarket|convenience|bakery"](around:1300,${lat},${lon});
  way["shop"~"supermarket|convenience|bakery"](around:1300,${lat},${lon});

  // Espacios verdes
  node["leisure"~"park|garden|pitch"](around:1300,${lat},${lon});
  way["leisure"~"park|garden|pitch"](around:1300,${lat},${lon});

  // Transporte
  node["highway"="bus_stop"](around:1300,${lat},${lon});
  node["railway"~"subway_entrance|station"](around:1300,${lat},${lon});
  node["public_transport"~"stop_position|platform"](around:1300,${lat},${lon});

  // Opcion A: Seguridad (Comisarías y Bomberos en 1600m)
  node["amenity"~"police|fire_station"](around:1600,${lat},${lon});
  way["amenity"~"police|fire_station"](around:1600,${lat},${lon});

  // Opcion A: Finanzas (Bancos y Cajeros)
  node["amenity"~"bank|atm"](around:1300,${lat},${lon});

  // Opcion B: Gastronomia y Ocio (Cafes, Restaurantes, Bares, Cines)
  node["amenity"~"cafe|restaurant|bar|pub|ice_cream|fast_food|cinema|theatre"](around:1300,${lat},${lon});
  way["amenity"~"cafe|restaurant|bar|pub|cinema"](around:1300,${lat},${lon});

  // Opcion B: Deportes y Fitness
  node["leisure"~"fitness_centre|sports_centre"](around:1300,${lat},${lon});
  way["leisure"~"fitness_centre|sports_centre"](around:1300,${lat},${lon});
);
out center 80;`;

    const endpoints = [
      'https://overpass-api.de/api/interpreter',
      'https://overpass.kumi.systems/api/interpreter'
    ];

    for (const url of endpoints) {
      try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 8500);

        const res = await fetch(url, {
          method: 'POST',
          body: 'data=' + encodeURIComponent(query),
          headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'Accept': 'application/json'
          },
          signal: controller.signal
        });

        clearTimeout(timeoutId);

        if (!res.ok) continue;

        const data = await res.json();
        if (!data || !Array.isArray(data.elements)) continue;

        const parsedPois: Poi[] = [];

        for (const el of data.elements) {
          const pLat = el.lat ?? el.center?.lat;
          const pLon = el.lon ?? el.center?.lon;
          if (!pLat || !pLon) continue;

          const tags = el.tags || {};
          let category: Poi['category'] = 'abastecimiento';
          let subcategory = 'Comercio';

          if (tags.amenity === 'police' || tags.amenity === 'fire_station') {
            category = 'seguridad';
            subcategory = tags.amenity === 'police' ? 'Comisaría / Policía' : 'Cuartel de Bomberos';
          } else if (tags.amenity === 'bank' || tags.amenity === 'atm') {
            category = 'finanzas';
            subcategory = tags.amenity === 'bank' ? 'Banco' : 'Cajero Automático (ATM)';
          } else if (['cafe', 'restaurant', 'bar', 'pub', 'ice_cream', 'fast_food', 'cinema', 'theatre'].includes(tags.amenity)) {
            category = 'gastronomia';
            subcategory = tags.amenity === 'cafe' ? 'Cafetería' :
              tags.amenity === 'bar' || tags.amenity === 'pub' ? 'Bar / Pub' :
                tags.amenity === 'ice_cream' ? 'Heladería' :
                  tags.amenity === 'cinema' ? 'Cine' :
                    tags.amenity === 'theatre' ? 'Teatro' : 'Restaurante';
          } else if (tags.leisure === 'fitness_centre' || tags.leisure === 'sports_centre') {
            category = 'deportes';
            subcategory = tags.leisure === 'fitness_centre' ? 'Gimnasio / Fitness' : 'Club / Polideportivo';
          } else if (tags.amenity === 'hospital' || tags.amenity === 'clinic' || tags.amenity === 'pharmacy' || tags.amenity === 'doctors') {
            category = 'salud';
            subcategory = tags.amenity === 'hospital' ? 'Hospital' :
              tags.amenity === 'clinic' ? 'Clínica' :
                tags.amenity === 'pharmacy' ? 'Farmacia' : 'Consultorio';
          } else if (tags.amenity === 'school' || tags.amenity === 'university' || tags.amenity === 'college' || tags.amenity === 'kindergarten') {
            category = 'educacion';
            subcategory = tags.amenity === 'university' ? 'Universidad' :
              tags.amenity === 'kindergarten' ? 'Jardín de Infantes' :
                tags.amenity === 'college' ? 'Instituto Terciario' : 'Escuela / Colegio';
          } else if (tags.shop || tags.amenity === 'marketplace') {
            category = 'abastecimiento';
            subcategory = tags.shop === 'supermarket' ? 'Supermercado' :
              tags.shop === 'bakery' ? 'Panadería' :
                tags.shop === 'convenience' ? 'Comercio de Barrio' : 'Comercio';
          } else if (tags.leisure === 'park' || tags.leisure === 'garden' || tags.leisure === 'pitch') {
            category = 'espacios_verdes';
            subcategory = tags.leisure === 'park' ? 'Parque / Plaza' :
              tags.leisure === 'pitch' ? 'Espacio Deportivo' : 'Espacio Verde';
          } else if (tags.highway === 'bus_stop' || tags.railway || tags.public_transport) {
            category = 'transporte';
            subcategory = tags.railway === 'subway_entrance' ? 'Boca de Subte' :
              tags.railway === 'station' ? 'Estación Ferroviaria' : 'Parada de Colectivo';
          }

          let name = tags.name;
          if (!name || name.trim().length === 0) {
            name = tags.brand || `${subcategory} ${parsedPois.length + 1}`;
          }

          parsedPois.push({
            id: `${el.type}_${el.id}`,
            name,
            category,
            subcategory,
            lat: pLat,
            lon: pLon
          });
        }

        if (parsedPois.length > 0) {
          return parsedPois;
        }
      } catch (e) {
        console.warn(`Overpass error on ${url}:`, e);
      }
    }

    return [];
  };

  // Helper: OSRM Public Routing
  const fetchOsrmRoute = async (lat1: number, lon1: number, lat2: number, lon2: number, mode: string) => {
    const profile = mode === 'walking' ? 'foot' : 'driving';
    try {
      const url = `https://router.project-osrm.org/route/v1/${profile}/${lon1},${lat1};${lon2},${lat2}?overview=full&geometries=geojson`;
      const res = await fetch(url);
      const data = await res.json();
      if (data.routes && data.routes.length > 0) {
        const route = data.routes[0];
        return {
          durationMin: Math.round(route.duration / 60),
          distanceKm: Number((route.distance / 1000).toFixed(1)),
          routeGeoJson: route.geometry
        };
      }
    } catch (e) {
      console.log('OSRM timeout, using estimated travel metric');
    }
    return {
      durationMin: mode === 'walking' ? 62 : 18,
      distanceKm: 5.6,
      routeGeoJson: null
    };
  };

  // Helper: Open-Meteo Current Weather
  const fetchOpenMeteo = async (lat: number, lon: number) => {
    try {
      const url = `https://api.open-meteo.com/v1/forecast?latitude=${lat}&longitude=${lon}&current=temperature_2m,precipitation&timezone=America%2FArgentina%2FBuenos_Aires`;
      const res = await fetch(url);
      const data = await res.json();
      return {
        temp: data.current?.temperature_2m ?? 22.5,
        precipitation: data.current?.precipitation ?? 0.0
      };
    } catch (e) {
      return { temp: 21.0, precipitation: 0.0 };
    }
  };

  // Helper: Multi-Criteria Normalized Scoring
  const computeScore = (
    zoneName: string,
    lat: number,
    lon: number,
    pois: Poi[],
    mobility: { durationMin: number; distanceKm: number },
    weather: { temp: number; precipitation: number },
    userWeights: typeof weights
  ): EvaluationResult => {
    const healthCount = pois.filter(p => p.category === 'salud').length;
    const eduCount = pois.filter(p => p.category === 'educacion').length;
    const shopCount = pois.filter(p => p.category === 'abastecimiento').length;
    const greenCount = pois.filter(p => p.category === 'espacios_verdes').length;
    const transitCount = pois.filter(p => p.category === 'transporte').length;
    const securityCount = pois.filter(p => p.category === 'seguridad').length;
    const financeCount = pois.filter(p => p.category === 'finanzas').length;
    const gastroCount = pois.filter(p => p.category === 'gastronomia').length;
    const sportsCount = pois.filter(p => p.category === 'deportes').length;

    // Normalización adaptativa basada en servicios reales detectados
    const subSeguridad = securityCount === 0 ? 35 : Math.min(100, 45 + securityCount * 30);
    const subSalud = healthCount === 0 ? 15 : Math.min(100, 20 + healthCount * 25);
    const subEducacion = eduCount === 0 ? 15 : Math.min(100, 20 + eduCount * 25);
    const subAbastecimiento = shopCount === 0 ? 20 : Math.min(100, 25 + shopCount * 20);
    const subGastronomia = gastroCount === 0 ? 20 : Math.min(100, 25 + gastroCount * 15);
    const subVerdes = greenCount === 0 ? 15 : Math.min(100, 25 + greenCount * 30);

    // Mobility: optimal < 20 min, drops to 0 at 60 min
    const subMovilidad = Math.max(10, Math.min(100, Math.round(100 - (mobility.durationMin - 15) * 2.2)));

    // Weather: optimal between 16 and 26 °C
    const subClima = Math.round(95 - Math.abs(weather.temp - 22) * 2);

    // Weighted average
    const totalWeight = userWeights.seguridad + userWeights.salud + userWeights.educacion +
      userWeights.abastecimiento + userWeights.gastronomia +
      userWeights.espacios_verdes + userWeights.movilidad + userWeights.clima;

    const weightedSum = (subSeguridad * userWeights.seguridad) +
      (subSalud * userWeights.salud) +
      (subEducacion * userWeights.educacion) +
      (subAbastecimiento * userWeights.abastecimiento) +
      (subGastronomia * userWeights.gastronomia) +
      (subVerdes * userWeights.espacios_verdes) +
      (subMovilidad * userWeights.movilidad) +
      (subClima * userWeights.clima);

    const totalScore = Math.round(weightedSum / (totalWeight || 1));

    // Dynamic Pros & Cons based on actual data
    const pros: string[] = [];
    const cons: string[] = [];

    if (subMovilidad >= 75) {
      pros.push(`Excelente conectividad al destino: solo ${mobility.durationMin} min de viaje (${mobility.distanceKm} km).`);
    } else {
      cons.push(`Tiempo de viaje al destino considerable (${mobility.durationMin} min).`);
    }

    if (securityCount >= 1) pros.push(`Seguridad: ${securityCount} comisaría(s) o destacamento(s) en el área.`);
    else cons.push('Sin comisarías o destacamentos policiales inmediatos registrados.');

    if (healthCount >= 2) pros.push(`Buena cobertura médica con ${healthCount} centros de salud y farmacias cercanas.`);
    else if (healthCount === 0) cons.push('Sin hospitales o farmacias mapeadas en el radio inmediato (1.3 km).');

    if (gastroCount >= 3) pros.push(`Gran oferta gastronómica y social con ${gastroCount} cafés, bares y restaurantes.`);
    if (financeCount >= 1) pros.push(`Servicios financieros con ${financeCount} banco(s) o cajero(s) accesibles.`);
    if (sportsCount >= 1) pros.push(`Infraestructura deportiva con ${sportsCount} gimnasio(s) o centros de fitness.`);

    if (shopCount >= 2) pros.push(`Gran abastecimiento con ${shopCount} comercios y supermercados.`);
    else if (shopCount === 0) cons.push('Poca presencia de comercios de proximidad registrados.');

    if (greenCount >= 1) pros.push(`Acceso a ${greenCount} plaza(s) o espacio(s) verde(s) para recreación.`);
    else cons.push('Escasez de plazas o espacios verdes identificados en la zona.');

    if (transitCount >= 2) pros.push(`Excelente acceso a transporte público (${transitCount} paradas/estaciones).`);

    const explanation = `La zona de ${zoneName} presenta un ajuste del ${totalScore}% para tu perfil. ${pros.length > 0 ? 'Puntos a favor: ' + pros.slice(0, 2).join(' ') : ''} ${cons.length > 0 ? 'A considerar: ' + cons[0] : ''}`;

    const confidence = pois.length >= 10 ? 98 : pois.length >= 4 ? 86 : 68;

    return {
      zoneName,
      lat,
      lon,
      totalScore,
      confidence,
      subscores: {
        salud: subSalud,
        educacion: subEducacion,
        abastecimiento: subAbastecimiento,
        espacios_verdes: subVerdes,
        movilidad: subMovilidad,
        clima: subClima,
        seguridad: subSeguridad,
        gastronomia: subGastronomia
      },
      metrics: {
        healthPoisCount: healthCount,
        educationPoisCount: eduCount,
        shopPoisCount: shopCount,
        greenAreasCount: greenCount,
        transitStopsCount: transitCount,
        securityPoisCount: securityCount,
        financePoisCount: financeCount,
        gastronomyPoisCount: gastroCount,
        sportsPoisCount: sportsCount,
        travelDurationMin: mobility.durationMin,
        travelDistanceKm: mobility.distanceKm,
        currentTemp: weather.temp,
        precipitationMm: weather.precipitation
      },
      pros,
      cons,
      explanation,
      pois
    };
  };

  // Draw Markers & Route on Map
  const drawOnMap = (
    res: EvaluationResult,
    pois: Poi[],
    routeGeoJson: any,
    destCoords: { lat: number; lon: number } = workCoords,
    destLabel: string = workAddress,
    categoryFilter: string = activeCategoryFilter
  ) => {
    if (!map.current || !markersLayer.current) return;

    // Clear previous markers & route
    markersLayer.current.clearLayers();
    if (routeLayer.current) {
      map.current.removeLayer(routeLayer.current);
      routeLayer.current = null;
    }

    // Adjust view to fit both zone and destination
    try {
      const bounds = L.latLngBounds([
        [res.lat, res.lon],
        [destCoords.lat, destCoords.lon]
      ]);
      map.current.fitBounds(bounds, { padding: [80, 80], maxZoom: 15 });
    } catch {
      map.current.flyTo([res.lat, res.lon], 14, { duration: 1.2 });
    }

    // Main Zone Marker
    const iconZone = L.divIcon({
      className: 'custom-marker marker-zone',
      html: '📍',
      iconSize: [38, 38],
      iconAnchor: [19, 19]
    });
    L.marker([res.lat, res.lon], { icon: iconZone })
      .bindPopup(`<strong>Zona Evaluada:</strong><br/>${res.zoneName}`)
      .addTo(markersLayer.current);

    // Destination Marker (Work)
    const iconWork = L.divIcon({
      className: 'custom-marker marker-work',
      html: '💼',
      iconSize: [34, 34],
      iconAnchor: [17, 17]
    });
    L.marker([destCoords.lat, destCoords.lon], { icon: iconWork })
      .bindPopup(`<strong>Destino:</strong><br/>${destLabel}`)
      .addTo(markersLayer.current);

    // POI Markers
    const visiblePois = categoryFilter === 'todos'
      ? pois
      : pois.filter(p => p.category === categoryFilter);

    visiblePois.forEach(poi => {
      const icon = poi.category === 'salud' ? '🏥' :
        poi.category === 'educacion' ? '🎓' :
          poi.category === 'abastecimiento' ? '🛒' :
            poi.category === 'espacios_verdes' ? '🌳' :
              poi.category === 'transporte' ? '🚌' :
                poi.category === 'seguridad' ? (poi.subcategory.includes('Bomberos') ? '🚒' : '👮') :
                  poi.category === 'finanzas' ? (poi.subcategory.includes('Banco') ? '🏦' : '🏧') :
                    poi.category === 'gastronomia' ? (poi.subcategory.includes('Café') ? '☕' : poi.subcategory.includes('Bar') ? '🍺' : '🍽️') :
                      '🏋️';

      const poiIcon = L.divIcon({
        className: `custom-marker marker-${poi.category}`,
        html: icon,
        iconSize: [30, 30],
        iconAnchor: [15, 15]
      });

      L.marker([poi.lat, poi.lon], { icon: poiIcon })
        .bindPopup(`
          <div style="color: #f8fafc; font-family: sans-serif; padding: 4px; min-width: 140px;">
            <div style="font-size: 10px; text-transform: uppercase; font-weight: bold; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 2px;">
              ${poi.category.toUpperCase()}
            </div>
            <strong style="color: #ffffff; font-size: 13px; line-height: 1.2; display: block;">${poi.name}</strong>
            <span style="font-size: 11px; color: #94a3b8; margin-top: 2px; display: block;">${poi.subcategory}</span>
          </div>
        `)
        .addTo(markersLayer.current!);
    });

    // Draw OSRM Route Layer
    if (routeGeoJson) {
      routeLayer.current = L.geoJSON(routeGeoJson, {
        style: {
          color: '#6366f1',
          weight: 5,
          opacity: 0.85,
          dashArray: '4, 8'
        }
      }).addTo(map.current);
    }
  };

  return (
    <div className="app-container">
      {/* Top Header */}
      <header className="app-header">
        <div className="brand-area">
          <div className="brand-logo-icon">Z</div>
          <span className="brand-title">ZonaMatch</span>
          <span className="brand-tag">MVP Argentina</span>
        </div>

        {/* Pipeline Step Tracker */}
        <div className="pipeline-pill-container">
          <span className={`pipeline-step ${pipelineStep === 'Georef AR' ? 'active' : ''}`}>1. Georef</span>
          <span className="pipeline-arrow">→</span>
          <span className={`pipeline-step ${pipelineStep === 'Overpass OSM' ? 'active' : ''}`}>2. Overpass</span>
          <span className="pipeline-arrow">→</span>
          <span className={`pipeline-step ${pipelineStep === 'OSRM Routing' ? 'active' : ''}`}>3. OSRM</span>
          <span className="pipeline-arrow">→</span>
          <span className={`pipeline-step ${pipelineStep === 'Open-Meteo' ? 'active' : ''}`}>4. Open-Meteo</span>
          <span className="pipeline-arrow">→</span>
          <span className={`pipeline-step ${pipelineStep === 'ZonaMatch Score' ? 'active' : ''}`}>5. PostGIS & Score</span>
        </div>

        {/* Backend & DB Status Badge */}
        <div className="status-badge">
          <span className="status-dot"></span>
          <span>API: {backendHealth?.status || 'Conectando...'}</span>
          <span style={{ opacity: 0.5 }}>|</span>
          <span style={{ color: backendHealth?.db ? '#34d399' : '#fbbf24' }}>
            {backendHealth?.db ? 'PostGIS Conectado' : 'PostGIS Standby'}
          </span>
        </div>
      </header>

      {/* Main Workspace */}
      <div className="main-view">
        {/* Map Canvas */}
        <div ref={mapContainer} className="map-viewport" />

        {/* Floating Map Style Switcher */}
        <div className="map-style-switcher">
          <button
            className={`map-style-btn ${mapTheme === 'voyager' ? 'active' : ''}`}
            onClick={() => switchTheme('voyager')}
            title="Vista de calles nítidas con nombres en español"
          >
            🗺️ Calles (Voyager)
          </button>
          <button
            className={`map-style-btn ${mapTheme === 'dark' ? 'active' : ''}`}
            onClick={() => switchTheme('dark')}
            title="Modo Oscuro CartoDB"
          >
            🌙 Modo Oscuro
          </button>
          <button
            className={`map-style-btn ${mapTheme === 'satellite' ? 'active' : ''}`}
            onClick={() => switchTheme('satellite')}
            title="Vista Satelital Esri"
          >
            🛰️ Satélite
          </button>
        </div>

        {/* Floating Category Filter Pills */}
        <div className="map-category-filter">
          {[
            { id: 'todos', label: 'Todos' },
            { id: 'seguridad', label: '👮 Seguridad' },
            { id: 'gastronomia', label: '☕ Gastronomía' },
            { id: 'salud', label: '🏥 Salud' },
            { id: 'educacion', label: '🎓 Educación' },
            { id: 'abastecimiento', label: '🛒 Compras' },
            { id: 'espacios_verdes', label: '🌳 Plazas' },
            { id: 'transporte', label: '🚌 Transporte' },
            { id: 'finanzas', label: '🏧 Finanzas' },
            { id: 'deportes', label: '🏋️ Deportes' },
          ].map(cat => (
            <button
              key={cat.id}
              className={`cat-filter-btn ${activeCategoryFilter === cat.id ? 'active' : ''}`}
              onClick={() => handleFilterCategory(cat.id)}
            >
              {cat.label}
            </button>
          ))}
        </div>

        {/* Floating Left Control Sidebar */}
        <aside className="floating-panel sidebar-left">
          <div className="panel-header">
            <h2 className="panel-title">📍 Explorar y Evaluar Zona</h2>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Criterios</span>
          </div>

          <div className="panel-content">
            {/* Zone Search Input */}
            <div className="search-box-wrapper">
              <label className="input-label">Zona o Dirección (CABA y AMBA)</label>
              <div className="input-with-icon">
                <span className="input-icon">🔍</span>
                <input
                  type="text"
                  className="text-input"
                  value={zoneQuery}
                  onChange={e => setZoneQuery(e.target.value)}
                  onKeyDown={e => {
                    if (e.key === 'Enter') runEvaluation(zoneQuery, workAddress);
                  }}
                  placeholder="Ej. Palermo, CABA / Ramos Mejía / Quilmes / San Isidro..."
                />
              </div>

              {/* Aviso de Cobertura */}
              {coverageNotice && (
                <div className="coverage-warning-banner">
                  <span>{coverageNotice}</span>
                  <button onClick={() => setCoverageNotice(null)} title="Cerrar aviso">✕</button>
                </div>
              )}

              {/* Quick Preset Buttons */}
              <div className="quick-picks">
                {PRESET_ZONES.map(z => (
                  <button
                    key={z.name}
                    className={`pick-chip ${zoneQuery === z.name ? 'active' : ''}`}
                    onClick={() => {
                      setZoneQuery(z.name);
                      setSelectedCoords({ lat: z.lat, lon: z.lon });
                      runEvaluation(z.name, workAddress);
                    }}
                  >
                    {z.name}
                  </button>
                ))}
              </div>
            </div>

            {/* Destination Point for OSRM */}
            <div className="dest-card">
              <label className="input-label">Destino Habitual (Trabajo / Estudio)</label>
              <input
                type="text"
                className="text-input"
                style={{ paddingLeft: '14px' }}
                value={workAddress}
                onChange={e => setWorkAddress(e.target.value)}
                onKeyDown={e => {
                  if (e.key === 'Enter') runEvaluation(zoneQuery, workAddress);
                }}
                placeholder="Ej. Obelisco, Microcentro / UNLaM..."
              />
              <div className="dest-row">
                <button
                  className={`transport-pill ${transportMode === 'driving' ? 'active' : ''}`}
                  onClick={() => {
                    setTransportMode('driving');
                    setTimeout(() => runEvaluation(zoneQuery, workAddress), 50);
                  }}
                >
                  🚗 En Auto
                </button>
                <button
                  className={`transport-pill ${transportMode === 'walking' ? 'active' : ''}`}
                  onClick={() => {
                    setTransportMode('walking');
                    setTimeout(() => runEvaluation(zoneQuery, workAddress), 50);
                  }}
                >
                  🚶 A Pie
                </button>
              </div>
            </div>

            {/* Priority Weights */}
            <div className="search-box-wrapper">
              <label className="input-label">Tus Prioridades y Ponderaciones</label>
              <div className="weight-list">
                {[
                  { key: 'seguridad', label: '👮 Seguridad' },
                  { key: 'salud', label: '🏥 Salud' },
                  { key: 'educacion', label: '🎓 Educación' },
                  { key: 'abastecimiento', label: '🛒 Compras' },
                  { key: 'gastronomia', label: '☕ Gastronomía' },
                  { key: 'espacios_verdes', label: '🌳 Plazas' },
                  { key: 'movilidad', label: '🚌 Movilidad y Tiempos de Viaje' },
                  { key: 'clima', label: '☀️ Clima y Confort' },
                ].map(item => (
                  <div key={item.key} className="weight-row">
                    <span className="weight-meta">{item.label}</span>
                    <div className="weight-selector">
                      {[1, 2, 3].map(val => (
                        <button
                          key={val}
                          className={`weight-btn ${weights[item.key as keyof typeof weights] === val ? 'active' : ''}`}
                          onClick={() => setWeights(prev => ({ ...prev, [item.key]: val }))}
                        >
                          {val === 1 ? 'Baja' : val === 2 ? 'Media' : 'Alta'}
                        </button>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Action Trigger */}
            <button
              className="btn-evaluate"
              disabled={isEvaluating}
              onClick={() => runEvaluation(zoneQuery, workAddress)}
            >
              {isEvaluating ? `Buscando y Evaluando: ${pipelineStep}...` : '⚡ Evaluar Zona con ZonaMatch'}
            </button>
          </div>
        </aside>

        {/* Floating Right Score & Analytics Panel */}
        {evaluation && (
          <aside className="floating-panel sidebar-right">
            <div className="panel-header">
              <h2 className="panel-title">📊 Resultado ZonaMatch Score</h2>
              <span style={{ fontSize: '0.8rem', color: 'var(--accent-cyan)' }}>{evaluation.zoneName}</span>
            </div>

            <div className="panel-content">
              {/* Score Hero */}
              <div className="score-hero">
                <div className="score-number">{evaluation.totalScore}%</div>
                <div className="score-label">Nivel de Coincidencia con tu Perfil</div>

                <div className="confidence-bar-container">
                  <div className="confidence-header">
                    <span>Cobertura de Datos Verificada</span>
                    <span>{evaluation.confidence}%</span>
                  </div>
                  <div className="progress-track">
                    <div className="progress-fill" style={{ width: `${evaluation.confidence}%` }}></div>
                  </div>
                </div>
              </div>

              {/* Subscores Grid */}
              <div className="subscores-grid">
                <div className="subscore-item">
                  <div className="subscore-top">
                    <span>👮 Seguridad</span>
                    <span>{evaluation.metrics.securityPoisCount} Destac.</span>
                  </div>
                  <div className="subscore-val">{evaluation.subscores.seguridad}%</div>
                </div>

                <div className="subscore-item">
                  <div className="subscore-top">
                    <span>🏥 Salud</span>
                    <span>{evaluation.metrics.healthPoisCount} POIs</span>
                  </div>
                  <div className="subscore-val">{evaluation.subscores.salud}%</div>
                </div>

                <div className="subscore-item">
                  <div className="subscore-top">
                    <span>☕ Gastronomía</span>
                    <span>{evaluation.metrics.gastronomyPoisCount} Locales</span>
                  </div>
                  <div className="subscore-val">{evaluation.subscores.gastronomia}%</div>
                </div>

                <div className="subscore-item">
                  <div className="subscore-top">
                    <span>🎓 Educación</span>
                    <span>{evaluation.metrics.educationPoisCount} Colegios</span>
                  </div>
                  <div className="subscore-val">{evaluation.subscores.educacion}%</div>
                </div>

                <div className="subscore-item">
                  <div className="subscore-top">
                    <span>🛒 Compras</span>
                    <span>{evaluation.metrics.shopPoisCount} Super</span>
                  </div>
                  <div className="subscore-val">{evaluation.subscores.abastecimiento}%</div>
                </div>

                <div className="subscore-item">
                  <div className="subscore-top">
                    <span>🌳 Plazas</span>
                    <span>{evaluation.metrics.greenAreasCount} Espacios</span>
                  </div>
                  <div className="subscore-val">{evaluation.subscores.espacios_verdes}%</div>
                </div>

                <div className="subscore-item">
                  <div className="subscore-top">
                    <span>🏧 Finanzas</span>
                    <span>{evaluation.metrics.financePoisCount} Bancos/ATM</span>
                  </div>
                  <div className="subscore-val" style={{ fontSize: '0.95rem' }}>{evaluation.metrics.financePoisCount > 0 ? 'Cubierto' : 'Lejos'}</div>
                </div>

                <div className="subscore-item">
                  <div className="subscore-top">
                    <span>🏋️ Deportes</span>
                    <span>{evaluation.metrics.sportsPoisCount} Fitness</span>
                  </div>
                  <div className="subscore-val" style={{ fontSize: '0.95rem' }}>{evaluation.metrics.sportsPoisCount > 0 ? 'Activo' : 'Bajo'}</div>
                </div>

                <div className="subscore-item">
                  <div className="subscore-top">
                    <span>🚗 Viaje OSRM</span>
                    <span>{evaluation.metrics.travelDurationMin} min</span>
                  </div>
                  <div className="subscore-val">{evaluation.subscores.movilidad}%</div>
                </div>

                <div className="subscore-item">
                  <div className="subscore-top">
                    <span>☀️ Clima</span>
                    <span>{evaluation.metrics.currentTemp}°C</span>
                  </div>
                  <div className="subscore-val">{evaluation.subscores.clima}%</div>
                </div>
              </div>

              {/* Pros and Cons */}
              <div className="diagnostics-block">
                <span className="diag-title" style={{ color: '#34d399' }}>✅ Puntos Fuertes Detectados</span>
                <ul className="diag-list">
                  {evaluation.pros.map((p, i) => (
                    <li key={i} className="diag-item pro">{p}</li>
                  ))}
                </ul>

                {evaluation.cons.length > 0 && (
                  <>
                    <span className="diag-title" style={{ color: '#f43f5e', marginTop: '6px' }}>⚠️ Aspectos a Evaluar</span>
                    <ul className="diag-list">
                      {evaluation.cons.map((c, i) => (
                        <li key={i} className="diag-item con">{c}</li>
                      ))}
                    </ul>
                  </>
                )}
              </div>

              {/* Pipeline Inspector */}
              <div className="pipeline-inspector">
                <div style={{ color: '#fff', fontWeight: 'bold', marginBottom: '4px' }}>📡 Traza del Pipeline MVP</div>
                <div className="inspector-row">
                  <span>Georef Coord:</span>
                  <span className="inspector-val">{evaluation.lat.toFixed(4)}, {evaluation.lon.toFixed(4)}</span>
                </div>
                <div className="inspector-row">
                  <span>Overpass POIs Reales:</span>
                  <span className="inspector-val">{evaluation.pois?.length || 0} lugares indexados</span>
                </div>
                <div className="inspector-row">
                  <span>OSRM Distancia/Tiempo:</span>
                  <span className="inspector-val">{evaluation.metrics.travelDistanceKm} km / {evaluation.metrics.travelDurationMin} min</span>
                </div>
                <div className="inspector-row">
                  <span>Open-Meteo:</span>
                  <span className="inspector-val">{evaluation.metrics.currentTemp}°C (Lluvia: {evaluation.metrics.precipitationMm} mm)</span>
                </div>
              </div>
            </div>
          </aside>
        )}
      </div>
    </div>
  );
}

export default App;
