using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RVS.Domain.DTOs;

namespace RVS.UI.Shared.Services;

/// <summary>
/// Typed HTTP client for the tenant's service-request status vocabulary (<c>Spec C-8</c>,
/// issue #855): <c>api/tenants/statuses</c>. A refusal arrives as a
/// <see cref="TenantStatusApiException"/> carrying the API's own sentence, so the board can
/// show "A status named … already exists" rather than an HTTP error.
/// </summary>
public sealed class TenantStatusApiClient
{
    private const string Route = "api/tenants/statuses";

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="TenantStatusApiClient"/>.
    /// </summary>
    /// <param name="httpClient">The configured <see cref="HttpClient"/> injected via DI.</param>
    public TenantStatusApiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>The tenant's statuses in board order, and whether the user may edit them.</summary>
    /// <exception cref="TenantStatusApiException">The API refused the read.</exception>
    public async Task<TenantStatusesResponseDto> GetStatusesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(Route, cancellationToken);

        await ThrowIfRefusedAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<TenantStatusesResponseDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize the status list.");
    }

    /// <summary>
    /// Replaces the tenant's statuses between <c>New</c> and <c>Completed</c>.
    /// </summary>
    /// <exception cref="TenantStatusApiException">The API refused the change.</exception>
    public async Task<TenantStatusesResponseDto> UpdateStatusesAsync(
        TenantStatusesUpdateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _httpClient.PutAsJsonAsync(Route, request, cancellationToken);

        await ThrowIfRefusedAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<TenantStatusesResponseDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize the status list.");
    }

    private static async Task ThrowIfRefusedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = response.StatusCode == HttpStatusCode.BadRequest
            ? await ReadMessageAsync(response, cancellationToken)
            : null;

        throw new TenantStatusApiException(response.StatusCode, message ?? DefaultMessageFor(response.StatusCode));
    }

    /// <summary>
    /// Reads <c>message</c> from the API's <c>{ message, errorId }</c> body. Only trusted for a
    /// 400, where it is the validation sentence; other errors get a fixed message.
    /// </summary>
    private static async Task<string?> ReadMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
            {
                var text = message.GetString();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
        }
        catch (JsonException)
        {
            // Not the API's JSON shape; fall back to the default message.
        }

        return null;
    }

    private static string DefaultMessageFor(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Forbidden => "You don't have permission to change statuses.",
        HttpStatusCode.BadRequest => "The status change was not accepted.",
        HttpStatusCode.NotFound => "The status list could not be found.",
        _ => "The status list could not be loaded or saved. Try again.",
    };
}

/// <summary>
/// A refusal from <see cref="TenantStatusApiClient"/>, with a message safe to show the user.
/// </summary>
public sealed class TenantStatusApiException : Exception
{
    /// <summary>Initializes a new instance of <see cref="TenantStatusApiException"/>.</summary>
    public TenantStatusApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>The HTTP status the API returned.</summary>
    public HttpStatusCode StatusCode { get; }
}
