namespace ZonaMatch.Application.Exceptions
{
    // The user already has the maximum number of saved analyses (3).
    public class LimiteAnalisisException : Exception
    {
        public LimiteAnalisisException()
            : base("Ya tenés 3 análisis guardados. Sobrescribí uno existente o eliminalo para guardar uno nuevo.")
        {
        }
    }
}
