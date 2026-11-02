using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Domain
{
    public class ContributionTests
    {
        private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private static void Votar(Contribution aporte, int cantidad, bool confirma)
        {
            for (var i = 0; i < cantidad; i++)
                aporte.Validar(Guid.NewGuid(), confirma, Ahora);
        }

        [Fact]
        public void Tres_confirmaciones_aprueban_el_aporte()
        {
            var aporte = new Contribution();

            Votar(aporte, 2, confirma: true);
            Assert.Equal(ContributionStatus.Pendiente, aporte.Estado);

            Votar(aporte, 1, confirma: true);
            Assert.Equal(ContributionStatus.Aprobado, aporte.Estado);
            Assert.Equal(Ahora, aporte.FechaResolucion);
        }

        [Fact]
        public void Un_rechazo_compensa_una_confirmacion()
        {
            var aporte = new Contribution();

            Votar(aporte, 1, confirma: false);
            Votar(aporte, 3, confirma: true);
            Assert.Equal(ContributionStatus.Pendiente, aporte.Estado);

            Votar(aporte, 1, confirma: true);
            Assert.Equal(ContributionStatus.Aprobado, aporte.Estado);
        }

        [Fact]
        public void Tres_rechazos_de_diferencia_rechazan_el_aporte()
        {
            var aporte = new Contribution();

            Votar(aporte, 3, confirma: false);

            Assert.Equal(ContributionStatus.Rechazado, aporte.Estado);
        }

        [Fact]
        public void Votar_de_nuevo_cambia_el_voto_sin_sumar_otro()
        {
            var aporte = new Contribution();
            var usuario = Guid.NewGuid();

            aporte.Validar(usuario, confirma: true, Ahora);
            aporte.Validar(usuario, confirma: false, Ahora);

            var voto = Assert.Single(aporte.Validaciones);
            Assert.False(voto.Confirma);
            Assert.Equal(0, aporte.Confirmaciones);
            Assert.Equal(1, aporte.Rechazos);
        }
    }
}
