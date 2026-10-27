using ZonaMatch.Application.UseCases.Groups;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Tests.UseCases.Groups;

public class GroupHousingSuggestionTests
{
    private static Contexto Profile(string operation, string? monthly, decimal? purchase = null, string? type = null) =>
        new() { Operacion = operation, AlquilerMaximo = monthly, CompraMaximaUsd = purchase, TipoVivienda = type };

    [Fact]
    public void When_everyone_buys_the_group_buys_and_adds_up_the_caps()
    {
        var suggestion = GroupHousingSuggestion.From([
            Profile(Operaciones.Compra, "150000", 100_000, TiposVivienda.Departamento),
            Profile(Operaciones.Compra, "50000", 60_000, TiposVivienda.Departamento),
        ]);

        Assert.Equal(new GroupHousingSuggestion(Operaciones.Compra, 200_000, 160_000, TiposVivienda.Departamento), suggestion);
    }

    [Fact]
    public void A_member_that_rents_makes_the_group_rent_and_only_rents_add_up()
    {
        var suggestion = GroupHousingSuggestion.From([
            Profile(Operaciones.Compra, "150000", 100_000, TiposVivienda.Casa),
            Profile(Operaciones.Alquiler, "800000", type: TiposVivienda.Ph),
        ]);

        // The 150.000 of the buyer are expenses, not rent: they do not count for a rent budget
        Assert.Equal(new GroupHousingSuggestion(Operaciones.Alquiler, 800_000, null, null), suggestion);
    }
}
