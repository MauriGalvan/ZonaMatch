using ZonaMatch.Application.UseCases.Community;

namespace ZonaMatch.Tests.UseCases.Community
{
    public class AuthorInitialsTests
    {
        [Theory]
        [InlineData("ana.perez@example.com", "AP")]
        [InlineData("luis_carlos@example.com", "LC")]
        [InlineData("juan@example.com", "JU")]
        [InlineData("j@example.com", "J")]
        [InlineData("1990.mara@example.com", "MA")]
        [InlineData("123@example.com", "?")]
        [InlineData(null, "?")]
        public void GetInitials_DerivesInitialsWithoutExposingTheEmail(string? email, string expected)
        {
            Assert.Equal(expected, AuthorInitials.GetInitials(email));
        }
    }
}
