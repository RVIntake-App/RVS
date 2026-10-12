using RVS.Domain.Entities;

namespace RVS.Domain.Validation;

/// <summary>
/// One status in a tenant's resolved vocabulary (<c>Spec C-8</c>).
/// </summary>
/// <param name="Id">The value stored on <see cref="ServiceRequest.Status"/>.</param>
/// <param name="Name">Display name.</param>
/// <param name="Color">Badge color key from <see cref="StatusVocabulary.Colors"/>.</param>
/// <param name="IsFixed">True for <c>New</c>, <c>Completed</c> and <c>Cancelled</c>, which no tenant can change.</param>
public sealed record StatusDefinition(string Id, string Name, string Color, bool IsFixed);

/// <summary>
/// A tenant's resolved vocabulary (<c>Spec C-8</c>): the active statuses in board order, plus a
/// map from each retired (deleted) status id to the active status that replaced it. Requests are
/// never rewritten when a status is deleted, so every reader resolves a stored id through
/// <see cref="ResolveId"/> before using it.
/// </summary>
/// <param name="Statuses">The active statuses in board order.</param>
/// <param name="Redirects">Retired status id → its active replacement.</param>
public sealed record TenantStatusVocabulary(
    IReadOnlyList<StatusDefinition> Statuses,
    IReadOnlyDictionary<string, string> Redirects)
{
    /// <summary>The status a stored id stands for now: its replacement if retired, else itself.</summary>
    public string ResolveId(string statusId) =>
        Redirects.TryGetValue(statusId, out var replacement) ? replacement : statusId;

    /// <summary>The definition a stored id stands for now, or null when it is unknown.</summary>
    public StatusDefinition? Find(string statusId)
    {
        var id = ResolveId(statusId);
        return Statuses.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
    }
}

/// <summary>
/// A tenant's service-request status vocabulary (<c>Spec C-8</c>, issue #855). <c>New</c>,
/// <c>Completed</c> and <c>Cancelled</c> are fixed for every tenant, so intake, disposition and
/// the board's Done window keep keying off them. Every status between <c>New</c> and
/// <c>Completed</c> belongs to the tenant, stored on <see cref="TenantConfig.IntermediateStatuses"/>.
/// </summary>
public static class StatusVocabulary
{
    /// <summary>The status every new intake request gets.</summary>
    public const string New = "New";

    /// <summary>Work finished.</summary>
    public const string Completed = "Completed";

    /// <summary>Stopped, or closed without work by disposition (<c>Spec C-4</c>).</summary>
    public const string Cancelled = "Cancelled";

    /// <summary>Maximum statuses per tenant, the fixed three included.</summary>
    public const int MaxStatuses = 12;

    /// <summary>Maximum length of a status name, after trimming.</summary>
    public const int MaxNameLength = 30;

    /// <summary>The fixed statuses, which no tenant can rename, recolor, reorder or delete.</summary>
    public static IReadOnlyList<string> FixedStatusIds { get; } = [New, Completed, Cancelled];

    /// <summary>Maximum tenant-managed statuses: <see cref="MaxStatuses"/> less the fixed three.</summary>
    public static int MaxIntermediateStatuses => MaxStatuses - FixedStatusIds.Count;

    /// <summary>
    /// The badge color keys a status may use. Each maps to an existing WCAG-audited status
    /// token pair in <c>design-tokens.css</c> (THEME-1), so no new color pairing is introduced.
    /// </summary>
    public static IReadOnlyList<string> Colors { get; } = ["blue", "amber", "teal", "green", "purple", "gray"];

    private static readonly StatusDefinition NewDefinition = new(New, "New", "green", IsFixed: true);
    private static readonly StatusDefinition CompletedDefinition = new(Completed, "Completed", "purple", IsFixed: true);
    private static readonly StatusDefinition CancelledDefinition = new(Cancelled, "Cancelled", "gray", IsFixed: true);

    private static readonly HashSet<string> FixedIds = new(FixedStatusIds, StringComparer.Ordinal);
    private static readonly HashSet<string> ColorSet = new(Colors, StringComparer.Ordinal);
    private static readonly HashSet<char> BlockedCharacters = ['<', '>'];

    /// <summary>
    /// The statuses a tenant starts with between <c>New</c> and <c>Completed</c>, using the ids
    /// stored before issue #855 so existing requests need no migration. A fresh list each call.
    /// </summary>
    public static List<TenantStatusEmbedded> DefaultIntermediateStatuses() =>
    [
        new() { Id = "InProgress", Name = "In Progress", Color = "blue" },
        new() { Id = "WaitingOnParts", Name = "Waiting on Parts", Color = "amber" },
        new() { Id = "WaitingOnCustomer", Name = "Waiting on Customer", Color = "teal" },
    ];

    /// <summary>Returns true when <paramref name="statusId"/> is one of the fixed three.</summary>
    public static bool IsFixed(string statusId) => FixedIds.Contains(statusId);

