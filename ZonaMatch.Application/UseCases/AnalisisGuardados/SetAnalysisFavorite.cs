using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.AnalisisGuardados;

// Stars or unstars one of the user's analyses (or versions). It is a list preference, not a change to the
// analysis, so the update date stays as it was.
public sealed class SetAnalysisFavorite
{
    private readonly IAnalisisRepository _analyses;

    public SetAnalysisFavorite(IAnalisisRepository analyses)
    {
        _analyses = analyses;
    }

    public async Task ExecuteAsync(Guid id, Guid userId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        var analysis = await _analyses.ObtenerAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException("El análisis no existe.");

        if (analysis.EsFavorito == isFavorite)
            return;

        analysis.EsFavorito = isFavorite;
        await _analyses.GuardarCambiosAsync(cancellationToken);
    }
}
