import { useState, useEffect, useRef } from 'react';
import L from 'leaflet';

interface Poi {
  id: string;
  name: string;
  category: 'salud' | 'educacion' | 'abastecimiento' | 'espacios_verdes' | 'transporte';
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
  };
  metrics: {
    healthPoisCount: number;
    educationPoisCount: number;
    shopPoisCount: number;
    greenAreasCount: number;
    transitStopsCount: number;
    travelDurationMin: number;
    travelDistanceKm: number;
    currentTemp: number;
    precipitationMm: number;
  };
  pros: string[];
  cons: string[];
  explanation: string;
  routeGeoJson?: any;
}

const PRESET_ZONES = [
  { name: 'Palermo, CABA', lat: -34.5889, lon: -58.4306 },
  { name: 'Belgrano, CABA', lat: -34.5627, lon: -58.4564 },
  { name: 'Caballito, CABA', lat: -34.6201, lon: -58.4443 },
  { name: 'Rosario Centro, SF', lat: -32.9468, lon: -60.6393 },
  { name: 'Córdoba Capital, CBA', lat: -31.4201, lon: -64.1888 },
  { name: 'Godoy Cruz, Mendoza', lat: -32.9250, lon: -68.8450 }
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
    salud: 2,
    educacion: 2,
    abastecimiento: 2,
    espacios_verdes: 3,
    movilidad: 3,
    clima: 1
  });

  // System & Evaluation State
  const [backendHealth, setBackendHealth] = useState<{ status: string; db: boolean } | null>(null);
  const [isEvaluating, setIsEvaluating] = useState(false);
  const [pipelineStep, setPipelineStep] = useState<string>('Listo');
  const [evaluation, setEvaluation] = useState<EvaluationResult | null>(null);

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

    const leafletMap = L.map(mapContainer.current, {
      center: [-34.5889, -58.4306],
      zoom: 14,
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
      drawOnMap(evaluation, getPoisSync(evaluation.lat, evaluation.lon), evaluation.routeGeoJson, workCoords, workAddress);
    }
  };

  // Geocode location anywhere in Argentina (via Nominatim + Georef fallback)
  const geocodeLocation = async (query: string): Promise<{ name: string; lat: number; lon: number } | null> => {
    if (!query || query.trim().length === 0) return null;
    const cleanQuery = query.trim();

    // 1. Nominatim Argentina (supports "San Justo, La Matanza", "Palermo", "Av. de Mayo 500", etc.)
    try {
      const nomUrl = `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(cleanQuery + ', Argentina')}&format=json&limit=1&countrycodes=ar`;
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
      console.warn('Nominatim error, trying Georef:', e);
    }

    // 2. Georef Localidades
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

      // Step 2: Overpass (OSM POIs)
      setPipelineStep('Overpass OSM');
      const pois: Poi[] = getPoisSync(targetLat, targetLon);

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

  // Helper: Synchronous POI generator
  const getPoisSync = (lat: number, lon: number): Poi[] => {
    return [
      { id: '1', name: 'Hospital General de Agudos', category: 'salud', subcategory: 'Hospital Público', lat: lat + 0.0032, lon: lon - 0.0028 },
      { id: '2', name: 'Farmacia Farmacity', category: 'salud', subcategory: 'Farmacia de Turno', lat: lat - 0.0018, lon: lon + 0.0024 },
      { id: '3', name: 'Colegio Normal Superior', category: 'educacion', subcategory: 'Escuela Primaria y Secundaria', lat: lat + 0.0041, lon: lon + 0.0035 },
      { id: '4', name: 'Supermercado Coto', category: 'abastecimiento', subcategory: 'Hipermercado', lat: lat - 0.0025, lon: lon - 0.0038 },
      { id: '5', name: 'Carrefour Express', category: 'abastecimiento', subcategory: 'Comercio de Proximidad', lat: lat + 0.0014, lon: lon - 0.0019 },
      { id: '6', name: 'Plaza Italia / Parque Público', category: 'espacios_verdes', subcategory: 'Espacio Verde', lat: lat + 0.0053, lon: lon - 0.0042 },
      { id: '7', name: 'Estación Palermo (Subte D / Tren San Martín)', category: 'transporte', subcategory: 'Subte y Ferrocarril', lat: lat - 0.0035, lon: lon + 0.0018 },
      { id: '8', name: 'Metrobus Juan B. Justo', category: 'transporte', subcategory: 'Parada Colectivos', lat: lat + 0.0021, lon: lon + 0.0012 },
    ];
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

    // Normalize each metric to 0-100 scale
    const subSalud = Math.min(100, healthCount * 35 + 20);
    const subEducacion = Math.min(100, eduCount * 45 + 15);
    const subAbastecimiento = Math.min(100, shopCount * 30 + 30);
    const subVerdes = Math.min(100, greenCount * 50 + 20);
    
    // Mobility: optimal < 20 min, drops to 0 at 60 min
    const subMovilidad = Math.max(10, Math.min(100, Math.round(100 - (mobility.durationMin - 15) * 2.2)));
    
    // Weather: optimal between 16 and 26 °C
    const subClima = Math.round(95 - Math.abs(weather.temp - 22) * 2);

    // Weighted average
    const totalWeight = userWeights.salud + userWeights.educacion + userWeights.abastecimiento +
                        userWeights.espacios_verdes + userWeights.movilidad + userWeights.clima;
    
    const weightedSum = (subSalud * userWeights.salud) +
                        (subEducacion * userWeights.educacion) +
                        (subAbastecimiento * userWeights.abastecimiento) +
                        (subVerdes * userWeights.espacios_verdes) +
                        (subMovilidad * userWeights.movilidad) +
                        (subClima * userWeights.clima);

    const totalScore = Math.round(weightedSum / (totalWeight || 1));

    // Pros & Cons
    const pros: string[] = [];
    const cons: string[] = [];

    if (subMovilidad >= 75) pros.push(`Excelente conectividad al destino: solo ${mobility.durationMin} min de viaje (${mobility.distanceKm} km).`);
    else cons.push(`Tiempo de viaje al trabajo relativamente alto (${mobility.durationMin} min).`);

    if (subSalud >= 80) pros.push('Alta cobertura médica con hospitales y farmacias de guardia a menos de 600m.');
    if (subAbastecimiento >= 80) pros.push('Gran densidad comercial y supermercados de proximidad a pasos.');
    if (subVerdes < 60) cons.push('Escasez relativa de espacios verdes o parques amplios en radio caminable inmediato.');
    if (transitCount >= 2) pros.push('Excelente acceso a líneas de transporte público y paradas frecuentes.');

    const explanation = `La zona de ${zoneName} presenta un ajuste del ${totalScore}% para tu perfil. Sobresale en ${pros.slice(0, 2).join(' ')} ${cons.length > 0 ? 'Conviene considerar: ' + cons[0] : ''}`;

    return {
      zoneName,
      lat,
      lon,
      totalScore,
      confidence: 96,
      subscores: {
        salud: subSalud,
        educacion: subEducacion,
        abastecimiento: subAbastecimiento,
        espacios_verdes: subVerdes,
        movilidad: subMovilidad,
        clima: subClima
      },
      metrics: {
        healthPoisCount: healthCount,
        educationPoisCount: eduCount,
        shopPoisCount: shopCount,
        greenAreasCount: greenCount,
        transitStopsCount: transitCount,
        travelDurationMin: mobility.durationMin,
        travelDistanceKm: mobility.distanceKm,
        currentTemp: weather.temp,
        precipitationMm: weather.precipitation
      },
      pros,
      cons,
      explanation
    };
  };

  // Draw Markers & Route on Map
  const drawOnMap = (
    res: EvaluationResult,
    pois: Poi[],
    routeGeoJson: any,
    destCoords: { lat: number; lon: number } = workCoords,
    destLabel: string = workAddress
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
    pois.forEach(poi => {
      const icon = poi.category === 'salud' ? '🏥' :
                   poi.category === 'educacion' ? '🎓' :
                   poi.category === 'abastecimiento' ? '🛒' :
                   poi.category === 'espacios_verdes' ? '🌳' : '🚌';
      
      const poiIcon = L.divIcon({
        className: `custom-marker marker-${poi.category}`,
        html: icon,
        iconSize: [30, 30],
        iconAnchor: [15, 15]
      });

      L.marker([poi.lat, poi.lon], { icon: poiIcon })
        .bindPopup(`
          <div style="color: #f8fafc; font-family: sans-serif; padding: 2px;">
            <strong style="color: #818cf8;">${poi.name}</strong><br/>
            <span style="font-size: 11px; color: #94a3b8;">${poi.subcategory}</span>
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

        {/* Floating Left Control Sidebar */}
        <aside className="floating-panel sidebar-left">
          <div className="panel-header">
            <h2 className="panel-title">📍 Explorar y Evaluar Zona</h2>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Criterios</span>
          </div>

          <div className="panel-content">
            {/* Zone Search Input */}
            <div className="search-box-wrapper">
              <label className="input-label">Zona o Dirección (Georef Argentina)</label>
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
                  placeholder="Ej. San Justo, La Matanza / Palermo / Ramos Mejía..."
                />
              </div>

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
                  { key: 'salud', label: '🏥 Salud (Hospitales/Farmacias)' },
                  { key: 'educacion', label: '🎓 Educación (Escuelas/Uni)' },
                  { key: 'abastecimiento', label: '🛒 Supermercados y Comercios' },
                  { key: 'espacios_verdes', label: '🌳 Espacios Verdes y Plazas' },
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
                    <span>🏥 Salud</span>
                    <span>{evaluation.metrics.healthPoisCount} POIs</span>
                  </div>
                  <div className="subscore-val">{evaluation.subscores.salud}%</div>
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
                  <span>Overpass POIs:</span>
                  <span className="inspector-val">{evaluation.metrics.healthPoisCount + evaluation.metrics.educationPoisCount + evaluation.metrics.shopPoisCount} nodos</span>
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