    /// <summary>
    /// The tenant's active vocabulary in board order: <c>New</c>, the tenant's statuses, then
    /// <c>Completed</c> and <c>Cancelled</c>. Retired statuses are left out.
    /// </summary>
    /// <param name="intermediateStatuses">The stored tenant statuses, or null for the defaults.</param>
    public static IReadOnlyList<StatusDefinition> Resolve(IReadOnlyList<TenantStatusEmbedded>? intermediateStatuses)
    {
        var intermediates = (intermediateStatuses ?? DefaultIntermediateStatuses()).Where(s => !s.Retired).ToList();

        var result = new List<StatusDefinition>(intermediates.Count + FixedStatusIds.Count) { NewDefinition };
        result.AddRange(intermediates.Select(s => new StatusDefinition(s.Id, s.Name, s.Color, IsFixed: false)));
        result.Add(CompletedDefinition);
        result.Add(CancelledDefinition);
        return result;
    }

    /// <summary>
    /// The active vocabulary plus the retired-status redirects. Each redirect is followed to an
    /// active status; a pointer that never reaches one (a cycle or a dangling id) lands on <c>New</c>.
    /// </summary>
    /// <param name="storedStatuses">The stored tenant statuses, or null for the defaults.</param>
    public static TenantStatusVocabulary Build(IReadOnlyList<TenantStatusEmbedded>? storedStatuses)
    {
        var statuses = Resolve(storedStatuses);
        var activeIds = statuses.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var pointers = (storedStatuses ?? [])
            .Where(s => s.Retired)
            .ToDictionary(s => s.Id, s => s.ReplacedBy ?? New, StringComparer.Ordinal);

        var redirects = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var retiredId in pointers.Keys)
        {
            var target = pointers[retiredId];
            for (var hops = 0; !activeIds.Contains(target) && pointers.ContainsKey(target) && hops < pointers.Count; hops++)
            {
                target = pointers[target];
            }

            redirects[retiredId] = activeIds.Contains(target) ? target : New;
        }

        return new TenantStatusVocabulary(statuses, redirects);
    }

    /// <summary>
    /// Validates a tenant's stored status list: ids across every entry, and for the active ones
    /// the count, names and colors. A retired entry is checked only for a replacement that is an
    /// active or fixed status; its name may be reused and it does not count toward the limit.
    /// </summary>
    /// <param name="storedStatuses">The statuses between <c>New</c> and <c>Completed</c>, retired ones included.</param>
    public static ValidationResult Validate(IReadOnlyList<TenantStatusEmbedded> storedStatuses)
    {
        ArgumentNullException.ThrowIfNull(storedStatuses);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var status in storedStatuses)
        {
            if (string.IsNullOrWhiteSpace(status.Id))
            {
                return ValidationResult.Failure("Every status must have an id.");
            }

            if (IsFixed(status.Id))
            {
                return ValidationResult.Failure($"'{status.Id}' is a fixed status and cannot be changed.");
            }

            if (!ids.Add(status.Id))
            {
                return ValidationResult.Failure($"Status id '{status.Id}' appears more than once.");
            }
        }

        var active = storedStatuses.Where(s => !s.Retired).ToList();
        if (active.Count > MaxIntermediateStatuses)
        {
            return ValidationResult.Failure(
                $"A tenant may have at most {MaxStatuses} statuses, including New, Completed and Cancelled.");
        }

        var activeIds = new HashSet<string>(FixedStatusIds.Concat(active.Select(s => s.Id)), StringComparer.Ordinal);
        foreach (var retired in storedStatuses.Where(s => s.Retired))
        {
            if (string.IsNullOrWhiteSpace(retired.ReplacedBy) || !activeIds.Contains(retired.ReplacedBy))
            {
                return ValidationResult.Failure($"Deleted status '{retired.Id}' must point to a status that exists.");
            }
        }

        var names = new HashSet<string>(FixedStatusIds, StringComparer.OrdinalIgnoreCase);

        foreach (var status in active)
        {
            var nameResult = ValidateName(status.Name);
            if (!nameResult.IsValid)
            {
                return nameResult;
            }

            if (!names.Add(status.Name.Trim()))
            {
                return ValidationResult.Failure($"A status named '{status.Name.Trim()}' already exists.");
            }

            if (!ColorSet.Contains(status.Color))
            {
                return ValidationResult.Failure($"Status color must be one of: {string.Join(", ", Colors)}.");
            }
        }

        return ValidationResult.Success;
    }

    private static ValidationResult ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ValidationResult.Failure("Status name is required.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length > MaxNameLength)
        {
            return ValidationResult.Failure($"Status name must not exceed {MaxNameLength} characters.");
        }

        foreach (var c in trimmed)
        {
            if (BlockedCharacters.Contains(c))
            {
                return ValidationResult.Failure($"Status name contains a blocked character: '{c}'.");
            }

            if (char.IsControl(c))
            {
                return ValidationResult.Failure("Status name contains a control character that is not allowed.");
            }
        }

        return ValidationResult.Success;
    }
}
