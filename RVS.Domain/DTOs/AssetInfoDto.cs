using RVS.Domain.Integrations;

namespace RVS.Domain.DTOs;

/// <summary>
/// Vehicle/asset information associated with a service request.
/// </summary>
public sealed record AssetInfoDto
{
    public required string AssetId { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public int? Year { get; init; }

    /// <summary>
    /// The kind of RV (Spec A-3, issue #865): pre-filled from the VIN decode, chosen or changed by
    /// the customer on Step 4, edited by the dealer. Optional; sent as the enum name.
    /// </summary>
    public RvBodyType? BodyType { get; init; }
}
