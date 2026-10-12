using FluentAssertions;
using RVS.Blazor.Manager.Shared;
using RVS.Domain.DTOs;

namespace RVS.UI.Shared.Tests.Manager;

/// <summary>
/// Tests for <see cref="StatusVocabularyView"/> — the manager app's view of a tenant's status
/// vocabulary (<c>Spec C-8</c>, issue #855): labels, board columns and the edits it sends.
/// </summary>
public class StatusVocabularyViewTests
{
    private static readonly List<TenantStatusDto> Custom =
    [
        Fixed("New", "New", "green"),
        Own("st_diag", "Diagnosing", "blue"),
        Own("st_repair", "Repairing", "amber"),
        Fixed("Completed", "Completed", "purple"),
        Fixed("Cancelled", "Cancelled", "gray"),
    ];

    // ── Defaults and labels ──────────────────────────────────────────────────

    [Fact]
    public void Defaults_ShouldBeTheSixStatusesInBoardOrder()
    {
        StatusVocabularyView.Defaults.Select(s => s.Id).Should().Equal(
            "New", "InProgress", "WaitingOnParts", "WaitingOnCustomer", "Completed", "Cancelled");
        StatusVocabularyView.Defaults.Select(s => s.Name).Should().Equal(
            "New", "In Progress", "Waiting on Parts", "Waiting on Customer", "Completed", "Cancelled");
    }

    [Fact]
    public void Label_ForAStatusInTheVocabulary_ShouldReturnItsName()
    {
        StatusVocabularyView.Label(Custom, "st_diag").Should().Be("Diagnosing");
    }

    [Fact]
    public void Label_ForAnUnknownStatus_ShouldEchoIt()
    {
        StatusVocabularyView.Label(Custom, "Mystery").Should().Be("Mystery");
    }

    [Fact]
    public void Label_ForNull_ShouldReturnEmpty()
    {
        StatusVocabularyView.Label(Custom, null).Should().BeEmpty();
    }

    [Fact]
    public void Color_ForAStatusInTheVocabulary_ShouldReturnItsColor()
    {
        StatusVocabularyView.Color(Custom, "st_repair").Should().Be("amber");
        StatusVocabularyView.Color(Custom, "Mystery").Should().BeNull();
    }

    // ── Board columns (Spec C-10) ────────────────────────────────────────────

    [Fact]
    public void BuildColumns_ShouldBeNewThenEachTenantStatusThenDone()
    {
        var columns = StatusVocabularyView.BuildColumns(Custom);

        columns.Select(c => c.ZoneId).Should().Equal("New", "st_diag", "st_repair", "Completed");
        columns.Select(c => c.Title).Should().Equal("New", "Diagnosing", "Repairing", "Done");
    }

    [Fact]
    public void BuildColumns_OnlyTenantStatusesAreEditable()
    {
        var columns = StatusVocabularyView.BuildColumns(Custom);

        columns.Where(c => c.IsEditable).Select(c => c.ZoneId).Should().Equal("st_diag", "st_repair");
    }

    [Fact]
    public void BuildColumns_WithNoTenantStatuses_ShouldBeNewAndDone()
    {
        var columns = StatusVocabularyView.BuildColumns([Custom[0], Custom[3], Custom[4]]);

        columns.Select(c => c.ZoneId).Should().Equal("New", "Completed");
    }

    [Theory]
    [InlineData("New", "New")]
    [InlineData("st_diag", "st_diag")]
    [InlineData("st_repair", "st_repair")]
    [InlineData("Completed", "Completed")]
    [InlineData("Cancelled", "Completed")]
    public void ZoneFor_ShouldPlaceEachStatusInItsColumn(string status, string expectedZone)
    {
        StatusVocabularyView.ZoneFor(Custom, status).Should().Be(expectedZone);
    }

    [Fact]
    public void ZoneFor_AStatusNoLongerInTheVocabulary_ShouldFallBackToNew()
    {
        StatusVocabularyView.ZoneFor(Custom, "InProgress").Should().Be("New");
    }

    [Fact]
    public void StatusForZone_TheDoneColumn_ShouldSetCompleted()
    {
        StatusVocabularyView.StatusForZone("Completed").Should().Be("Completed");
        StatusVocabularyView.StatusForZone("st_diag").Should().Be("st_diag");
    }

