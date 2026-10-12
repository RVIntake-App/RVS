using RVS.Blazor.Manager.Shared;
using RVS.Domain.DTOs;
using RVS.UI.Shared.Services;

namespace RVS.Blazor.Manager.State;

/// <summary>
/// The manager app's copy of the tenant's status vocabulary (<c>Spec C-8</c>, issue #855),
/// loaded once and shared by the board, the list, the detail dialog and the request page.
/// Until it loads, or if loading fails, it holds the defaults, so those pages always render.
/// </summary>
public sealed class StatusVocabularyState
{
    private readonly TenantStatusApiClient _client;
    private bool _loaded;

    /// <summary>
    /// Initializes a new instance of <see cref="StatusVocabularyState"/>.
    /// </summary>
    public StatusVocabularyState(TenantStatusApiClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <summary>The tenant's statuses in board order.</summary>
    public IReadOnlyList<TenantStatusDto> Statuses { get; private set; } = StatusVocabularyView.Defaults;

    /// <summary>Whether the signed-in user may edit the statuses.</summary>
    public bool CanEdit { get; private set; }

    /// <summary>Deleted status id → its replacement. Requests are never rewritten on delete.</summary>
    public IReadOnlyDictionary<string, string> Redirects { get; private set; } = new Dictionary<string, string>();

    /// <summary>The status a stored id stands for now: its replacement if it was deleted, else itself.</summary>
    public string Resolve(string statusId) =>
        Redirects.TryGetValue(statusId, out var replacement) ? replacement : statusId;

    /// <summary>The display name for a stored status id, after resolving a deleted one.</summary>
    public string Label(string? statusId) =>
        statusId is null ? string.Empty : StatusVocabularyView.Label(Statuses, Resolve(statusId));

    /// <summary>The badge color for a stored status id, after resolving a deleted one.</summary>
    public string? Color(string? statusId) =>
        statusId is null ? null : StatusVocabularyView.Color(Statuses, Resolve(statusId));

    /// <summary>Raised when <see cref="Statuses"/> changes.</summary>
    public event Action? OnChanged;

    /// <summary>
    /// Loads the vocabulary unless it already has. A failure keeps the defaults and is retried
    /// on the next call; it never throws, since every caller can render with the defaults.
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return;
        }

        try
        {
            Apply(await _client.GetStatusesAsync(cancellationToken));
            _loaded = true;
        }
        catch (TenantStatusApiException)
        {
            // Keep the defaults; try again next time.
        }
        catch (HttpRequestException)
        {
            // Offline or the API is unreachable; keep the defaults.
        }
    }

    /// <summary>Saves an edit and adopts the vocabulary the API returns.</summary>
    /// <exception cref="TenantStatusApiException">The API refused the edit; nothing changes.</exception>
    public async Task SaveAsync(TenantStatusesUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        Apply(await _client.UpdateStatusesAsync(request, cancellationToken));
        _loaded = true;
    }

    private void Apply(TenantStatusesResponseDto response)
    {
        Statuses = response.Statuses;
        CanEdit = response.CanEdit;
        Redirects = response.Redirects;
        OnChanged?.Invoke();
    }
}
