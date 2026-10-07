namespace ZonaMatch.Application.Exceptions
{
    // Input rules that data annotations cannot express, e.g. fields required only for one kind of correction (400)
    public class DatosInvalidosException : Exception
    {
        public DatosInvalidosException(string message)
            : base(message)
        {
        }
    }
}