    // ── Edits ────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_ShouldAppendANewStatusWithoutAnId()
    {
        var request = StatusVocabularyView.Add(Custom, "  Road Test ", "teal");

        request.IntermediateStatuses.Select(s => s.Id).Should().Equal("st_diag", "st_repair", null);
        request.IntermediateStatuses[^1].Name.Should().Be("Road Test");
        request.IntermediateStatuses[^1].Color.Should().Be("teal");
    }

    [Fact]
    public void Edit_ShouldChangeOnlyThatStatusNameAndColor()
    {
        var request = StatusVocabularyView.Edit(Custom, "st_repair", " Fixing ", "green");

        request.IntermediateStatuses.Select(s => s.Id).Should().Equal("st_diag", "st_repair");
        request.IntermediateStatuses.Select(s => s.Name).Should().Equal("Diagnosing", "Fixing");
        request.IntermediateStatuses.Select(s => s.Color).Should().Equal("blue", "green");
    }

    [Fact]
    public void Move_Right_ShouldSwapWithTheNextStatus()
    {
        var request = StatusVocabularyView.Move(Custom, "st_diag", +1);

        request.IntermediateStatuses.Select(s => s.Id).Should().Equal("st_repair", "st_diag");
    }

    [Fact]
    public void Move_Left_ShouldSwapWithThePreviousStatus()
    {
        var request = StatusVocabularyView.Move(Custom, "st_repair", -1);

        request.IntermediateStatuses.Select(s => s.Id).Should().Equal("st_repair", "st_diag");
    }

    [Fact]
    public void Move_PastEitherEnd_ShouldLeaveTheOrderUnchanged()
    {
        StatusVocabularyView.Move(Custom, "st_diag", -1).IntermediateStatuses.Select(s => s.Id)
            .Should().Equal("st_diag", "st_repair");
        StatusVocabularyView.Move(Custom, "st_repair", +1).IntermediateStatuses.Select(s => s.Id)
            .Should().Equal("st_diag", "st_repair");
    }

    [Fact]
    public void CanMove_ShouldBeFalseAtEitherEnd()
    {
        StatusVocabularyView.CanMove(Custom, "st_diag", -1).Should().BeFalse();
        StatusVocabularyView.CanMove(Custom, "st_diag", +1).Should().BeTrue();
        StatusVocabularyView.CanMove(Custom, "st_repair", +1).Should().BeFalse();
    }

    [Fact]
    public void Delete_ShouldDropTheStatusAndReassignItsRequests()
    {
        var request = StatusVocabularyView.Delete(Custom, "st_diag", moveRequestsTo: "st_repair");

        request.IntermediateStatuses.Select(s => s.Id).Should().Equal("st_repair");
        request.Reassignments.Should().ContainSingle().Which.Should().Be(
            new KeyValuePair<string, string>("st_diag", "st_repair"));
    }

    [Fact]
    public void DeleteTargets_ShouldBeEveryOtherStatus()
    {
        StatusVocabularyView.DeleteTargets(Custom, "st_diag").Select(s => s.Id).Should().Equal(
            "New", "st_repair", "Completed", "Cancelled");
    }

    [Fact]
    public void CanAdd_ShouldBeFalseAtTwelveStatuses()
    {
        var full = Enumerable.Range(1, 9).Select(i => Own($"st_{i}", $"S{i}", "blue")).ToList();
        full.Insert(0, Custom[0]);
        full.Add(Custom[3]);
        full.Add(Custom[4]);

        StatusVocabularyView.CanAdd(full).Should().BeFalse();
        StatusVocabularyView.CanAdd(Custom).Should().BeTrue();
    }

    [Fact]
    public void Edits_ShouldNeverSendAFixedStatus()
    {
        StatusVocabularyView.Edit(Custom, "st_diag", "X", "blue").IntermediateStatuses
            .Should().NotContain(s => s.Id == "New" || s.Id == "Completed" || s.Id == "Cancelled");
    }

    private static TenantStatusDto Fixed(string id, string name, string color) =>
        new() { Id = id, Name = name, Color = color, IsFixed = true };

    private static TenantStatusDto Own(string id, string name, string color) =>
        new() { Id = id, Name = name, Color = color };
}
