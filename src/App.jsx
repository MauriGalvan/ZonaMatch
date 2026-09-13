import { useEffect, useMemo, useRef, useState } from 'react'
import { MapContainer, TileLayer, useMap } from 'react-leaflet'
import L from 'leaflet'
import './App.css'
import geojsonBsAs from '../departamentos-buenos_aires.json'
import geojsonCaba from '../departamentos-ciudad_autonoma_de_buenos_aires.json'

const geojsonData = {
  type: 'FeatureCollection',
  features: [...geojsonBsAs.features, ...geojsonCaba.features],
}

const position = [-34.6037, -58.3816]

const baseStyle = {
  color: '#4a5568',
  weight: 1,
  fillColor: '#1a73e8',
  fillOpacity: 0.08,
}

const hoverStyle = {
  color: '#2b6cb0',
  weight: 3,
  fillColor: '#4299e1',
  fillOpacity: 0.25,
}

const selectedStyle = {
  color: '#1a7f37',
  weight: 3,
  fillColor: '#2da44e',
  fillOpacity: 0.6,
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
        const id = feature.properties.id
        layerMap.set(id, layer)

        layer.on({
          mouseover: (e) => {
            e.target.setStyle(hoverStyle)
            e.target.bringToFront()
          },
          mouseout: (e) => {
            e.target.setStyle(selectedIdRef.current === id ? selectedStyle : baseStyle)
          },
          click: (e) => {
            L.DomEvent.stopPropagation(e)
            onSelect(id)
          },
        })
      },
    })

    geoLayer.addTo(map)

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

export default function App() {
  const [selectedId, setSelectedId] = useState(null)

  const selectedFeature = useMemo(
    () => geojsonData.features.find((f) => f.properties.id === selectedId),
    [selectedId]
  )

  const handleSelect = (id) => {
    setSelectedId((prev) => (prev === id ? null : id))
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

      

        {selectedFeature && (
          <div className="zone-info">
            <h3>Zona seleccionada</h3>
            <p>Departamento: {selectedFeature.properties.departamento}</p>
            <p>Provincia: {selectedFeature.properties.provincia}</p>
            <p>ID: {selectedFeature.properties.id}</p>
          </div>
        )}
      </aside>

      <main className="map-area">
        <MapContainer
          center={position}
          zoom={13}
          className="map"
          scrollWheelZoom
        >
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            url="https://tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <DepartmentsLayer
            selectedId={selectedId}
            onSelect={handleSelect}
          />
        </MapContainer>
      </main>
    </div>
  )
}