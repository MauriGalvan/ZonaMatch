namespace ZonaMatch.Application.UseCases.Puntajes
{
    // Cuantos lugares se pueden medir: el visitante prueba con pocos; con cuenta, los de todo el hogar
    public static class LimitesLugares
    {
        public const int Visitante = 3;
        public const int ConCuenta = 15;

        public const string MensajeVisitante = "Como visitante podés medir hasta 3 lugares. Creá tu cuenta para sumar hasta 15.";
        public const string MensajeConCuenta = "Se pueden medir hasta 15 lugares.";
    }
}
