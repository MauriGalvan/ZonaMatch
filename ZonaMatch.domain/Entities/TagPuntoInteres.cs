using System.Globalization;
using System.Text;

namespace ZonaMatch.Domain.Entities
{
    // Etiqueta de un punto de interes personal ("Trabajo", "Peluqueria"...), tabla app.tags_punto_interes.
    // Es un catalogo compartido: "Peluquería" y " peluqueria " son el mismo tag, asi que comparten fila.
    public class TagPuntoInteres
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        // Como lo escribio quien lo creo primero, con los espacios limpios
        public string Nombre { get; set; } = string.Empty;

        // Sin mayusculas, tildes ni espacios repetidos; el indice unico se apoya en este valor
        public string NombreNormalizado { get; set; } = string.Empty;

        // Recorta y colapsa los espacios internos ("  Casa   de amigo " -> "Casa de amigo")
        public static string LimpiarNombre(string? nombre) =>
            string.Join(' ', (nombre ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        public static string Normalizar(string nombre)
        {
            var descompuesto = LimpiarNombre(nombre).ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sinTildes = descompuesto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
            return new string(sinTildes.ToArray()).Normalize(NormalizationForm.FormC);
        }
    }
}
