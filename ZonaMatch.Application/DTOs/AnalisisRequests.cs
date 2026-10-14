using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs
{
    // POST /Analisis — guarda un analisis con las zonas elegidas; el criterio sale del perfil del usuario
    public class CrearAnalisisRequest
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 50 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Las zonas son obligatorias.")]
        [MinLength(1, ErrorMessage = "Elegí al menos una zona.")]
        public List<ZonaAnalisisRequest> Zonas { get; set; } = [];
    }

    // POST /Analisis/{id}/versiones — el mismo analisis con el criterio ajustado solo para este caso
    public class CrearVersionAnalisisRequest
    {
        // Sin nombre, la version se llama como el original con un numero ("Cerca del trabajo (2)")
        [StringLength(50, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 50 caracteres.")]
        public string? Nombre { get; set; }

        [Required(ErrorMessage = "El criterio es obligatorio.")]
        public CriterioUsuarioRequest Criterio { get; set; } = null!;

        // Sin zonas, la version compara las mismas que el original
        public List<ZonaAnalisisRequest>? Zonas { get; set; }
    }

    // PUT /Analisis/{id} — solo cambia el nombre: el criterio de un analisis guardado no se edita, se versiona
    public class RenombrarAnalisisRequest
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 50 caracteres.")]
        public string Nombre { get; set; } = string.Empty;
    }

    public class ZonaAnalisisRequest
    {
        [Required(ErrorMessage = "El slug de la zona es obligatorio.")]
        [StringLength(100)]
        public string Slug { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre de la zona es obligatorio.")]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        public bool EsInicial { get; set; }
    }
}
