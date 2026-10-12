using Microsoft.AspNetCore.Components;

namespace RVS.UI.Shared.Components;

/// <summary>
/// Displays a colored badge indicating the workflow status of a service request. With a
/// <see cref="Color"/> from the tenant's vocabulary (<c>Spec C-8</c>, issue #855) the badge uses
/// that palette color and <see cref="Label"/>; without one it falls back to recognising the
/// default statuses by their stored value via <see cref="StatusBadgeFormatting"/>.
/// </summary>
public partial class StatusBadge : ComponentBase
{
    /// <summary>
    /// The stored workflow status (e.g., "New", "InProgress", or a tenant-added "st_…" id).
    /// </summary>
    [Parameter, EditorRequired]
    public string Status { get; set; } = string.Empty;

    /// <summary>The tenant's name for the status. Shown instead of <see cref="Status"/> when set.</summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>The tenant's palette color key for the status.</summary>
    [Parameter]
    public string? Color { get; set; }

    /// <summary>The text shown on the badge.</summary>
    protected string Text => string.IsNullOrWhiteSpace(Label) ? Status : Label;

    /// <summary>
    /// Computed CSS class: the palette color when one is given, else the stored status.
    /// </summary>
    protected string CssClass => string.IsNullOrWhiteSpace(Color)
        ? StatusBadgeFormatting.GetCssClass(Status)
        : StatusBadgeFormatting.GetCssClassForColor(Color);
}
