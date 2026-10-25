using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Aportes
{
    // Persists an approved contribution as a point of interest: a new point is added, a correction changes the
    // name, category or location of the place, or removes it if it closed. Shared by the community vote
    // (ValidarAporte) and the administrator (ResolveContribution). The caller saves the changes.
    public class CommunityPoiUpdater
    {
        private readonly ICommunityPoiRepository _pois;

        public CommunityPoiUpdater(ICommunityPoiRepository pois)
        {
            _pois = pois;
        }

        public async Task ApplyAsync(Aporte contribution, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            if (contribution.Estado != EstadoAporte.Aprobado)
                return;

            if (contribution.Tipo == TipoAporte.PuntoNuevo)
            {
                if (await _pois.GetAsync(CommunityPoi.NewPointId(contribution.Id), cancellationToken) is null)
                    _pois.Add(CommunityPoi.FromNewPoint(contribution, now));
                return;
            }

            if (contribution.PuntoInteresId is not { } poiId || contribution.Motivo == MotivoCorreccion.Otro)
                return;

            var poi = await _pois.GetAsync(poiId, cancellationToken);
            if (poi is null)
            {
                poi = CommunityPoi.ForOsmPoint(poiId, contribution.ZonaSlug);
                _pois.Add(poi);
            }

            poi.ApplyCorrection(contribution, now);
        }
    }
}
