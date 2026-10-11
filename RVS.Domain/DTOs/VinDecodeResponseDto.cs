namespace RVS.Domain.DTOs;

/// <summary>
/// Response DTO for a VIN decode operation.
/// Returned by the intake VIN lookup endpoint.
/// </summary>
public sealed record VinDecodeResponseDto
{
    /// <summary>The original VIN that was decoded.</summary>
    public required string Vin { get; init; }

    /// <summary>Vehicle manufacturer name (e.g. "Grand Design").</summary>
    public required string Manufacturer { get; init; }

    /// <summary>Vehicle model name (e.g. "Momentum 395MS").</summary>
    public required string Model { get; init; }

    /// <summary>Model year.</summary>
    public required int Year { get; init; }

    /// <summary>
    /// The kind of RV as a display name (e.g. "Fifth Wheel", "Class C Motorhome"), when the decode
    /// says which; see <c>RvBodyType</c>. Shown on Step 4, not stored on the request.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Whether the decode was exact (vPIC <c>ErrorCode</c> 0). Only an exact decode overrides the year
    /// read off the plate; the plate's brand and model are never overridden (Spec A-10).
    /// </summary>
    public bool IsExactMatch { get; init; }
}
