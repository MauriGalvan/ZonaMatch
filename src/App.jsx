import { useEffect, useMemo, useRef, useState } from 'react'
import { MapContainer, Marker, TileLayer, useMap } from 'react-leaflet'
import L from 'leaflet'
import './App.css'
import partidosRaw from '../data/partidos-pba.json'
import comunasRaw from '../data/departamentos-ciudad_autonoma_de_buenos_aires.json'
import { partidoCensoDepto } from './data/partidoCenso2022'
import { cabaComunaCenso } from './data/cabaComunaCenso2022'

const normalize = (name) =>
  (name || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toUpperCase().trim()

const partidoFeatures = partidosRaw.features.map((f) => {
  const key = normalize(f.properties.municipio_nombre)
  return {
    ...f,
    properties: {
      id: f.properties.municipio_id,
      key,
      departamento: f.properties.municipio_nombre,
      provincia: 'BUENOS AIRES',
      provOrigen: 'pba',
      censoCodigo: partidoCensoDepto[key] || null,
    },
  }
})

const comunaFeatures = comunasRaw.features.map((f) => {
  const key = normalize(f.properties.departamento)
  return {
    ...f,
    properties: {
      id: f.properties.id,
      key,
      departamento: f.properties.departamento,
      provincia: 'CIUDAD AUTONOMA DE BUENOS AIRES',
      provOrigen: 'caba',
      censoCodigo: cabaComunaCenso[key] || null,
    },
  }
})

const geojsonData = {
  type: 'FeatureCollection',
  features: [...partidoFeatures, ...comunaFeatures],
}

const position = [-36.5, -60.5]

const baseStyle = {
  color: '#64748B',
  weight: 1,
  fillColor: '#F1F5F9',
  fillOpacity: 0.5,
}

const hoverStyle = {
  color: '#0EA5A8',
  weight: 2,
  fillColor: '#CCFBF1',
  fillOpacity: 0.7,
}

const selectedStyle = {
  color: '#16A34A',
  weight: 2.5,
  fillColor: '#DCFCE7',
  fillOpacity: 0.7,
}

const radiosStyle = {
  color: '#64748B',
  weight: 1,
  fillColor: '#F1F5F9',
  fillOpacity: 0.2,
}

const radioHoverStyle = {
  color: '#0EA5A8',
  weight: 2,
  fillColor: '#CCFBF1',
  fillOpacity: 0.7,
}

const radioSelectedStyle = {
  color: '#2563EB',
  weight: 2,
  fillColor: '#DBEAFE',
  fillOpacity: 0.7,
}

const radiosFuentes = {
  pba: {
    url: 'radios-censales-pba-2022.json',
    match: (props, codigo) => String(props.DEPTO) === codigo,
    fraccion: (props) => props.FRAC,
    radio: (props) => props.RADIO,
    codigo: (props) => props.LINK,
  },
  caba: {
    url: 'radios-censales-caba-2022.json',
    match: (props, codigo) => props.ncom === codigo,
    fraccion: (props) => props.nfracc,
    radio: (props) => props.nrad,
    codigo: (props) => props.cro,
  },
}

const radiosCache = new Map()
function loadRadios(url) {
  if (!radiosCache.has(url)) {
    radiosCache.set(
      url,
      fetch(url)
        .then((res) => res.json())
        .catch((err) => {
          radiosCache.delete(url)
          throw err
        })
    )
  }
  return radiosCache.get(url)
}

const puntoIcon = L.divIcon({
  className: 'punto-marker',
  html: '<div class="punto-dot"></div>',
  iconSize: [18, 18],
  iconAnchor: [9, 9],
})

const reverseCache = new Map()
async function reverseGeocode(lat, lng) {
  const key = `${lat.toFixed(5)}_${lng.toFixed(5)}`
  if (reverseCache.has(key)) return reverseCache.get(key)
  const p = (async () => {
    const url =
      'https://nominatim.openstreetmap.org/reverse?format=jsonv2' +
      `&lat=${lat.toFixed(6)}&lon=${lng.toFixed(6)}&accept-language=es&zoom=18&addressdetails=1`
    const res = await fetch(url)
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    return res.json()
  })().catch((err) => {
    reverseCache.delete(key)
    throw err
  })
  reverseCache.set(key, p)
  return p
}

function formatDireccion(data) {
  const a = data?.address
  if (!a) return data?.display_name || ''
  const calle = [a.road || a.pedestrian || a.footway, a.house_number]
    .filter(Boolean)
    .join(' ')
  const barrio = a.suburb || a.neighbourhood || a.city_district || a.borough
  return [calle, barrio, a.city || a.town || a.municipality || a.village]
    .filter(Boolean)
    .join(', ')
}

function PuntoDireccion({ lat, lng }) {
  const [estado, setEstado] = useState('cargando')
  const [texto, setTexto] = useState('')

  useEffect(() => {
    let cancel = false
    reverseGeocode(lat, lng)
      .then((data) => {
        if (cancel) return
        setTexto(formatDireccion(data))
        setEstado('ok')
      })
      .catch(() => {
        if (!cancel) setEstado('error')
      })
    return () => {
      cancel = true
    }
  }, [lat, lng])

  if (estado === 'ok') return <p>Dirección: {texto}</p>
  if (estado === 'error') {
    return (
      <p className="analisis-status">
        Latitud: {lat.toFixed(5)} · Longitud: {lng.toFixed(5)}
      </p>
    )
  }
  return <p className="analisis-status">Buscando dirección…</p>
}

function DepartmentsLayer({ selectedId, onSelect }) {
  const map = useMap()
  const layersRef = useRef(new Map())
  const selectedIdRef = useRef(selectedId)

  useEffect(() => {
    selectedIdRef.current = selectedId
  }, [selectedId])

  useEffect(() => {
    const layerMap = layersRef.current
    const geoLayer = L.geoJSON(geojsonData, {
      style: baseStyle,
      onEachFeature: (feature, layer) => {
        const id = feature.properties.key
        layerMap.set(id, layer)

        layer.on({
          mouseover: (e) => {
            e.target.setStyle(hoverStyle)
            e.target.bringToFront()
          },
          mouseout: (e) => {
            e.target.setStyle(selectedIdRef.current === id ? selectedStyle : baseStyle)
          },
          click: () => {
            onSelect(id)
          },
        })
      },
    })

    geoLayer.addTo(map)
    geoLayer.bringToFront()

    return () => {
      layerMap.clear()
      geoLayer.remove()
    }
  }, [map, onSelect])

  useEffect(() => {
    layersRef.current.forEach((layer, id) => {
      if (id === selectedId) {
        layer.setStyle(selectedStyle)
        layer.bringToFront()
      } else {
        layer.setStyle(baseStyle)
      }
    })
  }, [selectedId])

  return null
}

function RadiosLayer({ fuente, codigo, onCountChange, selectedRadioId, onSelectRadio }) {
  const map = useMap()
  const layerRef = useRef(null)
  const layersRef = useRef(new Map())
  const onCountChangeRef = useRef(onCountChange)
  const onSelectRadioRef = useRef(onSelectRadio)
  const selectedRadioIdRef = useRef(selectedRadioId)

  useEffect(() => {
    onCountChangeRef.current = onCountChange
  }, [onCountChange])

  useEffect(() => {
    onSelectRadioRef.current = onSelectRadio
  }, [onSelectRadio])

  useEffect(() => {
    selectedRadioIdRef.current = selectedRadioId
  }, [selectedRadioId])

  useEffect(() => {
    const previous = layerRef.current
    if (previous) {
      map.removeLayer(previous)
      layerRef.current = null
    }
    layersRef.current.clear()

    onCountChangeRef.current?.(null)
    if (!fuente || !codigo) return undefined

    let cancelled = false
    loadRadios(fuente.url)
      .then((data) => {
        if (cancelled) return
        const features = data.features.filter((f) => fuente.match(f.properties, codigo))
        const geo = L.geoJSON(features, {
          style: radiosStyle,
          onEachFeature: (feature, layer) => {
            const id = fuente.codigo(feature.properties)
            layersRef.current.set(id, layer)
            layer.on({
              mouseover: () => {
                if (id !== selectedRadioIdRef.current) {
                  layer.setStyle(radioHoverStyle)
                  layer.bringToFront()
                }
              },
              mouseout: () => {
                layer.setStyle(
                  id === selectedRadioIdRef.current ? radioSelectedStyle : radiosStyle
                )
              },
              click: (e) => {
                onSelectRadioRef.current?.(feature.properties, e.latlng)
              },
            })
          },
        })
        if (cancelled) return
        layerRef.current = geo
        geo.addTo(map)
        geo.bringToFront()
        if (features.length) map.fitBounds(geo.getBounds())
        onCountChangeRef.current?.(features.length)
      })
      .catch(() => {
        onCountChangeRef.current?.(null)
      })

    return () => {
      cancelled = true
      const geo = layerRef.current
      if (geo) {
        map.removeLayer(geo)
        layerRef.current = null
      }
    }
  }, [map, fuente, codigo])

  useEffect(() => {
    layersRef.current.forEach((layer, id) => {
      if (id === selectedRadioId) {
        layer.setStyle(radioSelectedStyle)
        layer.bringToFront()
      } else {
        layer.setStyle(radiosStyle)
      }
    })
  }, [selectedRadioId])

  return null
}

export default function App() {
  const [selectedId, setSelectedId] = useState(null)
  const [radioCount, setRadioCount] = useState(null)
  const [selectedRadio, setSelectedRadio] = useState(null)
  const [punto, setPunto] = useState(null)

  const selectedDepartment = useMemo(
    () => geojsonData.features.find((f) => f.properties.key === selectedId),
    [selectedId]
  )

  const radiosFuente = selectedDepartment
    ? radiosFuentes[selectedDepartment.properties.provOrigen]
    : null
  const radiosCodigo = selectedDepartment
    ? selectedDepartment.properties.censoCodigo
    : null

  const handleSelect = (id) => {
    setSelectedId((prev) => (prev === id ? null : id))
    setSelectedRadio(null)
    setPunto(null)
  }

  const handleSelectRadio = (props, latlng) => {
    setSelectedRadio((prev) => {
      if (!prev) return props
      return radiosFuente.codigo(prev) === radiosFuente.codigo(props) ? null : props
    })
    if (latlng) setPunto({ lat: latlng.lat, lng: latlng.lng })
  }

  return (
    <div className="app">
      <aside className="sidebar">
        <div className="sidebar-header">
          <h1>ZonaMatch</h1>
        </div>

        <div className="search-box">
          <input
            type="text"
            placeholder="Buscar un lugar..."
            disabled
          />
          <button disabled>Buscar</button>
        </div>

        {selectedDepartment && (
          <div className="zone-info">
            <h3>Zona seleccionada</h3>
            <p>Departamento: {selectedDepartment.properties.departamento}</p>
            <p>Provincia: {selectedDepartment.properties.provincia}</p>
            {selectedDepartment.properties.censoCodigo && (
              <p>
                {selectedDepartment.properties.provOrigen === 'caba'
                  ? 'Comuna censal'
                  : 'Código censal'}: {selectedDepartment.properties.censoCodigo}
              </p>
            )}
            {radioCount !== null && <p>Radios: {radioCount}</p>}
          </div>
        )}

        <div className="zone-info analisis">
          <h3>Punto en el mapa</h3>
          {!punto && (
            <p className="analisis-status">
              Seleccioná un partido y hacé clic en un radio para marcar un punto.
            </p>
          )}
          {punto && (
            <>
              <PuntoDireccion
                key={`${punto.lat.toFixed(6)}_${punto.lng.toFixed(6)}`}
                lat={punto.lat}
                lng={punto.lng}
              />
              <button className="quitar" onClick={() => setPunto(null)}>
                Quitar punto
              </button>
            </>
          )}
        </div>
      </aside>

      <main className="map-area">
<MapContainer
            center={position}
            zoom={7}
            className="map"
            scrollWheelZoom
          >
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
              url="https://tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            {punto && <Marker position={[punto.lat, punto.lng]} icon={puntoIcon} />}
          <DepartmentsLayer
            selectedId={selectedId}
            onSelect={handleSelect}
          />
          <RadiosLayer
            fuente={radiosFuente}
            codigo={radiosCodigo}
            onCountChange={setRadioCount}
            selectedRadioId={selectedRadio && radiosFuente.codigo(selectedRadio)}
            onSelectRadio={handleSelectRadio}
          />
        </MapContainer>
      </main>
    </div>
  )
}