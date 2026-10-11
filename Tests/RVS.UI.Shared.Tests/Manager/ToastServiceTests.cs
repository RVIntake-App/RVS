using FluentAssertions;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using RVS.Blazor.Manager.Services;
using RVS.UI.Shared.Tests.Fakes;

namespace RVS.UI.Shared.Tests.Manager;

/// <summary>
/// Tests for <see cref="ToastService"/> and <see cref="ToastDefaults"/> — the one way Manager
/// raises a transient notification: a toast in the bottom-right corner (issue #867).
/// </summary>
public class ToastServiceTests
{
    private static (ToastService Toasts, RecordingSnackbar Snackbar) Create()
    {
        var snackbar = new RecordingSnackbar();
        ToastDefaults.Configure(snackbar.Configuration);
        return (new ToastService(snackbar), snackbar);
    }

    // ---- ToastDefaults ----

    [Fact]
    public void Configure_WhenConfigurationIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => ToastDefaults.Configure(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Configure_ShouldPlaceToastsBottomRight()
    {
        var config = new SnackbarConfiguration();

        ToastDefaults.Configure(config);

        config.PositionClass.Should().Be(Defaults.Classes.Position.BottomRight);
    }

    [Fact]
    public void Configure_ShouldStackNewestNearestTheCornerAndCapTheStack()
    {
        var config = new SnackbarConfiguration();

        ToastDefaults.Configure(config);

        config.NewestOnTop.Should().BeFalse();
        config.MaxDisplayedSnackbars.Should().Be(ToastDefaults.MaxDisplayed);
    }

    [Fact]
    public void Configure_ShouldShowSeverityIconAndCloseButtonAndPreventDuplicates()
    {
        var config = new SnackbarConfiguration();

        ToastDefaults.Configure(config);

        config.HideIcon.Should().BeFalse();
        config.ShowCloseIcon.Should().BeTrue();
        config.PreventDuplicates.Should().BeTrue();
        config.VisibleStateDuration.Should().Be(ToastDefaults.ShortDurationMs);
    }

    // ---- Constructor ----

    [Fact]
    public void Constructor_WhenSnackbarIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => new ToastService(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ---- Success / Info / Warning / Error ----

    [Fact]
    public void Success_ShouldAddSuccessToastThatDismissesItself()
    {
        var (toasts, snackbar) = Create();

        toasts.Success("Saved.");

        var call = snackbar.Calls.Should().ContainSingle().Subject;
        call.Text.Should().Be("Saved.");
        call.Severity.Should().Be(Severity.Success);
        call.Options.RequireInteraction.Should().BeFalse();
        call.Options.VisibleStateDuration.Should().Be(ToastDefaults.ShortDurationMs);
    }

    [Fact]
    public void Info_ShouldAddInfoToastThatDismissesItself()
    {
        var (toasts, snackbar) = Create();

        toasts.Info("Heads up.");

        var call = snackbar.Calls.Should().ContainSingle().Subject;
        call.Severity.Should().Be(Severity.Info);
        call.Options.RequireInteraction.Should().BeFalse();
        call.Options.VisibleStateDuration.Should().Be(ToastDefaults.ShortDurationMs);
    }

    [Fact]
    public void Warning_ShouldAddWarningToastThatStaysLonger()
    {
        var (toasts, snackbar) = Create();

        toasts.Warning("Fix the highlighted fields.");

        var call = snackbar.Calls.Should().ContainSingle().Subject;
        call.Severity.Should().Be(Severity.Warning);
        call.Options.VisibleStateDuration.Should().Be(ToastDefaults.LongDurationMs);
    }

    [Fact]
    public void Error_ShouldAddErrorToastThatStaysLongerWithIconAndCloseButton()
    {
        var (toasts, snackbar) = Create();

        toasts.Error("Failed to save.");

        var call = snackbar.Calls.Should().ContainSingle().Subject;
        call.Severity.Should().Be(Severity.Error);
        call.Options.VisibleStateDuration.Should().Be(ToastDefaults.LongDurationMs);
        call.Options.HideIcon.Should().BeFalse();
        call.Options.ShowCloseIcon.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Success_WhenMessageIsBlank_ShouldThrowArgumentException(string? message)
    {
        var (toasts, _) = Create();

        var act = () => toasts.Success(message!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Error_WhenMessageIsBlank_ShouldThrowArgumentException(string? message)
    {
        var (toasts, _) = Create();

        var act = () => toasts.Error(message!);

        act.Should().Throw<ArgumentException>();
    }

    // ---- Prompt (one action) ----

    [Fact]
    public void Prompt_ShouldAddPersistentKeyedInfoToastWithAction()
    {
        var (toasts, snackbar) = Create();

        toasts.Prompt("A new version is available.", "Reload", () => Task.CompletedTask, "rvs-new-build");

        var call = snackbar.Calls.Should().ContainSingle().Subject;
        call.Text.Should().Be("A new version is available.");
        call.Severity.Should().Be(Severity.Info);
        call.Key.Should().Be("rvs-new-build");
        call.Options.RequireInteraction.Should().BeTrue();
        call.Options.ShowCloseIcon.Should().BeTrue();
        call.Options.Action.Should().Be("Reload");
        call.Options.ActionColor.Should().Be(Color.Primary);
        call.Options.DuplicatesBehavior.Should().Be(SnackbarDuplicatesBehavior.Prevent);
    }

    [Fact]
    public async Task Prompt_WhenActionClicked_ShouldInvokeCallback()
    {
        var (toasts, snackbar) = Create();
        var invoked = false;

        toasts.Prompt("A new version is available.", "Reload", () => { invoked = true; return Task.CompletedTask; }, "rvs-new-build");
        await snackbar.Calls.Single().Options.OnClick!(null!);

        invoked.Should().BeTrue();
    }

    [Fact]
    public void Prompt_WhenActionIsNull_ShouldThrowArgumentNullException()
    {
        var (toasts, _) = Create();

        var act = () => toasts.Prompt("Message", "Reload", null!, "key");

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Prompt_WhenKeyIsBlank_ShouldThrowArgumentException(string? key)
    {
        var (toasts, _) = Create();

        var act = () => toasts.Prompt("Message", "Reload", () => Task.CompletedTask, key!);

        act.Should().Throw<ArgumentException>();
    }

    // ---- Prompt (custom content) ----

    [Fact]
    public void Prompt_WithContent_ShouldAddPersistentKeyedToastWithoutCloseButton()
    {
        var (toasts, snackbar) = Create();
        RenderFragment content = _ => { };

        toasts.Prompt(content, "rvs-keep-signed-in");

        var call = snackbar.Calls.Should().ContainSingle().Subject;
        call.Content.Should().BeSameAs(content);
        call.Severity.Should().Be(Severity.Normal);
        call.Key.Should().Be("rvs-keep-signed-in");
        call.Options.RequireInteraction.Should().BeTrue();
        call.Options.ShowCloseIcon.Should().BeFalse();
        call.Options.HideIcon.Should().BeTrue();
        call.Options.DuplicatesBehavior.Should().Be(SnackbarDuplicatesBehavior.Prevent);
    }

    [Fact]
    public void Prompt_WithContent_WhenContentIsNull_ShouldThrowArgumentNullException()
    {
        var (toasts, _) = Create();

        var act = () => toasts.Prompt((RenderFragment)null!, "key");

        act.Should().Throw<ArgumentNullException>();
    }

    // ---- Dismiss ----

    [Fact]
    public void Dismiss_ShouldRemoveToastByKey()
    {
        var (toasts, snackbar) = Create();

        toasts.Dismiss("rvs-keep-signed-in");

        snackbar.RemovedKeys.Should().ContainSingle().Which.Should().Be("rvs-keep-signed-in");
    }
}
