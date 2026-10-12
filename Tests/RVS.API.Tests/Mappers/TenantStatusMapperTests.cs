using FluentAssertions;
using RVS.API.Mappers;
using RVS.Domain.Entities;
using RVS.Domain.Validation;

namespace RVS.API.Tests.Mappers;

public class TenantStatusMapperTests
{
    [Fact]
    public void ToResponseDto_ShouldCarryEveryStatusInOrder()
    {
        var statuses = StatusVocabulary.Build(
            [new TenantStatusEmbedded { Id = "st_diag", Name = "Diagnosing", Color = "teal" }]);

        var dto = statuses.ToResponseDto();

        dto.Statuses.Select(s => s.Id).Should().Equal("New", "st_diag", "Completed", "Cancelled");
        dto.Statuses[1].Name.Should().Be("Diagnosing");
        dto.Statuses[1].Color.Should().Be("teal");
        dto.Statuses[1].IsFixed.Should().BeFalse();
        dto.Statuses[0].IsFixed.Should().BeTrue();
    }

    [Fact]
    public void ToResponseDto_WhenNull_ShouldThrowArgumentNullException()
    {
        var act = () => ((TenantStatusVocabulary)null!).ToResponseDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToResponseDto_ShouldCarryTheRedirects()
    {
        var vocabulary = StatusVocabulary.Build(
        [
            new TenantStatusEmbedded { Id = "st_diag", Name = "Diagnosing", Color = "teal" },
            new TenantStatusEmbedded { Id = "st_old", Name = "Old", Color = "blue", Retired = true, ReplacedBy = "st_diag" },
        ]);

        var dto = vocabulary.ToResponseDto();

        dto.Redirects.Should().ContainKey("st_old").WhoseValue.Should().Be("st_diag");
        dto.Statuses.Select(s => s.Id).Should().NotContain("st_old");
    }

    [Fact]
    public void ToCustomerStatusItemDto_ForADeletedStatus_ShouldShowItsReplacement()
    {
        var vocabulary = StatusVocabulary.Build(
        [
            new TenantStatusEmbedded { Id = "st_diag", Name = "Diagnosing", Color = "teal" },
            new TenantStatusEmbedded { Id = "st_old", Name = "Old", Color = "blue", Retired = true, ReplacedBy = "st_diag" },
        ]);

        var dto = new ServiceRequest { Status = "st_old" }.ToCustomerStatusItemDto(location: null, vocabulary);

        dto.Status.Should().Be("st_diag");
        dto.StatusLabel.Should().Be("Diagnosing");
        dto.StatusColor.Should().Be("teal");
    }

    [Fact]
    public void ToCustomerStatusItemDto_WithVocabulary_ShouldCarryTheTenantsNameAndColor()
    {
        var statuses = StatusVocabulary.Build(
            [new TenantStatusEmbedded { Id = "st_diag", Name = "Diagnosing", Color = "teal" }]);
        var sr = new ServiceRequest { Status = "st_diag" };

        var dto = sr.ToCustomerStatusItemDto(location: null, statuses);

        dto.Status.Should().Be("st_diag");
        dto.StatusLabel.Should().Be("Diagnosing");
        dto.StatusColor.Should().Be("teal");
    }

    [Fact]
    public void ToCustomerStatusItemDto_WhenStatusIsNotInTheVocabulary_ShouldLeaveLabelAndColorNull()
    {
        var sr = new ServiceRequest { Status = "st_deleted" };

        var dto = sr.ToCustomerStatusItemDto(location: null, StatusVocabulary.Build(null));

        dto.StatusLabel.Should().BeNull();
        dto.StatusColor.Should().BeNull();
    }

    [Fact]
    public void ToCustomerStatusItemDto_WithoutVocabulary_ShouldLeaveLabelAndColorNull()
    {
        var dto = new ServiceRequest { Status = "InProgress" }.ToCustomerStatusItemDto(location: null);

        dto.StatusLabel.Should().BeNull();
    }
}
