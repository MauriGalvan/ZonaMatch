using FluentAssertions;
using Moq;
using ZonaMatch.Application.UseCases.Criterion;
using CriterionRepository = ZonaMatch.Application.Interfaces.ICriterionRepository;
using DomainCriterion = ZonaMatch.Domain.Entities.Criterion;

namespace ZonaMatch.Tests.UseCases.Criterion;

public class ListCriteriaTests
{
    [Fact]
    public async Task ListCriteria_ExcludesInactiveItemsAndMapsActiveCriteriaInRepositoryOrder()
    {
        var repository = new Mock<CriterionRepository>();
        repository.Setup(item => item.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new DomainCriterion { Id = "mobility", Name = "Mobility", Order = 2, IsActive = true },
            new DomainCriterion { Id = "hidden", Name = "Hidden", Order = 1, IsActive = false },
            new DomainCriterion { Id = "safety", Name = "Safety", Order = 3, IsActive = true }
        });

        var result = await new ListCriteria(repository.Object).ExecuteAsync();

        result.Select(item => item.Id).Should().Equal("mobility", "safety");
        result.Select(item => item.Order).Should().Equal(2, 3);
    }
}
