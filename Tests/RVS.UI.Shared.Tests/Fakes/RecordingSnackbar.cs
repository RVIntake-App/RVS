using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace RVS.UI.Shared.Tests.Fakes;

/// <summary>
/// An <see cref="ISnackbar"/> that renders nothing and records each call, with the options the
/// caller's configure delegate produced, so tests can assert what a toast would look like.
/// </summary>
public sealed class RecordingSnackbar : ISnackbar
{
    public sealed record Call(string? Text, RenderFragment? Content, Severity Severity, SnackbarOptions Options, string? Key);

    public List<Call> Calls { get; } = [];

    public List<string> RemovedKeys { get; } = [];

    public SnackbarConfiguration Configuration { get; } = new();

    public IEnumerable<Snackbar> ShownSnackbars => [];

#pragma warning disable CS0067 // Never raised: nothing renders.
    public event Action? OnSnackbarsUpdated;
#pragma warning restore CS0067

    public Snackbar? Add(string message, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = null)
        => Record(message, null, severity, configure, key);

    public Snackbar? Add(MarkupString message, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = null)
        => Record(message.Value, null, severity, configure, key);

    public Snackbar? Add(RenderFragment message, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = null)
        => Record(null, message, severity, configure, key);

    public Snackbar? Add<T>(Dictionary<string, object>? componentParameters = null, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = null)
        where T : IComponent
        => Record(typeof(T).Name, null, severity, configure, key);

    public void Clear() => Calls.Clear();

    public void Remove(Snackbar snackbar) { }

    public void RemoveByKey(string key) => RemovedKeys.Add(key);

    public void Dispose() { }

    private Snackbar? Record(string? text, RenderFragment? content, Severity severity, Action<SnackbarOptions>? configure, string? key)
    {
        var options = new SnackbarOptions(severity, Configuration);
        configure?.Invoke(options);
        Calls.Add(new Call(text, content, severity, options, key));
        return null;
    }
}
