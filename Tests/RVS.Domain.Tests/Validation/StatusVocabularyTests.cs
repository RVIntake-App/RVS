using FluentAssertions;
using RVS.Domain.Entities;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

public class StatusVocabularyTests
{
    // ── Resolve ──────────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_WhenNoStoredStatuses_ReturnsTheDefaultSixInOrder()
    {
        var statuses = StatusVocabulary.Resolve(null);

        statuses.Select(s => s.Id).Should().Equal(
            "New", "InProgress", "WaitingOnParts", "WaitingOnCustomer", "Completed", "Cancelled");
    }

    [Fact]
    public void Resolve_WhenNoStoredStatuses_NamesTheDefaultsAsTheyDisplayToday()
    {
        var statuses = StatusVocabulary.Resolve(null);

        statuses.Select(s => s.Name).Should().Equal(
            "New", "In Progress", "Waiting on Parts", "Waiting on Customer", "Completed", "Cancelled");
    }

    [Fact]
    public void Resolve_MarksOnlyNewCompletedAndCancelledAsFixed()
    {
        var statuses = StatusVocabulary.Resolve(null);

        statuses.Where(s => s.IsFixed).Select(s => s.Id).Should().Equal("New", "Completed", "Cancelled");
    }

    [Fact]
    public void Resolve_WhenStoredStatusesExist_PlacesThemBetweenNewAndCompleted()
    {
        var stored = new List<TenantStatusEmbedded>
        {
            new() { Id = "st_diag", Name = "Diagnosing", Color = "blue" },
            new() { Id = "st_repair", Name = "Repairing", Color = "amber" },
        };

        var statuses = StatusVocabulary.Resolve(stored);

        statuses.Select(s => s.Id).Should().Equal("New", "st_diag", "st_repair", "Completed", "Cancelled");
        statuses[1].Name.Should().Be("Diagnosing");
        statuses[1].Color.Should().Be("blue");
        statuses[1].IsFixed.Should().BeFalse();
    }

    [Fact]
    public void Resolve_WhenStoredListIsEmpty_ReturnsOnlyTheFixedThree()
    {
        var statuses = StatusVocabulary.Resolve([]);

        statuses.Select(s => s.Id).Should().Equal("New", "Completed", "Cancelled");
    }

    [Fact]
    public void DefaultIntermediateStatuses_ReturnsAFreshListEachCall()
    {
        var first = StatusVocabulary.DefaultIntermediateStatuses();
        first[0].Name = "Changed";

        StatusVocabulary.DefaultIntermediateStatuses()[0].Name.Should().Be("In Progress");
    }

    // ── IsFixed ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("New", true)]
    [InlineData("Completed", true)]
    [InlineData("Cancelled", true)]
    [InlineData("InProgress", false)]
    [InlineData("st_custom", false)]
    public void IsFixed_ReturnsWhetherTheIdIsOneOfTheFixedThree(string id, bool expected)
    {
        StatusVocabulary.IsFixed(id).Should().Be(expected);
    }

