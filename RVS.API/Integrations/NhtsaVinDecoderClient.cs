using System.Text.Json.Serialization;
using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Decodes VINs using the NHTSA vPIC public API.
/// Returns <c>null</c> on timeout or HTTP error — never throws.
/// </summary>
public sealed class NhtsaVinDecoderClient : IVinDecoderService
{
    /// <summary>vPIC's <c>ErrorCode</c> for a clean decode. Anything else is a partial decode.</summary>
    private const string CleanDecodeErrorCode = "0";

    /// <summary>What vPIC puts in a field that does not apply, e.g. <c>TrailerType</c> on a motorhome.</summary>
    private const string NotApplicable = "Not Applicable";

    private readonly HttpClient _httpClient;
    private readonly ILogger<NhtsaVinDecoderClient> _logger;

    public NhtsaVinDecoderClient(HttpClient httpClient, ILogger<NhtsaVinDecoderClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<VinDecoderResult?> DecodeVinAsync(string vin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);

        try
        {
            var response = await _httpClient.GetFromJsonAsync<NhtsaApiResponse>(
                $"vehicles/DecodeVinValues/{vin}?format=json", cancellationToken);

            var result = response?.Results.FirstOrDefault();
            if (result is null || string.IsNullOrWhiteSpace(result.Make))
            {
                _logger.LogWarning("NHTSA VIN decode returned no results for VIN ending {VinSuffix}", vin[^4..]);
                return null;
            }

            _ = int.TryParse(result.ModelYear, out var year);

            // A non-zero ErrorCode is a partial decode. Even a clean one can name the wrong brand: Make
            // is the brand registered to the maker's WMI, so an Arctic Fox decodes as NASH (issue #854).
            // The caller keeps a brand read off the plate over it.
            var isExactMatch = result.ErrorCode?.Trim() == CleanDecodeErrorCode;
            if (!isExactMatch)
            {
                _logger.LogInformation(
                    "NHTSA VIN decode was partial for VIN ending {VinSuffix}: Make {Make}, Manufacturer {VpicManufacturer}, ErrorCode {ErrorCode}",
                    vin[^4..], result.Make, result.Manufacturer, result.ErrorCode);
            }

            // RV floor plans and model lines often come back in Series or Trim rather than Model.
            var model = FirstPresent(result.Model, result.Series, result.Trim) ?? string.Empty;
            var bodyType = RvBodyTypeClassifier.Classify(new VinBodyFields(
                result.VehicleType, result.BodyClass, result.TrailerBodyType, result.TrailerType, result.OtherTrailerInfo));

            return new VinDecoderResult(vin, result.Make, model, year, bodyType, isExactMatch);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "NHTSA VIN decode failed for VIN ending {VinSuffix}", vin[^4..]);
            return null;
        }
    }

    private static string? FirstPresent(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v) && !string.Equals(v.Trim(), NotApplicable, StringComparison.OrdinalIgnoreCase))?.Trim();

    /// <summary>
    /// Top-level NHTSA vPIC API response.
    /// </summary>
    internal sealed class NhtsaApiResponse
    {
        [JsonPropertyName("Results")]
        public List<NhtsaVehicle> Results { get; set; } = [];
    }

    /// <summary>
    /// Individual vehicle record from the NHTSA vPIC DecodeVinValues endpoint.
    /// </summary>
    internal sealed class NhtsaVehicle
    {
        [JsonPropertyName("Make")]
        public string? Make { get; set; }

        [JsonPropertyName("Model")]
        public string? Model { get; set; }

        [JsonPropertyName("ModelYear")]
        public string? ModelYear { get; set; }

        /// <summary>The company of record (e.g. "NORTHWOOD MANUFACTURING"). Logged for diagnostics, never shown.</summary>
        [JsonPropertyName("Manufacturer")]
        public string? Manufacturer { get; set; }

        /// <summary>"0" on a clean decode; otherwise one or more vPIC error codes.</summary>
        [JsonPropertyName("ErrorCode")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("Series")]
        public string? Series { get; set; }

        [JsonPropertyName("Trim")]
        public string? Trim { get; set; }

        [JsonPropertyName("VehicleType")]
        public string? VehicleType { get; set; }

        [JsonPropertyName("OtherTrailerInfo")]
        public string? OtherTrailerInfo { get; set; }

        [JsonPropertyName("TrailerBodyType")]
        public string? TrailerBodyType { get; set; }

        [JsonPropertyName("BodyClass")]
        public string? BodyClass { get; set; }

        /// <summary>The hitch ("Fifth Wheel", "Bumper Pull"), not the kind of RV.</summary>
        [JsonPropertyName("TrailerType")]
        public string? TrailerType { get; set; }
    }
}
