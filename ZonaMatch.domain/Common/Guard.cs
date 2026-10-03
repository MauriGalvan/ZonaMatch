namespace ZonaMatch.Domain.Common
{
    // validaciones de invariantes compartidas por las entidades
    public static class Guard
    {
        public static string NotBlank(string? value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("El valor no puede estar vacío.", paramName);
            }

            return value.Trim();
        }

        public static T NotNull<T>(T? value, string paramName) where T : class
        {
            return value ?? throw new ArgumentNullException(paramName);
        }

        public static TNumber Positive<TNumber>(TNumber value, string paramName)
            where TNumber : struct, IComparable<TNumber>
        {
            if (value.CompareTo(default) <= 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "El valor debe ser mayor a cero.");
            }

            return value;
        }

        public static TNumber NotNegative<TNumber>(TNumber value, string paramName)
            where TNumber : struct, IComparable<TNumber>
        {
            if (value.CompareTo(default) < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "El valor no puede ser negativo.");
            }

            return value;
        }
    }
}
