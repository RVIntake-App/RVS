namespace RVS.Domain.DTOs;

/// <summary>
/// One status in a tenant's vocabulary (<c>Spec C-8</c>), as the manager app reads it.
/// </summary>
public sealed record TenantStatusDto
{
    /// <summary>The value stored on a service request's <c>status</c>.</summary>
    public string Id { get; init; } = default!;

    /// <summary>Display name.</summary>
    public string Name { get; init; } = default!;

    /// <summary>Badge color key (<c>blue</c>, <c>amber</c>, <c>teal</c>, <c>green</c>, <c>purple</c>, <c>gray</c>).</summary>
    public string Color { get; init; } = default!;

    /// <summary>True for <c>New</c>, <c>Completed</c> and <c>Cancelled</c>, which cannot be changed.</summary>
    public bool IsFixed { get; init; }
}

/// <summary>
/// A tenant's full status vocabulary in board order: <c>New</c>, the tenant's statuses,
/// <c>Completed</c>, <c>Cancelled</c>.
/// </summary>
public sealed record TenantStatusesResponseDto
{
    /// <summary>The statuses, in board order.</summary>
    public List<TenantStatusDto> Statuses { get; init; } = [];

    /// <summary>
    /// Deleted status id → the status that replaced it. A request still carrying a deleted id is
    /// shown, and moved, as its replacement; requests are never rewritten when a status is deleted.
    /// </summary>
    public Dictionary<string, string> Redirects { get; init; } = [];

    /// <summary>
    /// Whether the signed-in user may edit the statuses. The manager app shows the board's
    /// status-editing controls only when this is true.
    /// </summary>
    public bool CanEdit { get; init; }
}

/// <summary>
/// One tenant-managed status in a <see cref="TenantStatusesUpdateRequestDto"/>.
/// </summary>
public sealed record TenantStatusUpdateItemDto
{
    /// <summary>The id of an existing status, or null to add a new one.</summary>
    public string? Id { get; init; }

    /// <summary>Display name, 1–30 characters.</summary>
    public string Name { get; init; } = default!;

    /// <summary>Badge color key.</summary>
    public string Color { get; init; } = default!;
}

/// <summary>
/// Replaces a tenant's statuses between <c>New</c> and <c>Completed</c> (<c>Spec C-8</c>).
/// The list is the whole set, in order: an existing status left out is deleted, and an item
/// without an id is added.
/// </summary>
public sealed record TenantStatusesUpdateRequestDto
{
    /// <summary>The tenant's statuses between <c>New</c> and <c>Completed</c>, in board order.</summary>
    public List<TenantStatusUpdateItemDto> IntermediateStatuses { get; init; } = [];

    /// <summary>
    /// For each deleted status, the id of the active or fixed status that replaces it. Required
    /// for every status the list leaves out. Requests in a deleted status are not rewritten;
    /// they read as the replacement.
    /// </summary>
    public Dictionary<string, string>? Reassignments { get; init; }
}
