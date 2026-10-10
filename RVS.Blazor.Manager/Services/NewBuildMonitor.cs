using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace RVS.Blazor.Manager.Services;

/// <summary>
/// Notices that a newer Manager build has been deployed while this tab has been open (issue #859).
/// Manager is an all-shift console: a tab or installed window left open never reloads on its own,
/// so it would keep running the build it opened with. The deploy writes the build's git SHA to
/// <c>version.json</c>; this compares it with the SHA compiled into the running assembly.
/// </summary>
/// <remarks>
/// It checks when the tab becomes visible again and on each <see cref="CheckInterval"/> tick while
/// visible (the <c>NewBuildPrompt</c> component drives both). Network and parse failures are
/// silent: the next check simply tries again. Once a new build is found it stops checking.
/// </remarks>
public sealed class NewBuildMonitor
{
    /// <summary>How often a visible tab checks for a new build.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    private const string VersionPath = "version.json";

    private readonly HttpClient _http;
    private readonly string? _currentBuildId;
    private bool _visible = true;
    private bool _checking;

    /// <param name="http">A client whose base address is the Manager app's own origin.</param>
    /// <param name="currentBuildId">The running build's ID, or null when unknown (checking is then off).</param>
    public NewBuildMonitor(HttpClient http, string? currentBuildId)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _currentBuildId = string.IsNullOrWhiteSpace(currentBuildId) ? null : currentBuildId.Trim();
    }

    /// <summary>Raised once, when a build other than the running one is found.</summary>
    public event Action? NewBuildAvailable;

    /// <summary>False when the running build's ID is unknown, so there is nothing to compare.</summary>
    public bool IsEnabled => _currentBuildId is not null;

    /// <summary>True once a deployed build differs from the running one.</summary>
    public bool IsNewBuildAvailable { get; private set; }

    /// <summary>
    /// The source revision from an <c>AssemblyInformationalVersion</c> such as <c>1.0.0+&lt;sha&gt;</c>,
    /// or null when it carries none.
    /// </summary>
    public static string? BuildIdFromInformationalVersion(string? informationalVersion)
    {
        var plus = informationalVersion?.IndexOf('+') ?? -1;
        if (plus < 0) return null;

        var id = informationalVersion![(plus + 1)..].Trim();
        return id.Length == 0 ? null : id;
    }

    /// <summary>Fetches <c>version.json</c> and compares it with the running build.</summary>
    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || IsNewBuildAvailable || _checking) return;

        _checking = true;
        try
        {
            var deployed = await FetchDeployedBuildIdAsync(cancellationToken);
            if (deployed is null || string.Equals(deployed, _currentBuildId, StringComparison.OrdinalIgnoreCase)) return;

            IsNewBuildAvailable = true;
            NewBuildAvailable?.Invoke();
        }
        finally
        {
            _checking = false;
        }
    }

    /// <summary>Records tab visibility; becoming visible checks straight away.</summary>
    public Task OnVisibilityChangedAsync(bool visible, CancellationToken cancellationToken = default)
    {
        _visible = visible;
        return visible ? CheckAsync(cancellationToken) : Task.CompletedTask;
    }

    /// <summary>A periodic tick: checks only while the tab is visible.</summary>
    public Task OnTimerTickAsync(CancellationToken cancellationToken = default) =>
        _visible ? CheckAsync(cancellationToken) : Task.CompletedTask;

    private async Task<string?> FetchDeployedBuildIdAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, VersionPath);
            // Revalidate with the server rather than trust the browser cache (the SWA also serves it no-cache).
            request.SetBrowserRequestCache(BrowserRequestCache.NoCache);

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadFromJsonAsync<VersionFile>(cancellationToken);
            return string.IsNullOrWhiteSpace(body?.BuildId) ? null : body.BuildId.Trim();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException
                                       || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Offline, mid-deploy, or not a version file: try again on the next check.
            return null;
        }
    }

    private sealed record VersionFile([property: JsonPropertyName("buildId")] string? BuildId);
}
