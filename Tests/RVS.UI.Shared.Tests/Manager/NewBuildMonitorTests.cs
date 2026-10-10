using System.Net;
using System.Text;
using FluentAssertions;
using RVS.Blazor.Manager.Services;

namespace RVS.UI.Shared.Tests.Manager;

/// <summary>
/// Tests for <see cref="NewBuildMonitor"/> — notices that a newer Manager build has been deployed
/// while a tab sits open all shift, so the app can offer a reload (issue #859).
/// </summary>
public class NewBuildMonitorTests
{
    private const string Running = "a1b2c3d";
    private const string Deployed = "e4f5a6b";

    private static string VersionJson(string buildId) => $$"""{"buildId":"{{buildId}}"}""";

    private static (NewBuildMonitor Monitor, StubHandler Handler) Create(
        string? currentBuildId, HttpStatusCode status = HttpStatusCode.OK, string? body = null)
    {
        var handler = new StubHandler(status, body ?? VersionJson(Running));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://manager.example/") };
        return (new NewBuildMonitor(http, currentBuildId), handler);
    }

    // ---- BuildIdFromInformationalVersion ----

    [Fact]
    public void BuildIdFromInformationalVersion_WhenSourceRevisionPresent_ShouldReturnIt()
    {
        NewBuildMonitor.BuildIdFromInformationalVersion("1.0.0+" + Running).Should().Be(Running);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0.0")]
    [InlineData("1.0.0+")]
    [InlineData("1.0.0+  ")]
    public void BuildIdFromInformationalVersion_WhenNoSourceRevision_ShouldReturnNull(string? version)
    {
        NewBuildMonitor.BuildIdFromInformationalVersion(version).Should().BeNull();
    }

    // ---- Constructor ----

    [Fact]
    public void Constructor_WhenHttpClientIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => new NewBuildMonitor(null!, Running);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void IsEnabled_WhenOwnBuildIdUnknown_ShouldBeFalse(string? currentBuildId)
    {
        var (monitor, _) = Create(currentBuildId);

        monitor.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void CheckInterval_ShouldBeFifteenMinutes()
    {
        NewBuildMonitor.CheckInterval.Should().Be(TimeSpan.FromMinutes(15));
    }

    // ---- CheckAsync: comparison ----

    [Fact]
    public async Task CheckAsync_WhenDeployedBuildDiffers_ShouldFlagNewBuildAndRaiseEvent()
    {
        var (monitor, _) = Create(Running, body: VersionJson(Deployed));
        var raised = 0;
        monitor.NewBuildAvailable += () => raised++;

        await monitor.CheckAsync();

        monitor.IsNewBuildAvailable.Should().BeTrue();
        raised.Should().Be(1);
    }

    [Fact]
    public async Task CheckAsync_WhenDeployedBuildMatches_ShouldNotFlag()
    {
        var (monitor, _) = Create(Running, body: VersionJson(Running));
        var raised = 0;
        monitor.NewBuildAvailable += () => raised++;

        await monitor.CheckAsync();

        monitor.IsNewBuildAvailable.Should().BeFalse();
        raised.Should().Be(0);
    }

    [Fact]
    public async Task CheckAsync_WhenIdsDifferOnlyInCase_ShouldNotFlag()
    {
        var (monitor, _) = Create(Running, body: VersionJson(Running.ToUpperInvariant()));

        await monitor.CheckAsync();

        monitor.IsNewBuildAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_ShouldRequestVersionJsonFromTheAppOrigin()
    {
        var (monitor, handler) = Create(Running);

        await monitor.CheckAsync();

        handler.Requests.Should().ContainSingle()
            .Which.Should().Be(new Uri("https://manager.example/version.json"));
    }

    [Fact]
    public async Task CheckAsync_WhenOwnBuildIdUnknown_ShouldNotFetch()
    {
        var (monitor, handler) = Create(null, body: VersionJson(Deployed));

        await monitor.CheckAsync();

        handler.Requests.Should().BeEmpty();
        monitor.IsNewBuildAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_AfterNewBuildFound_ShouldNotFetchOrRaiseAgain()
    {
        var (monitor, handler) = Create(Running, body: VersionJson(Deployed));
        var raised = 0;
        monitor.NewBuildAvailable += () => raised++;

        await monitor.CheckAsync();
        await monitor.CheckAsync();

        handler.Requests.Should().ContainSingle();
        raised.Should().Be(1);
    }

    // ---- CheckAsync: failures are silent ----

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task CheckAsync_WhenServerErrors_ShouldStaySilentAndUnflagged(HttpStatusCode status)
    {
        var (monitor, _) = Create(Running, status, VersionJson(Deployed));

        var act = () => monitor.CheckAsync();

        await act.Should().NotThrowAsync();
        monitor.IsNewBuildAvailable.Should().BeFalse();
    }

    [Theory]
    [InlineData("<!DOCTYPE html><html></html>")]
    [InlineData("{}")]
    [InlineData("""{"buildId":""}""")]
    [InlineData("""{"buildId":"   "}""")]
    [InlineData("")]
    public async Task CheckAsync_WhenBodyUnusable_ShouldStaySilentAndUnflagged(string body)
    {
        var (monitor, _) = Create(Running, body: body);

        var act = () => monitor.CheckAsync();

        await act.Should().NotThrowAsync();
        monitor.IsNewBuildAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_WhenNetworkFails_ShouldStaySilentAndTryAgainNextTime()
    {
        var handler = new StubHandler(HttpStatusCode.OK, VersionJson(Deployed)) { FailNext = true };
        var monitor = new NewBuildMonitor(
            new HttpClient(handler) { BaseAddress = new Uri("https://manager.example/") }, Running);

        var act = () => monitor.CheckAsync();
        await act.Should().NotThrowAsync();
        monitor.IsNewBuildAvailable.Should().BeFalse();

        await monitor.CheckAsync();
        monitor.IsNewBuildAvailable.Should().BeTrue();
    }

    // ---- Scheduling: visibility and timer ticks ----

    [Fact]
    public async Task OnVisibilityChangedAsync_WhenBecomesVisible_ShouldCheckImmediately()
    {
        var (monitor, handler) = Create(Running, body: VersionJson(Deployed));
        await monitor.OnVisibilityChangedAsync(false);

        await monitor.OnVisibilityChangedAsync(true);

        handler.Requests.Should().ContainSingle();
        monitor.IsNewBuildAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task OnVisibilityChangedAsync_WhenBecomesHidden_ShouldNotCheck()
    {
        var (monitor, handler) = Create(Running);

        await monitor.OnVisibilityChangedAsync(false);

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task OnTimerTickAsync_WhenVisible_ShouldCheck()
    {
        var (monitor, handler) = Create(Running, body: VersionJson(Deployed));

        await monitor.OnTimerTickAsync();

        handler.Requests.Should().ContainSingle();
        monitor.IsNewBuildAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task OnTimerTickAsync_WhenHidden_ShouldNotCheck()
    {
        var (monitor, handler) = Create(Running, body: VersionJson(Deployed));
        await monitor.OnVisibilityChangedAsync(false);

        await monitor.OnTimerTickAsync();

        handler.Requests.Should().BeEmpty();
        monitor.IsNewBuildAvailable.Should().BeFalse();
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        public bool FailNext { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            if (FailNext)
            {
                FailNext = false;
                throw new HttpRequestException("network down");
            }

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
