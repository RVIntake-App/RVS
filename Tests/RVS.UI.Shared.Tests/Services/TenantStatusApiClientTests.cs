using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.UI.Shared.Services;

namespace RVS.UI.Shared.Tests.Services;

/// <summary>
/// Tests for <see cref="TenantStatusApiClient"/> — the tenant's status vocabulary
/// (<c>Spec C-8</c>, issue #855).
/// </summary>
public class TenantStatusApiClientTests
{
    private static TenantStatusApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });

    private static readonly TenantStatusesResponseDto SampleResponse = new()
    {
        Statuses =
        [
            new TenantStatusDto { Id = "New", Name = "New", Color = "green", IsFixed = true },
            new TenantStatusDto { Id = "st_1", Name = "Diagnosing", Color = "blue" },
            new TenantStatusDto { Id = "Completed", Name = "Completed", Color = "purple", IsFixed = true },
            new TenantStatusDto { Id = "Cancelled", Name = "Cancelled", Color = "gray", IsFixed = true },
        ],
        CanEdit = true,
    };

    [Fact]
    public void Constructor_WhenHttpClientIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => new TenantStatusApiClient(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetStatusesAsync_ShouldGetTheStatusesRoute_AndDeserialize()
    {
        var handler = new StubHandler(HttpStatusCode.OK, SampleResponse);
        var sut = CreateClient(handler);

        var result = await sut.GetStatusesAsync();

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/tenants/statuses");
        result.Statuses.Select(s => s.Id).Should().Equal("New", "st_1", "Completed", "Cancelled");
        result.CanEdit.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateStatusesAsync_WhenRequestIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateClient(new StubHandler(HttpStatusCode.OK, SampleResponse));

        var act = () => sut.UpdateStatusesAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateStatusesAsync_ShouldPutTheRequest_AndDeserialize()
    {
        var handler = new StubHandler(HttpStatusCode.OK, SampleResponse);
        var sut = CreateClient(handler);
        var request = new TenantStatusesUpdateRequestDto
        {
            IntermediateStatuses = [new TenantStatusUpdateItemDto { Id = null, Name = "Diagnosing", Color = "blue" }],
            Reassignments = new Dictionary<string, string> { ["InProgress"] = "New" },
        };

        var result = await sut.UpdateStatusesAsync(request);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/tenants/statuses");
        handler.LastRequestBody.Should().Contain("Diagnosing").And.Contain("InProgress");
        result.Statuses.Should().HaveCount(4);
    }

    [Fact]
    public async Task UpdateStatusesAsync_WhenTheApiRejectsTheList_ShouldThrowWithTheApisMessage()
    {
        var sut = CreateClient(new StubHandler(HttpStatusCode.BadRequest,
            new { message = "A status named 'Diagnosing' already exists.", errorId = "e1" }));

        var act = () => sut.UpdateStatusesAsync(new TenantStatusesUpdateRequestDto());

        var ex = await act.Should().ThrowAsync<TenantStatusApiException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Be("A status named 'Diagnosing' already exists.");
    }

    [Fact]
    public async Task UpdateStatusesAsync_WhenForbidden_ShouldThrowAPlainLanguageMessage()
    {
        var sut = CreateClient(new StubHandler(HttpStatusCode.Forbidden, null));

        var act = () => sut.UpdateStatusesAsync(new TenantStatusesUpdateRequestDto());

        var ex = await act.Should().ThrowAsync<TenantStatusApiException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ex.Which.Message.Should().Contain("permission");
    }

    [Fact]
    public async Task GetStatusesAsync_WhenServerFails_ShouldThrowTenantStatusApiException()
    {
        var sut = CreateClient(new StubHandler(HttpStatusCode.InternalServerError, null));

        var act = () => sut.GetStatusesAsync();

        await act.Should().ThrowAsync<TenantStatusApiException>();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object? _responseBody;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public StubHandler(HttpStatusCode statusCode, object? responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            var response = new HttpResponseMessage(_statusCode);
            if (_responseBody is not null)
            {
                response.Content = JsonContent.Create(_responseBody, _responseBody.GetType(), options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }

            return response;
        }
    }
}
