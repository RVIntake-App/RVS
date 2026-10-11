using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace RVS.Blazor.Manager.Services;

/// <summary>
/// The one look for a Manager toast (issue #867): bottom right, newest nearest the corner,
/// severity icon always shown so an error never relies on colour alone (Spec THEME-1).
/// </summary>
public static class ToastDefaults
{
    /// <summary>How long a success or info toast stays up.</summary>
    public const int ShortDurationMs = 4000;

    /// <summary>How long a warning or error toast stays up — long enough to read a reason.</summary>
    public const int LongDurationMs = 8000;

    /// <summary>Toasts shown at once; further ones queue.</summary>
    public const int MaxDisplayed = 3;

    /// <summary>Applies the Manager toast defaults to MudBlazor's snackbar configuration.</summary>
    public static void Configure(SnackbarConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.PositionClass = Defaults.Classes.Position.BottomRight;
        configuration.NewestOnTop = false;
        configuration.MaxDisplayedSnackbars = MaxDisplayed;
        configuration.PreventDuplicates = true;
        configuration.ShowCloseIcon = true;
        configuration.HideIcon = false;
        configuration.SnackbarVariant = Variant.Filled;
        configuration.VisibleStateDuration = ShortDurationMs;
    }
}

/// <summary>
/// Raises Manager's transient notifications as toasts (issue #867). Pages and dialogs go through
/// this rather than <see cref="ISnackbar"/> so every notice looks, sits and times out the same.
/// </summary>
public sealed class ToastService
{
    private readonly ISnackbar _snackbar;

    public ToastService(ISnackbar snackbar)
    {
        ArgumentNullException.ThrowIfNull(snackbar);
        _snackbar = snackbar;
    }

    /// <summary>Confirms something the user did worked.</summary>
    public void Success(string message) => Show(message, Severity.Success, ToastDefaults.ShortDurationMs);

    /// <summary>Tells the user something they did not ask about.</summary>
    public void Info(string message) => Show(message, Severity.Info, ToastDefaults.ShortDurationMs);

    /// <summary>Asks the user to fix something before trying again.</summary>
    public void Warning(string message) => Show(message, Severity.Warning, ToastDefaults.LongDurationMs);

    /// <summary>Reports that something failed.</summary>
    public void Error(string message) => Show(message, Severity.Error, ToastDefaults.LongDurationMs);

    /// <summary>
    /// A toast that stays until the user acts on it or closes it, with one action button.
    /// <paramref name="key"/> keeps it from stacking if raised again.
    /// </summary>
    public void Prompt(string message, string actionLabel, Func<Task> onAction, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionLabel);
        ArgumentNullException.ThrowIfNull(onAction);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _snackbar.Add(message, Severity.Info, options =>
        {
            options.RequireInteraction = true;
            options.ShowCloseIcon = true;
            options.DuplicatesBehavior = SnackbarDuplicatesBehavior.Prevent;
            options.Action = actionLabel;
            options.ActionColor = Color.Primary;
            options.OnClick = _ => onAction();
        }, key);
    }

    /// <summary>
    /// A toast that stays until the content itself calls <see cref="Dismiss"/> — for a question
    /// with more than one answer, where a close button would leave it unanswered.
    /// </summary>
    public void Prompt(RenderFragment content, string key)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _snackbar.Add(content, Severity.Normal, options =>
        {
            options.RequireInteraction = true;
            options.ShowCloseIcon = false;
            options.HideIcon = true;
            options.DuplicatesBehavior = SnackbarDuplicatesBehavior.Prevent;
        }, key);
    }

    /// <summary>Removes the toast raised with <paramref name="key"/>, if it is still up.</summary>
    public void Dismiss(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _snackbar.RemoveByKey(key);
    }

    private void Show(string message, Severity severity, int durationMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        _snackbar.Add(message, severity, options =>
        {
            options.RequireInteraction = false;
            options.VisibleStateDuration = durationMs;
            options.ShowCloseIcon = true;
            options.HideIcon = false;
        });
    }
}
