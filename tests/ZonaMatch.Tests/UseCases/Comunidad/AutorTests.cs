using ZonaMatch.Application.UseCases.Comunidad;

namespace ZonaMatch.Tests.UseCases.Comunidad
{
    public class AutorTests
    {
        [Theory]
        [InlineData("ana.perez@example.com", "AP")]
        [InlineData("luis_carlos@example.com", "LC")]
        [InlineData("juan@example.com", "JU")]
        [InlineData("j@example.com", "J")]
        [InlineData("1990.mara@example.com", "MA")]
        [InlineData("123@example.com", "?")]
        [InlineData(null, "?")]
        public void Las_iniciales_salen_del_email_sin_exponerlo(string? email, string esperado)
        {
            Assert.Equal(esperado, Autor.Iniciales(email));
        }
    }
}
