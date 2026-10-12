using FluentAssertions;
using Moq;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

namespace RVS.API.Tests.Services;

public class TenantStatusServiceTests
{
    private readonly Mock<ITenantConfigRepository> _configRepoMock = new();
    private readonly Mock<IUserContextAccessor> _userContextMock = new();
    private readonly TenantStatusService _sut;

    public TenantStatusServiceTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("usr_test");
        _sut = new TenantStatusService(_configRepoMock.Object, _userContextMock.Object);
    }

    // ── GetStatusesAsync ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetStatusesAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.GetStatusesAsync(tenantId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetStatusesAsync_WhenNoTenantConfig_ShouldReturnTheDefaults()
    {
        _configRepoMock.Setup(r => r.GetAsync("ten_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantConfig?)null);

        var result = await _sut.GetStatusesAsync("ten_1");

        result.Statuses.Select(s => s.Id).Should().Equal(
            "New", "InProgress", "WaitingOnParts", "WaitingOnCustomer", "Completed", "Cancelled");
        result.Redirects.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStatusesAsync_WhenTenantHasStatuses_ShouldReturnThemBetweenTheFixedOnes()
    {
        SetupConfig(Status("st_diag", "Diagnosing"));

        var result = await _sut.GetStatusesAsync("ten_1");

        result.Statuses.Select(s => s.Id).Should().Equal("New", "st_diag", "Completed", "Cancelled");
    }

    [Fact]
    public async Task GetStatusesAsync_ShouldCarryRetiredStatusRedirects()
    {
        SetupConfig(Status("st_diag", "Diagnosing"), Retired("st_old", "st_diag"));

        var result = await _sut.GetStatusesAsync("ten_1");

        result.Redirects.Should().ContainKey("st_old").WhoseValue.Should().Be("st_diag");
    }

    // ── UpdateStatusesAsync: guards ──────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateStatusesAsync_WhenTenantIdIsNullOrWhiteSpace_ShouldThrowArgumentException(string? tenantId)
    {
        var act = () => _sut.UpdateStatusesAsync(tenantId!, new TenantStatusesUpdateRequestDto());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateStatusesAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.UpdateStatusesAsync("ten_1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateStatusesAsync_WhenTenantConfigNotFound_ShouldThrowKeyNotFoundException()
    {
        _configRepoMock.Setup(r => r.GetAsync("ten_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantConfig?)null);

        var act = () => _sut.UpdateStatusesAsync("ten_1", new TenantStatusesUpdateRequestDto());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── UpdateStatusesAsync: editing ─────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusesAsync_RenameAndRecolor_ShouldKeepTheIdAndSave()
    {
        var config = SetupConfig();

        var result = await _sut.UpdateStatusesAsync("ten_1", Request(
            Item("InProgress", "Repairing", "teal"),
            Item("WaitingOnParts", "Waiting on Parts", "amber"),
            Item("WaitingOnCustomer", "Waiting on Customer", "teal")));

        result.Statuses[1].Should().Be(new StatusDefinition("InProgress", "Repairing", "teal", false));
        config.IntermediateStatuses!.Select(s => s.Name).Should().Equal("Repairing", "Waiting on Parts", "Waiting on Customer");
        config.UpdatedByUserId.Should().Be("usr_test");
        _configRepoMock.Verify(r => r.SaveAsync(config, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusesAsync_ShouldTrimNames()
    {
        var config = SetupConfig(Status("st_1", "One"));

        await _sut.UpdateStatusesAsync("ten_1", Request(Item("st_1", "  Repairing  ", "blue")));

        config.IntermediateStatuses!.Single().Name.Should().Be("Repairing");
    }

    [Fact]
    public async Task UpdateStatusesAsync_Reorder_ShouldStoreTheNewOrder()
    {
        var config = SetupConfig();

        await _sut.UpdateStatusesAsync("ten_1", Request(
            Item("WaitingOnCustomer", "Waiting on Customer", "teal"),
            Item("InProgress", "In Progress", "blue"),
            Item("WaitingOnParts", "Waiting on Parts", "amber")));

        config.IntermediateStatuses!.Select(s => s.Id).Should().Equal("WaitingOnCustomer", "InProgress", "WaitingOnParts");
    }

    [Fact]
    public async Task UpdateStatusesAsync_AddWithoutId_ShouldGenerateAnId()
    {
        var config = SetupConfig(Status("st_1", "One"));

        var result = await _sut.UpdateStatusesAsync("ten_1", Request(
            Item("st_1", "One", "blue"),
            Item(null, "Road Test", "green")));

        var added = config.IntermediateStatuses!.Last();
        added.Id.Should().StartWith("st_");
        added.Name.Should().Be("Road Test");
        result.Statuses.Select(s => s.Id).Should().Contain(added.Id);
    }

    [Fact]
    public async Task UpdateStatusesAsync_AddTwo_ShouldGenerateDistinctIds()
    {
        var config = SetupConfig(Status("st_1", "One"));

        await _sut.UpdateStatusesAsync("ten_1", Request(
            Item("st_1", "One", "blue"), Item(null, "Two", "blue"), Item(null, "Three", "blue")));

        config.IntermediateStatuses!.Select(s => s.Id).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("New")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task UpdateStatusesAsync_WhenAFixedStatusIsSubmitted_ShouldThrowArgumentException(string fixedId)
    {
        SetupConfig();

        var act = () => _sut.UpdateStatusesAsync("ten_1", Request(Item(fixedId, "Renamed", "blue")));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*fixed*");
        _configRepoMock.Verify(r => r.SaveAsync(It.IsAny<TenantConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusesAsync_WhenAnIdIsUnknown_ShouldThrowArgumentException()
    {
        SetupConfig();

        var act = () => _sut.UpdateStatusesAsync("ten_1", Request(Item("st_made_up", "Made up", "blue")));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*st_made_up*");
    }

    [Fact]
    public async Task UpdateStatusesAsync_WhenARetiredIdIsSubmitted_ShouldThrowArgumentException()
    {
        SetupConfig(Status("st_1", "One"), Retired("st_old", "st_1"));

        var act = () => _sut.UpdateStatusesAsync("ten_1", Request(Item("st_1", "One", "blue"), Item("st_old", "Back", "blue")));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*st_old*");
    }

    [Fact]
    public async Task UpdateStatusesAsync_WhenTheListIsInvalid_ShouldThrowArgumentExceptionAndNotSave()
    {
        SetupConfig(Status("st_1", "One"));

        var act = () => _sut.UpdateStatusesAsync("ten_1", Request(Item("st_1", "Bay <3>", "blue")));

        await act.Should().ThrowAsync<ArgumentException>();
        _configRepoMock.Verify(r => r.SaveAsync(It.IsAny<TenantConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── UpdateStatusesAsync: deleting retires, never rewrites requests ───────

    [Fact]
    public async Task UpdateStatusesAsync_Delete_ShouldRetireTheStatusWithAPointerToItsReplacement()
    {
        var config = SetupConfig(Status("st_1", "One"), Status("st_2", "Two"));

        var result = await _sut.UpdateStatusesAsync("ten_1", Request(
            [Item("st_2", "Two", "blue")],
            new Dictionary<string, string> { ["st_1"] = "st_2" }));

        var retired = config.IntermediateStatuses!.Single(s => s.Id == "st_1");
        retired.Retired.Should().BeTrue();
        retired.ReplacedBy.Should().Be("st_2");
        result.Statuses.Select(s => s.Id).Should().Equal("New", "st_2", "Completed", "Cancelled");
        result.Redirects["st_1"].Should().Be("st_2");
    }

    [Fact]
    public async Task UpdateStatusesAsync_DeleteADefault_ShouldRetireItToo()
    {
        var config = SetupConfig();

        await _sut.UpdateStatusesAsync("ten_1", Request(
            [Item("InProgress", "In Progress", "blue"), Item("WaitingOnCustomer", "Waiting on Customer", "teal")],
            new Dictionary<string, string> { ["WaitingOnParts"] = "InProgress" }));

        config.IntermediateStatuses!.Single(s => s.Id == "WaitingOnParts").ReplacedBy.Should().Be("InProgress");
    }

    [Theory]
    [InlineData("New")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task UpdateStatusesAsync_Delete_ReplacementMayBeAFixedStatus(string replacement)
    {
        var config = SetupConfig(Status("st_1", "One"));

        await _sut.UpdateStatusesAsync("ten_1", Request([], new Dictionary<string, string> { ["st_1"] = replacement }));

        config.IntermediateStatuses!.Single().ReplacedBy.Should().Be(replacement);
    }

    [Fact]
    public async Task UpdateStatusesAsync_DeleteWithoutAReplacement_ShouldThrowAndNotSave()
    {
        SetupConfig(Status("st_1", "One"), Status("st_2", "Two"));

        var act = () => _sut.UpdateStatusesAsync("ten_1", Request(Item("st_2", "Two", "blue")));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*st_1*");
        _configRepoMock.Verify(r => r.SaveAsync(It.IsAny<TenantConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("st_1")]
    [InlineData("st_gone")]
    public async Task UpdateStatusesAsync_DeleteWithAReplacementThatIsNotActive_ShouldThrow(string replacement)
    {
        SetupConfig(Status("st_1", "One"), Status("st_2", "Two"));

        var act = () => _sut.UpdateStatusesAsync("ten_1", Request(
            [Item("st_2", "Two", "blue")],
            new Dictionary<string, string> { ["st_1"] = replacement }));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateStatusesAsync_DeletingAReplacement_ShouldReAimOlderPointersToTheNewOne()
    {
        // st_old already points at st_1; deleting st_1 into st_2 must leave st_old one hop from st_2.
        var config = SetupConfig(Status("st_1", "One"), Status("st_2", "Two"), Retired("st_old", "st_1"));

        await _sut.UpdateStatusesAsync("ten_1", Request(
            [Item("st_2", "Two", "blue")],
            new Dictionary<string, string> { ["st_1"] = "st_2" }));

        config.IntermediateStatuses!.Single(s => s.Id == "st_old").ReplacedBy.Should().Be("st_2");
    }

    [Fact]
    public async Task UpdateStatusesAsync_ShouldKeepExistingRetiredStatuses()
    {
        var config = SetupConfig(Status("st_1", "One"), Retired("st_old", "st_1"));

        await _sut.UpdateStatusesAsync("ten_1", Request(Item("st_1", "Renamed", "blue")));

        config.IntermediateStatuses!.Should().Contain(s => s.Id == "st_old" && s.Retired);
    }

    [Fact]
    public async Task UpdateStatusesAsync_RetiredStatusesDoNotCountTowardTheLimit()
    {
        var retired = Enumerable.Range(1, 5).Select(i => Retired($"st_old{i}", "New")).ToArray();
        SetupConfig(retired);

        var items = Enumerable.Range(1, StatusVocabulary.MaxIntermediateStatuses)
            .Select(i => Item(null, $"Step {i}", "blue")).ToArray();

        var result = await _sut.UpdateStatusesAsync("ten_1", Request(items));

        result.Statuses.Should().HaveCount(StatusVocabulary.MaxStatuses);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private TenantConfig SetupConfig(params TenantStatusEmbedded[] statuses)
    {
        var config = new TenantConfig
        {
            Id = "ten_1",
            TenantId = "ten_1",
            IntermediateStatuses = statuses.Length == 0 ? null : [.. statuses],
        };
        _configRepoMock.Setup(r => r.GetAsync("ten_1", It.IsAny<CancellationToken>())).ReturnsAsync(config);
        return config;
    }

    private static TenantStatusEmbedded Status(string id, string name, string color = "blue") =>
        new() { Id = id, Name = name, Color = color };

    private static TenantStatusEmbedded Retired(string id, string replacedBy) =>
        new() { Id = id, Name = id, Color = "blue", Retired = true, ReplacedBy = replacedBy };

    private static TenantStatusUpdateItemDto Item(string? id, string name, string color) =>
        new() { Id = id, Name = name, Color = color };

    private static TenantStatusesUpdateRequestDto Request(params TenantStatusUpdateItemDto[] items) =>
        new() { IntermediateStatuses = [.. items] };

    private static TenantStatusesUpdateRequestDto Request(
        TenantStatusUpdateItemDto[] items, Dictionary<string, string> reassignments) =>
        new() { IntermediateStatuses = [.. items], Reassignments = reassignments };
}
