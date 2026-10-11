using RVS.Domain.Integrations;

namespace RVS.API.Integrations;

/// <summary>
/// Development mock for the VIN decoder service.
/// Activate one response block inside <see cref="DecodeVinAsync"/> at a time:
/// API failure, negative (null), wrong brand on a clean decode, partial, or positive (Grand Design Momentum, the default).
/// </summary>
public sealed class MockVinDecoderService : IVinDecoderService
{
    private readonly ILogger<MockVinDecoderService> _logger;

    public MockVinDecoderService(ILogger<MockVinDecoderService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<VinDecoderResult?> DecodeVinAsync(string vin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);

        // API failure — uncomment to simulate a downstream HTTP error:
        //_logger.LogDebug("MockVinDecoderService simulating API failure for VIN ending {VinSuffix}", vin[^4..]);
        //throw new HttpRequestException("Mock VIN decoder API failure (503 Service Unavailable).");

        // Negative response — uncomment to return a not-found result:
        // _logger.LogDebug("MockVinDecoderService returning null (negative response) for VIN ending {VinSuffix}", vin[^4..]);
        // return Task.FromResult<VinDecoderResult?>(null);

        // Wrong brand on a clean decode — uncomment to return what vPIC says for an Arctic Fox (issue #854):
        // _logger.LogDebug("MockVinDecoderService returning a clean NASH decode for VIN ending {VinSuffix}", vin[^4..]);
        // return Task.FromResult<VinDecoderResult?>(new VinDecoderResult(vin, "NASH", "Northwood Investments Corporation", 2021, RvBodyType.FifthWheel, IsExactMatch: true));

        // Partial response — uncomment to return a decode with a non-zero vPIC ErrorCode:
        // _logger.LogDebug("MockVinDecoderService returning a partial decode for VIN ending {VinSuffix}", vin[^4..]);
        // return Task.FromResult<VinDecoderResult?>(new VinDecoderResult(vin, "Grand Design", string.Empty, 2024, RvBodyType.FifthWheel, IsExactMatch: false));

        // Positive response — uncomment to return Grand Design Momentum data:
        _logger.LogDebug("MockVinDecoderService returning Grand Design Momentum for VIN ending {VinSuffix}", vin[^4..]);
        var result = new VinDecoderResult(vin, "Grand Design", "Momentum 395MS", 2024, RvBodyType.FifthWheel, IsExactMatch: true);
        return Task.FromResult<VinDecoderResult?>(result);
    }
}
