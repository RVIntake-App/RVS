using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

namespace RVS.API.Services;

/// <summary>
/// Reads and edits a tenant's service-request status vocabulary (<c>Spec C-8</c>, issue #855).
/// The vocabulary lives on <see cref="TenantConfig.IntermediateStatuses"/>; the fixed
/// <c>New</c>, <c>Completed</c> and <c>Cancelled</c> are never stored or editable.
///
/// Every edit is a single write to the tenant config. Deleting a status retires it with a
/// pointer to its replacement instead of rewriting the requests that carry it, so a delete
/// costs the same however many requests the tenant has, and cannot half-finish.
/// </summary>
public sealed class TenantStatusService : ITenantStatusService
{
    private readonly ITenantConfigRepository _configRepository;
    private readonly IUserContextAccessor _userContext;

    /// <summary>
    /// Initializes a new instance of <see cref="TenantStatusService"/>.
    /// </summary>
    public TenantStatusService(ITenantConfigRepository configRepository, IUserContextAccessor userContext)
    {
        _configRepository = configRepository;
        _userContext = userContext;
    }

    /// <inheritdoc />
    public async Task<TenantStatusVocabulary> GetStatusesAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var config = await _configRepository.GetAsync(tenantId, cancellationToken);
        return StatusVocabulary.Build(config?.IntermediateStatuses);
    }

    /// <inheritdoc />
    public async Task<TenantStatusVocabulary> UpdateStatusesAsync(
        string tenantId,
        TenantStatusesUpdateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        var config = await _configRepository.GetAsync(tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant config not found for tenant '{tenantId}'.");

        var stored = config.IntermediateStatuses ?? StatusVocabulary.DefaultIntermediateStatuses();
        var currentActive = stored.Where(s => !s.Retired).ToList();
        var alreadyRetired = stored.Where(s => s.Retired).ToList();
        var currentActiveIds = currentActive.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);

        var proposed = request.IntermediateStatuses.Select(item => ToEmbedded(item, currentActiveIds)).ToList();
        var proposedIds = proposed.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var activeIds = StatusVocabulary.FixedStatusIds.Concat(proposedIds).ToHashSet(StringComparer.Ordinal);

        // A deleted status is retired with a pointer to the status the manager chose for it.
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        var newlyRetired = new List<TenantStatusEmbedded>();
        foreach (var removed in currentActive.Where(s => !proposedIds.Contains(s.Id)))
        {
            var replacement = ResolveReplacement(removed.Id, request.Reassignments, activeIds);
            replacements[removed.Id] = replacement;
            newlyRetired.Add(new TenantStatusEmbedded
            {
                Id = removed.Id,
                Name = removed.Name,
                Color = removed.Color,
                Retired = true,
                ReplacedBy = replacement,
            });
        }

        // Keep every pointer one hop from an active status.
        foreach (var retired in alreadyRetired)
        {
            if (retired.ReplacedBy is { } target && replacements.TryGetValue(target, out var newTarget))
            {
                retired.ReplacedBy = newTarget;
            }
        }

        var all = proposed.Concat(alreadyRetired).Concat(newlyRetired).ToList();

        var validation = StatusVocabulary.Validate(all);
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.ErrorMessage);
        }

        config.IntermediateStatuses = all;
        config.MarkAsUpdated(_userContext.UserId);
        await _configRepository.SaveAsync(config, cancellationToken);

        return StatusVocabulary.Build(all);
    }

    private static TenantStatusEmbedded ToEmbedded(TenantStatusUpdateItemDto item, HashSet<string> currentActiveIds)
    {
        ArgumentNullException.ThrowIfNull(item);

        string id;
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            id = $"st_{Guid.NewGuid():N}"[..15];
        }
        else if (StatusVocabulary.IsFixed(item.Id))
        {
            throw new ArgumentException($"'{item.Id}' is a fixed status and cannot be changed.");
        }
        else if (!currentActiveIds.Contains(item.Id))
        {
            throw new ArgumentException($"Unknown status id '{item.Id}'.");
        }
        else
        {
            id = item.Id;
        }

        return new TenantStatusEmbedded
        {
            Id = id,
            Name = item.Name?.Trim() ?? string.Empty,
            Color = item.Color ?? string.Empty,
        };
    }

    private static string ResolveReplacement(
        string removedId,
        Dictionary<string, string>? reassignments,
        HashSet<string> activeIds)
    {
        if (reassignments is null
            || !reassignments.TryGetValue(removedId, out var target)
            || string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException($"Choose a status to replace '{removedId}'.");
        }

        if (!activeIds.Contains(target))
        {
            throw new ArgumentException($"Cannot replace '{removedId}' with '{target}': no such status.");
        }

        return target;
    }
}
