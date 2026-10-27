using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Application.UseCases.Puntajes
{
    // Alquilar o comprar, tope de compra y tipo de vivienda: las mismas reglas para el perfil, los analisis,
    // el puntaje de visitantes y los grupos
    public static class Vivienda
    {
        public const decimal CompraMaximaPermitidaUsd = 100_000_000;
        public const int SuperficieMinimaM2 = 10;
        public const int SuperficieMaximaM2 = 2000;

        // Sin superficie (o 0), los precios son los promedios de cada zona
        public static int? ValidarSuperficie(int? superficieM2)
        {
            if (superficieM2 is null or 0)
                return null;

            if (superficieM2 is < SuperficieMinimaM2 or > SuperficieMaximaM2)
                throw new BadRequestException("La superficie debe estar entre 10 y 2.000 m².");

            return superficieM2;
        }

        // Sin operacion es alquiler (los perfiles de antes de que hubiera compra). El tope de compra solo vale si
        // compra; sin tipo de vivienda (o "all"), todos los tipos.
        public static (string Operacion, decimal? CompraMaximaUsd, string? TipoVivienda) Validar(
            string? operacion, decimal? compraMaximaUsd, string? tipoVivienda)
        {
            var pedida = string.IsNullOrWhiteSpace(operacion) ? Operaciones.Alquiler : operacion.Trim();
            if (!Operaciones.EsValida(pedida))
                throw new BadRequestException("La operación debe ser alquiler o compra.");

            if (compraMaximaUsd is < 0 or > CompraMaximaPermitidaUsd)
                throw new BadRequestException("El precio máximo de compra no es válido.");

            var tipo = string.IsNullOrWhiteSpace(tipoVivienda) || tipoVivienda.Trim() == TiposVivienda.Todos ? null : tipoVivienda.Trim();
            if (tipo is not null && !TiposVivienda.EsValido(tipo))
                throw new BadRequestException("El tipo de vivienda debe ser departamento, casa o PH.");

            var compra = pedida == Operaciones.Compra && compraMaximaUsd > 0 ? compraMaximaUsd : null;
            return (pedida, compra, tipo);
        }

        // Alquiler del tipo pedido en la zona; null sin dato. Con superficie, el costo mensual usa el alquiler estimado.
        public static AlquilerZonaDto? Alquiler(PerfilZona zona, string? tipo, int? superficieM2 = null)
        {
            var mercado = zona.Mercado(tipo);
            return mercado.AlquilerPromedio is { } promedio
                ? new AlquilerZonaDto(
                    promedio,
                    mercado.PropiedadesEnAlquiler ?? 0,
                    mercado.ExpensasPromedio,
                    mercado.CostoMensualAlquilerPara(superficieM2),
                    mercado.AlquilerM2,
                    mercado.EstimaAlquiler(superficieM2) ? superficieM2 : null,
                    mercado.EstimaAlquiler(superficieM2) ? mercado.AlquilerPara(superficieM2) : null)
                : null;
        }

        // Venta del tipo pedido en la zona; null sin dato
        public static CompraZonaDto? Compra(PerfilZona zona, string? tipo, int? superficieM2 = null)
        {
            var mercado = zona.Mercado(tipo);
            return mercado.VentaPromedioUsd is { } promedio
                ? new CompraZonaDto(
                    promedio,
                    mercado.PropiedadesEnVenta ?? 0,
                    mercado.ExpensasPromedio,
                    mercado.VentaM2Usd,
                    mercado.EstimaVenta(superficieM2) ? superficieM2 : null,
                    mercado.EstimaVenta(superficieM2) ? mercado.VentaParaUsd(superficieM2) : null)
                : null;
        }
    }
}
