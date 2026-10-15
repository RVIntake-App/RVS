using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace RVS.API.Tests.Integration;

/// <summary>
/// Swagger UI stays in Development and Staging for browsing the API, without an Auth0 sign-in.
/// Swashbuckle writes its OAuth settings into <c>/swagger/index.js</c>, a public page, so a
/// configured client secret was published there. The Swagger SPA client is deleted; neither
/// the page nor the OpenAPI document may carry OAuth client details.
/// </summary>
public sealed class SwaggerUiIntegrationTests : IClassFixture<TenantAccessGateApiFactory>
{
    private readonly TenantAccessGateApiFactory _factory;

    public SwaggerUiIntegrationTests(TenantAccessGateApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SwaggerUi_InDevelopment_IsServed()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/index.html");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SwaggerUiConfig_WithAuth0ClientSettings_DoesNotPublishThem()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/index.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var script = await response.Content.ReadAsStringAsync();
        script.Should().NotContain(TenantAccessGateApiFactory.SentinelClientSecret);
        script.Should().NotContain(TenantAccessGateApiFactory.SentinelClientId);
    }

    [Fact]
    public async Task OpenApiDocument_DeclaresNoOAuth2SecurityScheme()
    {
        var response = await _factory.CreateClient().GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var hasSchemes = doc.RootElement.TryGetProperty("components", out var components)
            && components.TryGetProperty("securitySchemes", out _);
        hasSchemes.Should().BeFalse();
    }
}
