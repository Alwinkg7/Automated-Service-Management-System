"use client";
// Leaflet map (loaded with next/dynamic, ssr:false – Leaflet needs `window`).
// Circle markers avoid Leaflet's default image icons, which break with bundlers.
import { useEffect } from "react";
import { CircleMarker, MapContainer, Polyline, TileLayer, Tooltip, useMap } from "react-leaflet";

export interface LatLng {
  lat: number;
  lng: number;
}

// Default view (India) until the first location arrives.
const DEFAULT_CENTER: [number, number] = [20.5937, 78.9629];

function FollowPosition({ position }: { position: LatLng | null }) {
  const map = useMap();
  useEffect(() => {
    if (position) map.flyTo([position.lat, position.lng], Math.max(map.getZoom(), 15), { duration: 0.6 });
  }, [map, position]);
  return null;
}

export default function TrackingMap({ position, trail }: { position: LatLng | null; trail: LatLng[] }) {
  return (
    <MapContainer center={position ? [position.lat, position.lng] : DEFAULT_CENTER} zoom={position ? 15 : 5} scrollWheelZoom className="h-full w-full">
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />
      {trail.length > 1 ? <Polyline positions={trail.map((p) => [p.lat, p.lng])} pathOptions={{ color: "#2453d6", weight: 3, opacity: 0.6 }} /> : null}
      {position ? (
        <CircleMarker center={[position.lat, position.lng]} radius={10} pathOptions={{ color: "#ffffff", weight: 3, fillColor: "#2453d6", fillOpacity: 1 }}>
          <Tooltip permanent direction="top" offset={[0, -10]}>
            Technician
          </Tooltip>
        </CircleMarker>
      ) : null}
      <FollowPosition position={position} />
    </MapContainer>
  );
}
