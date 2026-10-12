using FluentAssertions;
using RVS.Domain.Entities;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class StatusTransitionsTests
{
    private static readonly IReadOnlyList<StatusDefinition> Defaults = StatusVocabulary.Resolve(null);

    private static readonly IReadOnlyList<StatusDefinition> Custom = StatusVocabulary.Resolve(
    [
        new TenantStatusEmbedded { Id = "st_diag", Name = "Diagnosing", Color = "blue" },
    ]);

    [Theory]
    [InlineData("New", "InProgress")]
    [InlineData("New", "Completed")]
    [InlineData("New", "Cancelled")]
    [InlineData("New", "WaitingOnParts")]
    [InlineData("New", "WaitingOnCustomer")]
    [InlineData("InProgress", "New")]
    [InlineData("InProgress", "WaitingOnCustomer")]
    [InlineData("WaitingOnParts", "Completed")]
    [InlineData("WaitingOnCustomer", "Cancelled")]
    [InlineData("Completed", "New")]
    [InlineData("Completed", "InProgress")]
    [InlineData("Cancelled", "New")]
    [InlineData("Cancelled", "Completed")]
    public void IsValid_DefaultVocabulary_AnyStatusToAnyOther_ReturnsTrue(string from, string to)
    {
        StatusTransitions.IsValid(from, to, Defaults).Should().BeTrue();
    }

    [Theory]
    [InlineData("New")]
    [InlineData("InProgress")]
    [InlineData("WaitingOnParts")]
    [InlineData("WaitingOnCustomer")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void IsValid_SameStatus_ReturnsFalse(string status)
    {
        StatusTransitions.IsValid(status, status, Defaults).Should().BeFalse();
    }

    [Fact]
    public void IsValid_TargetNotInTheTenantsVocabulary_ReturnsFalse()
    {
        StatusTransitions.IsValid("New", "InProgress", Custom).Should().BeFalse();
    }

    [Fact]
    public void IsValid_TargetIsATenantDefinedStatus_ReturnsTrue()
    {
        StatusTransitions.IsValid("New", "st_diag", Custom).Should().BeTrue();
    }

    [Fact]
    public void IsValid_FromAStatusTheTenantDeleted_ToAKnownStatus_ReturnsTrue()
    {
        // A request left in a deleted status must still be movable to one that exists.
        StatusTransitions.IsValid("InProgress", "st_diag", Custom).Should().BeTrue();
    }

    [Fact]
    public void IsValid_UnknownTarget_ReturnsFalse()
    {
        StatusTransitions.IsValid("New", "Unknown", Defaults).Should().BeFalse();
    }

    [Fact]
    public void IsValid_TargetMatchIsCaseSensitive()
    {
        StatusTransitions.IsValid("New", "inprogress", Defaults).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "InProgress")]
    [InlineData("New", null)]
    [InlineData("", "InProgress")]
    [InlineData("New", "")]
    [InlineData("  ", "InProgress")]
    [InlineData("New", "  ")]
    public void IsValid_NullOrWhitespace_ThrowsArgumentException(string? from, string? to)
    {
        var act = () => StatusTransitions.IsValid(from!, to!, Defaults);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsValid_NullVocabulary_ThrowsArgumentNullException()
    {
        var act = () => StatusTransitions.IsValid("New", "InProgress", null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
