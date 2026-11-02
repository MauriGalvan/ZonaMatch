using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Puntajes;

namespace ZonaMatch.Tests.UseCases.Puntajes;

public class FreeCriteriaLimitTests
{
    private static readonly string[] Catalog = ["seguridad", "movilidad", "espacios-verdes", "educacion", "salud"];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void A_free_account_can_choose_up_to_two_criteria(int count)
    {
        FreeCriteriaLimit.Validate(true, Catalog.Take(count));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void A_free_account_cannot_choose_more_than_two_criteria(int count)
    {
        var error = Assert.Throws<BadRequestException>(() => FreeCriteriaLimit.Validate(true, Catalog.Take(count)));
        Assert.Contains("2 criterios", error.Message);
    }

    [Fact]
    public void A_paid_account_has_no_limit()
    {
        FreeCriteriaLimit.Validate(false, Catalog);
    }

    [Fact]
    public void A_repeated_criterion_counts_once()
    {
        FreeCriteriaLimit.Validate(true, ["seguridad", "Seguridad", " seguridad ", "movilidad"]);
    }

    [Fact]
    public void Blank_entries_are_not_criteria()
    {
        FreeCriteriaLimit.Validate(true, ["seguridad", null, " ", "movilidad"]);
    }
}