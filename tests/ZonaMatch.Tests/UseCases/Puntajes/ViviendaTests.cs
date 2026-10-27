using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Puntajes;
using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Tests.UseCases.Puntajes
{
    public class ViviendaTests
    {
        [Fact]
        public void Sin_operacion_es_alquiler_y_el_tope_de_compra_no_cuenta()
        {
            var (operacion, compra, tipo) = Vivienda.Validar(null, 150_000, TiposVivienda.Todos);

            Assert.Equal(Operaciones.Alquiler, operacion);
            Assert.Null(compra);
            Assert.Null(tipo);
        }

        [Fact]
        public void Compra_con_tipo_de_vivienda()
        {
            var (operacion, compra, tipo) = Vivienda.Validar("compra", 150_000, "ph");

            Assert.Equal(Operaciones.Compra, operacion);
            Assert.Equal(150_000, compra);
            Assert.Equal(TiposVivienda.Ph, tipo);
        }

        [Theory]
        [InlineData("permuta", null, null)]
        [InlineData("compra", -1, null)]
        [InlineData("compra", 100, "loft")]
        public void Rechaza_lo_que_no_conoce(string operacion, int? compra, string? tipo)
        {
            Assert.Throws<BadRequestException>(() => Vivienda.Validar(operacion, compra, tipo));
        }
    }
}
