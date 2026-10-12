using RVS.Domain.DTOs;
using RVS.Domain.Validation;

namespace RVS.Blazor.Manager.Shared;

/// <summary>
/// Pure logic behind the manager app's view of a tenant's status vocabulary (<c>Spec C-8</c>,
/// issue #855): display labels, the service board's columns (<c>Spec C-10</c>), and the update
/// request each board edit sends. The fixed <c>New</c>, <c>Completed</c> and <c>Cancelled</c>
/// are never editable and never sent.
/// </summary>
public static class StatusVocabularyView
{
    /// <summary>The Done column's zone id. A drop on it sets <c>Completed</c>.</summary>
    public const string DoneZone = StatusVocabulary.Completed;

    /// <summary>The default vocabulary, used until the tenant's own has loaded.</summary>
    public static IReadOnlyList<TenantStatusDto> Defaults { get; } =
        [.. StatusVocabulary.Resolve(null).Select(s => new TenantStatusDto
        {
            Id = s.Id,
            Name = s.Name,
            Color = s.Color,
            IsFixed = s.IsFixed,
        })];

    /// <summary>The status's display name; an unknown value is echoed back.</summary>
    public static string Label(IReadOnlyList<TenantStatusDto> statuses, string? statusId) =>
        statusId is null ? string.Empty : Find(statuses, statusId)?.Name ?? statusId;

    /// <summary>The status's palette color key, or null when it is not in the vocabulary.</summary>
    public static string? Color(IReadOnlyList<TenantStatusDto> statuses, string? statusId) =>
        statusId is null ? null : Find(statuses, statusId)?.Color;

    /// <summary>The board's columns: <c>New</c>, each tenant status in order, then Done.</summary>
    public static IReadOnlyList<BoardColumnView> BuildColumns(IReadOnlyList<TenantStatusDto> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        var columns = new List<BoardColumnView>
        {
            new(StatusVocabulary.New, Label(statuses, StatusVocabulary.New), "Items that haven't been started", IsEditable: false),
        };
        columns.AddRange(Intermediates(statuses).Select(s => new BoardColumnView(s.Id, s.Name, string.Empty, IsEditable: true)));
        columns.Add(new BoardColumnView(
            DoneZone,
            "Done",
            $"Completed or cancelled in the last {BoardRetentionFilter.DoneColumnWindowDays} days",
            IsEditable: false));
        return columns;
    }

    /// <summary>
    /// The column a status sits in. <c>Completed</c> and <c>Cancelled</c> share Done; a status
    /// no longer in the vocabulary falls back to <c>New</c> so its card stays visible.
    /// </summary>
    public static string ZoneFor(IReadOnlyList<TenantStatusDto> statuses, string status)
    {
        if (status is StatusVocabulary.Completed or StatusVocabulary.Cancelled)
        {
            return DoneZone;
        }

        return Find(statuses, status) is not null ? status : StatusVocabulary.New;
    }

    /// <summary>The status a drop on a column sets.</summary>
    public static string StatusForZone(string zoneId) => zoneId;

    /// <summary>Whether another status can be added within the 12-status limit.</summary>
    public static bool CanAdd(IReadOnlyList<TenantStatusDto> statuses) =>
        statuses.Count < StatusVocabulary.MaxStatuses;

    /// <summary>Whether the status can move <paramref name="offset"/> places and stay among the tenant's statuses.</summary>
    public static bool CanMove(IReadOnlyList<TenantStatusDto> statuses, string statusId, int offset)
    {
        var list = Intermediates(statuses).ToList();
        var index = list.FindIndex(s => s.Id == statusId);
        var target = index + offset;
        return index >= 0 && target >= 0 && target < list.Count;
    }

    /// <summary>Adds a status at the end of the tenant's statuses, just before Done.</summary>
    public static TenantStatusesUpdateRequestDto Add(IReadOnlyList<TenantStatusDto> statuses, string name, string color)
    {
        var items = ToItems(statuses);
        items.Add(new TenantStatusUpdateItemDto { Id = null, Name = name.Trim(), Color = color });
        return new TenantStatusesUpdateRequestDto { IntermediateStatuses = items };
    }

    /// <summary>Renames and recolors one of the tenant's statuses.</summary>
    public static TenantStatusesUpdateRequestDto Edit(IReadOnlyList<TenantStatusDto> statuses, string statusId, string name, string color) =>
        new()
        {
            IntermediateStatuses =
                [.. ToItems(statuses).Select(i => i.Id == statusId ? i with { Name = name.Trim(), Color = color } : i)],
        };

    /// <summary>Moves one of the tenant's statuses left (-1) or right (+1); past either end it stays put.</summary>
    public static TenantStatusesUpdateRequestDto Move(IReadOnlyList<TenantStatusDto> statuses, string statusId, int offset)
    {
        var items = ToItems(statuses);
        if (CanMove(statuses, statusId, offset))
        {
            var index = items.FindIndex(i => i.Id == statusId);
            (items[index], items[index + offset]) = (items[index + offset], items[index]);
        }

        return new TenantStatusesUpdateRequestDto { IntermediateStatuses = items };
    }

    /// <summary>Deletes one of the tenant's statuses, moving any requests in it to <paramref name="moveRequestsTo"/>.</summary>
    public static TenantStatusesUpdateRequestDto Delete(IReadOnlyList<TenantStatusDto> statuses, string statusId, string moveRequestsTo) =>
        new()
        {
            IntermediateStatuses = [.. ToItems(statuses).Where(i => i.Id != statusId)],
            Reassignments = new Dictionary<string, string> { [statusId] = moveRequestsTo },
        };

    /// <summary>The statuses a deleted status's requests can move to: every other one.</summary>
    public static IReadOnlyList<TenantStatusDto> DeleteTargets(IReadOnlyList<TenantStatusDto> statuses, string statusId) =>
        [.. statuses.Where(s => s.Id != statusId)];

    private static IEnumerable<TenantStatusDto> Intermediates(IReadOnlyList<TenantStatusDto> statuses) =>
        statuses.Where(s => !s.IsFixed);

    private static List<TenantStatusUpdateItemDto> ToItems(IReadOnlyList<TenantStatusDto> statuses) =>
        [.. Intermediates(statuses).Select(s => new TenantStatusUpdateItemDto { Id = s.Id, Name = s.Name, Color = s.Color })];

    private static TenantStatusDto? Find(IReadOnlyList<TenantStatusDto> statuses, string statusId) =>
        statuses.FirstOrDefault(s => string.Equals(s.Id, statusId, StringComparison.Ordinal));
}

/// <summary>One service-board column (<c>Spec C-10</c>).</summary>
/// <param name="ZoneId">The drop-zone id: a status id, or <see cref="StatusVocabularyView.DoneZone"/>.</param>
/// <param name="Title">Column heading.</param>
/// <param name="Description">Caption under the heading; empty for a tenant's own status.</param>
/// <param name="IsEditable">True for a tenant's own status, which has the column menu.</param>
public sealed record BoardColumnView(string ZoneId, string Title, string Description, bool IsEditable);