    // ── Validate ─────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_DefaultIntermediates_IsValid()
    {
        StatusVocabulary.Validate(StatusVocabulary.DefaultIntermediateStatuses()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyList_IsValid()
    {
        StatusVocabulary.Validate([]).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullList_ThrowsArgumentNullException()
    {
        var act = () => StatusVocabulary.Validate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_MaximumIntermediates_IsValid()
    {
        var statuses = Enumerable.Range(1, StatusVocabulary.MaxIntermediateStatuses)
            .Select(i => Status($"st_{i}", $"Step {i}"))
            .ToList();

        StatusVocabulary.Validate(statuses).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TooManyIntermediates_Fails()
    {
        var statuses = Enumerable.Range(1, StatusVocabulary.MaxIntermediateStatuses + 1)
            .Select(i => Status($"st_{i}", $"Step {i}"))
            .ToList();

        var result = StatusVocabulary.Validate(statuses);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("12");
    }

    [Fact]
    public void MaxIntermediateStatuses_IsTwelveLessTheFixedThree()
    {
        StatusVocabulary.MaxStatuses.Should().Be(12);
        StatusVocabulary.MaxIntermediateStatuses.Should().Be(9);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankName_Fails(string name)
    {
        StatusVocabulary.Validate([Status("st_1", name)]).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NameAtMaximumLength_IsValid()
    {
        StatusVocabulary.Validate([Status("st_1", new string('a', StatusVocabulary.MaxNameLength))])
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NameOverMaximumLength_Fails()
    {
        StatusVocabulary.Validate([Status("st_1", new string('a', StatusVocabulary.MaxNameLength + 1))])
            .IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Bay <3>")]
    [InlineData("Bay\03")]
    [InlineData("Bay\n3")]
    public void Validate_NameWithBlockedOrControlCharacter_Fails(string name)
    {
        StatusVocabulary.Validate([Status("st_1", name)]).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NameWithOrdinaryPunctuation_IsValid()
    {
        StatusVocabulary.Validate([Status("st_1", "Jay's van; \"hold\"")]).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DuplicateNamesIgnoringCase_Fails()
    {
        var result = StatusVocabulary.Validate([Status("st_1", "Diagnosing"), Status("st_2", " diagnosing ")]);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("New")]
    [InlineData("completed")]
    [InlineData("CANCELLED")]
    public void Validate_NameOfAFixedStatus_Fails(string name)
    {
        StatusVocabulary.Validate([Status("st_1", name)]).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("New")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void Validate_IdOfAFixedStatus_Fails(string id)
    {
        StatusVocabulary.Validate([Status(id, "Something")]).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_DuplicateIds_Fails()
    {
        StatusVocabulary.Validate([Status("st_1", "One"), Status("st_1", "Two")]).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_BlankId_Fails(string id)
    {
        StatusVocabulary.Validate([Status(id, "One")]).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("amber")]
    [InlineData("teal")]
    [InlineData("green")]
    [InlineData("purple")]
    [InlineData("gray")]
    public void Validate_PaletteColor_IsValid(string color)
    {
        StatusVocabulary.Validate([Status("st_1", "One", color)]).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("#FF0000")]
    [InlineData("red")]
    [InlineData("")]
    public void Validate_ColorOutsideThePalette_Fails(string color)
    {
        StatusVocabulary.Validate([Status("st_1", "One", color)]).IsValid.Should().BeFalse();
    }

    // ── Retired statuses (a deleted status points to its replacement) ────────

    [Fact]
    public void Resolve_LeavesRetiredStatusesOffTheBoard()
    {
        var statuses = StatusVocabulary.Resolve([Status("st_1", "Diagnosing"), Retired("st_old", "Old", "New")]);

        statuses.Select(s => s.Id).Should().Equal("New", "st_1", "Completed", "Cancelled");
    }

    [Fact]
    public void Build_MapsEachRetiredStatusToItsReplacement()
    {
        var vocabulary = StatusVocabulary.Build([Status("st_1", "Diagnosing"), Retired("st_old", "Old", "st_1")]);

        vocabulary.Redirects.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>("st_old", "st_1"));
        vocabulary.Statuses.Select(s => s.Id).Should().Equal("New", "st_1", "Completed", "Cancelled");
    }

    [Fact]
    public void Build_WithNoStoredStatuses_HasTheDefaultsAndNoRedirects()
    {
        var vocabulary = StatusVocabulary.Build(null);

        vocabulary.Statuses.Should().HaveCount(6);
        vocabulary.Redirects.Should().BeEmpty();
    }

    [Fact]
    public void Build_FollowsAChainToTheActiveStatus()
    {
        // Normally the service keeps pointers one hop deep; resolution must still never stop on a retired id.
        var vocabulary = StatusVocabulary.Build(
            [Status("st_3", "Three"), Retired("st_1", "One", "st_2"), Retired("st_2", "Two", "st_3")]);

        vocabulary.Redirects["st_1"].Should().Be("st_3");
        vocabulary.Redirects["st_2"].Should().Be("st_3");
    }

    [Fact]
    public void Build_WithACycle_FallsBackToNew()
    {
        var vocabulary = StatusVocabulary.Build([Retired("st_1", "One", "st_2"), Retired("st_2", "Two", "st_1")]);

        vocabulary.Redirects["st_1"].Should().Be("New");
    }

    [Fact]
    public void ResolveId_ForARetiredStatus_ReturnsItsReplacement()
    {
        var vocabulary = StatusVocabulary.Build([Status("st_1", "Diagnosing"), Retired("st_old", "Old", "st_1")]);

        vocabulary.ResolveId("st_old").Should().Be("st_1");
        vocabulary.ResolveId("st_1").Should().Be("st_1");
        vocabulary.ResolveId("Completed").Should().Be("Completed");
    }

    [Fact]
    public void Find_ForARetiredStatus_ReturnsTheReplacementsDefinition()
    {
        var vocabulary = StatusVocabulary.Build([Status("st_1", "Diagnosing"), Retired("st_old", "Old", "st_1")]);

        vocabulary.Find("st_old")!.Name.Should().Be("Diagnosing");
        vocabulary.Find("st_unknown").Should().BeNull();
    }

    [Fact]
    public void Validate_RetiredStatusesDoNotCountTowardTheLimit()
    {
        var statuses = Enumerable.Range(1, StatusVocabulary.MaxIntermediateStatuses)
            .Select(i => Status($"st_{i}", $"Step {i}"))
            .Append(Retired("st_old", "Old", "New"))
            .ToList();

        StatusVocabulary.Validate(statuses).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ANewStatusMayReuseARetiredStatusName()
    {
        StatusVocabulary.Validate([Status("st_2", "Diagnosing"), Retired("st_1", "Diagnosing", "st_2")])
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("New")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    [InlineData("st_1")]
    public void Validate_RetiredReplacementIsAnActiveOrFixedStatus_IsValid(string replacedBy)
    {
        StatusVocabulary.Validate([Status("st_1", "One"), Retired("st_old", "Old", replacedBy)])
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("st_missing")]
    [InlineData("st_other_retired")]
    [InlineData("")]
    public void Validate_RetiredReplacementIsNotAnActiveStatus_Fails(string replacedBy)
    {
        StatusVocabulary.Validate([Retired("st_other_retired", "Other", "New"), Retired("st_old", "Old", replacedBy)])
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_RetiredIdSharedWithAnActiveStatus_Fails()
    {
        StatusVocabulary.Validate([Status("st_1", "One"), Retired("st_1", "Old", "New")]).IsValid.Should().BeFalse();
    }

    private static TenantStatusEmbedded Retired(string id, string name, string replacedBy) =>
        new() { Id = id, Name = name, Color = "blue", Retired = true, ReplacedBy = replacedBy };

    private static TenantStatusEmbedded Status(string id, string name, string color = "blue") =>
        new() { Id = id, Name = name, Color = color };
}
