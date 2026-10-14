using System.ComponentModel.DataAnnotations;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.DTOs
{
    // GET /Zonas/{slug}/resenas: resumen y resenas, la mas nueva primero
    public record ResenasZonaDto(ResumenResenasDto Resumen, IReadOnlyList<ResenaDto> Resenas);

    // Promedio es 0 cuando no hay resenas. Aspectos trae cada AspectoZona en orden.
    public record ResumenResenasDto(double Promedio, int Total, int Verificadas, IReadOnlyList<PromedioAspectoDto> Aspectos);

    public record PromedioAspectoDto(string Aspecto, double Promedio);

    // Iniciales identifica al autor sin exponer el email.
    // Puntaje es el promedio de los aspectos (siempre con un decimal como mucho).
    // MarcadaUtilPorMi y EsMia son false en pedidos anonimos.
    public record ResenaDto(
        Guid Id,
        string Iniciales,
        int? AniosEnZona,
        bool Verificada,
        double Puntaje,
        PuntajesAspectosDto Aspectos,
        IReadOnlyList<string> Temas,
        string Texto,
        DateTimeOffset Fecha,
        int Utiles,
        bool MarcadaUtilPorMi,
        bool EsMia);

    public record PuntajesAspectosDto(
        [Range(1, 5, ErrorMessage = "El puntaje de seguridad debe estar entre 1 y 5.")] int Seguridad,
        [Range(1, 5, ErrorMessage = "El puntaje de transporte debe estar entre 1 y 5.")] int Transporte,
        [Range(1, 5, ErrorMessage = "El puntaje de conectividad debe estar entre 1 y 5.")] int Conectividad,
        [Range(1, 5, ErrorMessage = "El puntaje de comercios y servicios debe estar entre 1 y 5.")] int Comercios,
        [Range(1, 5, ErrorMessage = "El puntaje de espacios verdes debe estar entre 1 y 5.")] int EspaciosVerdes);

    // PUT /Zonas/{slug}/resenas/mia: crea o reemplaza la resena del usuario actual.
    // El puntaje general no se envia: se calcula a partir de los aspectos.
    // Temas: codigos de AspectoZona (seguridad, transporte, conectividad, comercios, espacios-verdes).
    public record GuardarResenaDto(
        [Required(ErrorMessage = "Indica el puntaje de cada aspecto.")]
        PuntajesAspectosDto? Aspectos,
        [Required(ErrorMessage = "El texto de la resena es obligatorio.")]
        [StringLength(Longitudes.TextoResena, MinimumLength = Longitudes.TextoResenaMinimo,
            ErrorMessage = "La resena debe tener entre 20 y 1000 caracteres.")]
        string Texto,
        [Range(0, 100, ErrorMessage = "Los anos en la zona deben estar entre 0 y 100.")]
        int? AniosEnZona,
        IReadOnlyList<string>? Temas);
}
