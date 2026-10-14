namespace ZonaMatch.Domain.Common
{
    // Limites de texto de la comunidad de una zona, compartidos por las columnas de la base y la validacion de los pedidos
    public static class Longitudes
    {
        public const int TextoResenaMinimo = 20;
        public const int TextoResena = 1000;

        public const int TextoPreguntaMinimo = 10;
        public const int TextoPregunta = 300;

        public const int TextoRespuestaMinimo = 2;
        public const int TextoRespuesta = 1000;

        public const int NombreLugar = 120;
        public const int Horario = 120;
        public const int Direccion = 200;
        public const int Comentario = 500;
    }
}
