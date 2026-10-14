using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RVS.Blazor.Manager.State;
using RVS.Domain.DTOs;
using RVS.UI.Shared.Services;

namespace RVS.UI.Shared.Tests.Manager;

/// <summary>
/// Tests for <see cref="StatusVocabularyState"/> — the manager app's cached copy of the tenant's
/// status vocabulary (<c>Spec C-8</c>, issue #855).
/// </summary>
public class StatusVocabularyStateTests
{
    private static readonly TenantStatusesResponseDto Custom = new()
    {
        Statuses =
        [
            new TenantStatusDto { Id = "New", Name = "New", Color = "green", IsFixed = true },
            new TenantStatusDto { Id = "st_diag", Name = "Diagnosing", Color = "blue" },
            new TenantStatusDto { Id = "Completed", Name = "Completed", Color = "purple", IsFixed = true },
            new TenantStatusDto { Id = "Cancelled", Name = "Cancelled", Color = "gray", IsFixed = true },
        ],
        CanEdit = true,
    };

    [Fact]
    public void BeforeLoading_ShouldExposeTheDefaultsAndNoEditing()
    {
        var sut = new StatusVocabularyState(Client(new StubHandler(HttpStatusCode.OK, Custom)));

        sut.Statuses.Select(s => s.Id).Should().Contain("InProgress");
        sut.CanEdit.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureLoadedAsync_ShouldLoadTheTenantsVocabulary()
    {
        var sut = new StatusVocabularyState(Client(new StubHandler(HttpStatusCode.OK, Custom)));

        await sut.EnsureLoadedAsync();

        sut.Statuses.Select(s => s.Id).Should().Equal("New", "st_diag", "Completed", "Cancelled");
        sut.CanEdit.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureLoadedAsync_ShouldOnlyCallTheApiOnce()
    {
        var handler = new StubHandler(HttpStatusCode.OK, Custom);
        var sut = new StatusVocabularyState(Client(handler));

        await sut.EnsureLoadedAsync();
        await sut.EnsureLoadedAsync();

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task EnsureLoadedAsync_WhenTheApiFails_ShouldKeepTheDefaultsAndTryAgainNextTime()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, null);
        var sut = new StatusVocabularyState(Client(handler));

        await sut.EnsureLoadedAsync();
        await sut.EnsureLoadedAsync();

        sut.Statuses.Select(s => s.Id).Should().Contain("InProgress");
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task EnsureLoadedAsync_ShouldRaiseOnChanged()
    {
        var sut = new StatusVocabularyState(Client(new StubHandler(HttpStatusCode.OK, Custom)));
        var raised = 0;
        sut.OnChanged += () => raised++;

        await sut.EnsureLoadedAsync();

        raised.Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_ShouldReplaceTheVocabularyAndRaiseOnChanged()
    {
        var sut = new StatusVocabularyState(Client(new StubHandler(HttpStatusCode.OK, Custom)));
        var raised = 0;
        sut.OnChanged += () => raised++;

        await sut.SaveAsync(new TenantStatusesUpdateRequestDto());

        sut.Statuses.Select(s => s.Id).Should().Equal("New", "st_diag", "Completed", "Cancelled");
        raised.Should().Be(1);
    }

    [Fact]
    public async Task Resolve_ForADeletedStatus_ShouldReturnItsReplacement()
    {
        var sut = new StatusVocabularyState(Client(new StubHandler(HttpStatusCode.OK, Custom with
        {
            Redirects = new Dictionary<string, string> { ["st_old"] = "st_diag" },
        })));
        await sut.EnsureLoadedAsync();

        sut.Resolve("st_old").Should().Be("st_diag");
        sut.Resolve("st_diag").Should().Be("st_diag");
        sut.Label("st_old").Should().Be("Diagnosing");
        sut.Color("st_old").Should().Be("blue");
    }

    [Fact]
    public void Label_ForAnUnknownStatus_ShouldEchoIt()
    {
        var sut = new StatusVocabularyState(Client(new StubHandler(HttpStatusCode.OK, Custom)));

        sut.Label("Mystery").Should().Be("Mystery");
        sut.Color("Mystery").Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WhenRefused_ShouldThrowAndKeepTheCurrentVocabulary()
    {
        var sut = new StatusVocabularyState(Client(new StubHandler(HttpStatusCode.BadRequest,
            new { message = "A status named 'New' already exists." })));

        var act = () => sut.SaveAsync(new TenantStatusesUpdateRequestDto());

        await act.Should().ThrowAsync<TenantStatusApiException>().WithMessage("*already exists*");
        sut.Statuses.Select(s => s.Id).Should().Contain("InProgress");
    }

    private static TenantStatusApiClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });

    private sealed class StubHandler(HttpStatusCode statusCode, object? body) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(statusCode);
            if (body is not null)
            {
                response.Content = JsonContent.Create(body, body.GetType(), options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }

            return Task.FromResult(response);
        }
    }
}
