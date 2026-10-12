using RVS.Domain.DTOs;
using RVS.Domain.Validation;

namespace RVS.Domain.Interfaces;

/// <summary>
/// Reads and edits a tenant's service-request status vocabulary (<c>Spec C-8</c>, issue #855).
/// </summary>
public interface ITenantStatusService
{
    /// <summary>
    /// Returns the tenant's vocabulary: the active statuses in board order and the redirects
    /// from deleted statuses to their replacements. A tenant with no stored configuration reads
    /// the defaults.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TenantStatusVocabulary> GetStatusesAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the tenant's statuses between <c>New</c> and <c>Completed</c>. A status left out
    /// is retired, pointing to the status named for it in
    /// <see cref="TenantStatusesUpdateRequestDto.Reassignments"/>; no request is rewritten.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="request">The new list and a replacement for each deleted status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant's vocabulary after the change.</returns>
    /// <exception cref="ArgumentException">The list is invalid, names an unknown, retired or fixed id, or a deleted status has no valid replacement.</exception>
    /// <exception cref="KeyNotFoundException">The tenant config is not found.</exception>
    Task<TenantStatusVocabulary> UpdateStatusesAsync(string tenantId, TenantStatusesUpdateRequestDto request, CancellationToken cancellationToken = default);
}
