namespace ZonaMatch.Domain.Puntaje
{
    public enum ModoViaje
    {
        TransportePublico,
        Auto,
        Bici,
        APie,
    }

    public static class ModosViaje
    {
        // Como viaja el usuario segun la movilidad de su hogar (a-pie, transporte-publico, vehiculo, bici).
        // Sin dato, o si usa transporte publico, se mide en transporte publico: es lo que usa la mayoria.
        public static ModoViaje DesdeMovilidad(IReadOnlyCollection<string> movilidad)
        {
            if (movilidad.Count == 0 || movilidad.Contains("transporte-publico"))
                return ModoViaje.TransportePublico;

            if (movilidad.Contains("vehiculo"))
                return ModoViaje.Auto;

            if (movilidad.Contains("bici"))
                return ModoViaje.Bici;

            return ModoViaje.APie;
        }

        // In public transport, five blocks of 100 m or less are walked: nobody takes a bus for three blocks. A household
        // that moves by car or bike keeps its mode. Straight-line distance, as in Estimar.
        public const double DistanciaACaminarKm = 0.5;

        public static ModoViaje ParaDistancia(
            ModoViaje modo, double latitudOrigen, double longitudOrigen, double latitudDestino, double longitudDestino) =>
            modo == ModoViaje.TransportePublico
            && DistanciaKm(latitudOrigen, longitudOrigen, latitudDestino, longitudDestino) <= DistanciaACaminarKm
                ? ModoViaje.APie
                : modo;

        // Inverse of Codigo; no code means public transport, as in DesdeMovilidad. Null for an unknown code.
        public static ModoViaje? DesdeCodigo(string? codigo) => codigo?.Trim().ToLowerInvariant() switch
        {
            null or "" or "transporte-publico" => ModoViaje.TransportePublico,
            "auto" => ModoViaje.Auto,
            "bici" => ModoViaje.Bici,
            "a-pie" => ModoViaje.APie,
            _ => null,
        };

        public static string Codigo(ModoViaje modo) => modo switch
        {
            ModoViaje.Auto => "auto",
            ModoViaje.Bici => "bici",
            ModoViaje.APie => "a-pie",
            _ => "transporte-publico",
        };

        // Estimacion gruesa por distancia en linea recta, para cuando no responde el ruteo.
        // Velocidades medias del AMBA con una espera fija para el transporte y para estacionar.
        public static TiempoViaje Estimar(ModoViaje modo, double latitudOrigen, double longitudOrigen, double latitudDestino, double longitudDestino)
        {
            var km = DistanciaKm(latitudOrigen, longitudOrigen, latitudDestino, longitudDestino);

            var minutos = modo switch
            {
                ModoViaje.Auto => 5 + km * 2.5,
                ModoViaje.Bici => km * 4,
                ModoViaje.APie => km * 12,
                _ => 10 + km * 4,
            };

            return new TiempoViaje((int)Math.Round(minutos), Estimado: true);
        }

        // Haversine, en kilometros
        public static double DistanciaKm(double latitud1, double longitud1, double latitud2, double longitud2)
        {
            const double RadioTierraKm = 6371;
            var dLat = Radianes(latitud2 - latitud1);
            var dLon = Radianes(longitud2 - longitud1);
            var a = Math.Pow(Math.Sin(dLat / 2), 2)
                    + Math.Cos(Radianes(latitud1)) * Math.Cos(Radianes(latitud2)) * Math.Pow(Math.Sin(dLon / 2), 2);

            return 2 * RadioTierraKm * Math.Asin(Math.Sqrt(a));
        }

        private static double Radianes(double grados) => grados * Math.PI / 180;
    }
}
