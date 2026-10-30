using ZonaMatch.Application.Exceptions;
using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Application.UseCases.Puntajes
{
    // The engine needs every limit: without them it would fall back to defaults the user never chose.
    // Only checked on new input (scoring request, profile, new version); saved analyses recalculate as they are.
    public static class LimitesObligatorios
    {
        public static void Validar(string? gastoMensual, string? operacion, decimal? compraMaximaUsd, IEnumerable<string?> tiemposMaximos)
        {
            // Buying, the monthly cap is only the building fee (expensas): optional, and 0 means no cap
            if (operacion?.Trim() == Operaciones.Compra)
            {
                if (compraMaximaUsd is not > 0)
                    throw new BadRequestException("El precio máximo de compra es obligatorio.");
            }
            else if (!TieneNumeroPositivo(gastoMensual))
                throw new BadRequestException("El gasto máximo por mes es obligatorio.");

            if (tiemposMaximos.Any(tiempo => !TieneNumeroPositivo(tiempo)))
                throw new BadRequestException("El tiempo máximo de traslado es obligatorio en cada lugar.");
        }

        // "$ 800.000", "800000" or "45 min" -> true; blank or zero -> false
        private static bool TieneNumeroPositivo(string? valor)
        {
            var digitos = new string((valor ?? string.Empty).Where(char.IsDigit).ToArray());
            return decimal.TryParse(digitos, out var numero) && numero > 0;
        }
    }
}
