using System.Text;

namespace ZonaMatch.Infrastructure.Data
{
    // convención de nombres de PostgreSQL: snake_case en minúsculas
    public static class StoreNaming
    {
        public static string ToSnakeCase(string name)
        {
            var builder = new StringBuilder(name.Length + 8);

            for (var index = 0; index < name.Length; index++)
            {
                var current = name[index];

                if (char.IsUpper(current))
                {
                    var previousIsLowerOrDigit = index > 0 && (char.IsLower(name[index - 1]) || char.IsDigit(name[index - 1]));
                    var nextIsLower = index + 1 < name.Length && char.IsLower(name[index + 1]);
                    var previousIsUpper = index > 0 && char.IsUpper(name[index - 1]);

                    // "ZoneId" -> zone_id, "PKZone" -> pk_zone
                    if (previousIsLowerOrDigit || (previousIsUpper && nextIsLower))
                    {
                        builder.Append('_');
                    }

                    builder.Append(char.ToLowerInvariant(current));
                }
                else
                {
                    builder.Append(current);
                }
            }

            return builder.ToString();
        }
    }
}
