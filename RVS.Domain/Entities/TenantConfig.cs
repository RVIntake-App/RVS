using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Tenant-level configuration. Includes access gate settings and the master list of
/// service capabilities available across all locations.
///
/// Cosmos DB partition key: /tenantId
/// </summary>
public class TenantConfig : EntityBase
{
    /// <inheritdoc />
    [JsonProperty("type")]
    public override string Type { get; init; } = "tenantConfig";

    /// <summary>
    /// Tenant access gate — controls whether logins are enabled for this tenant.
    /// </summary>
    [JsonProperty("accessGate")]
    public TenantAccessGateEmbedded AccessGate { get; set; } = new();

    /// <summary>
    /// Master list of service capabilities offered by this tenant.
    /// Individual locations choose which of these they support via
    /// <see cref="Location.EnabledCapabilities"/>.
    /// </summary>
    [JsonProperty("availableCapabilities")]
    public List<TenantCapabilityEmbedded> AvailableCapabilities { get; set; } = [];

    /// <summary>
    /// The tenant's own statuses between <c>New</c> and <c>Completed</c>, in board order
    /// (<c>Spec C-8</c>, issue #855). <c>null</c> means the tenant has never edited them and
    /// reads <see cref="Validation.StatusVocabulary.DefaultIntermediateStatuses"/>. Deleted statuses
    /// stay in the list as <see cref="TenantStatusEmbedded.Retired"/> entries pointing to their
    /// replacement. The fixed <c>New</c>, <c>Completed</c> and <c>Cancelled</c> are never stored here.
    /// </summary>
    [JsonProperty("intermediateStatuses")]
    public List<TenantStatusEmbedded>? IntermediateStatuses { get; set; }
}

// ---------------------------------------------------------------------------
// Embedded: TenantStatusEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// One tenant-managed service-request status (<c>Spec C-8</c>). Its position in
/// <see cref="TenantConfig.IntermediateStatuses"/> is its board order.
/// </summary>
public class TenantStatusEmbedded
{
    /// <summary>
    /// Stable identifier stored on <see cref="ServiceRequest.Status"/>. Never changes on
    /// rename. The defaults keep the values stored before issue #855 (<c>InProgress</c>,
    /// <c>WaitingOnParts</c>, <c>WaitingOnCustomer</c>); new ones are server-generated.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name, shown on the board, in the manager app and to the customer.</summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Badge color key from <see cref="Validation.StatusVocabulary.Colors"/>.</summary>
    [JsonProperty("color")]
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// True once the tenant has deleted the status. A retired status is no longer a column or a
    /// choice, and does not count toward the limit; requests still carrying its id are shown as
    /// <see cref="ReplacedBy"/>, so a delete never rewrites requests.
    /// </summary>
    [JsonProperty("retired")]
    public bool Retired { get; set; }

    /// <summary>For a retired status, the active (or fixed) status that takes its place.</summary>
    [JsonProperty("replacedBy")]
    public string? ReplacedBy { get; set; }
}

// ---------------------------------------------------------------------------
// Embedded: TenantAccessGateEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// Controls tenant-level login access. When disabled, the UI shows a friendly message.
/// </summary>
public class TenantAccessGateEmbedded
{
    /// <summary>
    /// Hard switch the UI can enforce at startup.
    /// </summary>
    [JsonProperty("loginsEnabled")]
    public bool LoginsEnabled { get; set; } = true;

    /// <summary>
    /// Why disabled (if disabled). Keep as string for flexibility.
    /// Examples: "PastDue", "Suspended", "Canceled", "ManualHold", "SecurityLock"
    /// </summary>
    [JsonProperty("disabledReason")]
    public string? DisabledReason { get; set; }

    /// <summary>
    /// Optional user-friendly but non-sensitive message to show on login/splash screen.
    /// </summary>
    [JsonProperty("disabledMessage")]
    public string? DisabledMessage { get; set; }

    /// <summary>
    /// Optional support contact email for display in the UI (e.g., "Pay invoice", "Contact support").
    /// </summary>
    [JsonProperty("supportContactEmail")]
    [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Invalid email address format")]
    public string? SupportContactEmail { get; set; }

    /// <summary>
    /// Optional timestamp when the tenant was disabled. Useful for audit/debugging.
    /// </summary>
    [JsonProperty("disabledAtUtc")]
    public DateTimeOffset? DisabledAtUtc { get; set; }

    /// <summary>
    /// How long a disabled tenant's intake slugs keep capturing, counted from
    /// <see cref="DisabledAtUtc"/> (<c>Spec A-19</c>, issue #478).
    /// </summary>
    public const int IntakeCaptureDays = 60;

    /// <summary>
    /// Whether the tenant's intake slugs have stopped accepting new requests (<c>Spec A-19</c>).
    /// Derived on read rather than stored, so re-enabling the tenant restores intake at once. A
    /// disabled gate with no <see cref="DisabledAtUtc"/> has no clock to run and keeps capturing.
    /// </summary>
    /// <param name="utcNow">The current time.</param>
    public bool IsIntakeExpired(DateTimeOffset utcNow) =>
        !LoginsEnabled
        && DisabledAtUtc is { } disabledAt
        && utcNow >= disabledAt.AddDays(IntakeCaptureDays);
}

// ---------------------------------------------------------------------------
// Embedded: TenantCapabilityEmbedded
// ---------------------------------------------------------------------------

/// <summary>
/// A service capability available at the tenant level (e.g., "Diesel Engine Service").
/// Locations opt in to specific capabilities from this master list.
/// </summary>
public class TenantCapabilityEmbedded
{
    /// <summary>
    /// URL-safe unique code (e.g., "diesel-service").
    /// Used as the reference key in <see cref="Location.EnabledCapabilities"/>.
    /// </summary>
    [JsonProperty("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name (e.g., "Diesel Engine Service").
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description shown to staff when selecting capabilities for a location.
    /// </summary>
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Controls display order in the UI.
    /// </summary>
    [JsonProperty("sortOrder")]
    public int SortOrder { get; set; }

    /// <summary>
    /// When false the capability is soft-deleted and hidden from location selection.
    /// </summary>
    [JsonProperty("isActive")]
    public bool IsActive { get; set; } = true;
}
