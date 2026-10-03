namespace ZonaMatch.Application.Common
{
    public class NotFoundException : Exception
    {
        public NotFoundException(string resource, string key)
            : base($"No se encontró {resource} '{key}'.")
        {
            Resource = resource;
            Key = key;
        }

        public string Resource { get; }
        public string Key { get; }
    }
}
