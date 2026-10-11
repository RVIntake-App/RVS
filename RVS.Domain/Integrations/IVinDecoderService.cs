namespace RVS.Domain.Integrations;

/// <summary>
/// Decodes a Vehicle Identification Number (VIN) using the NHTSA vPIC API to
/// retrieve manufacturer, model, and year information.
/// </summary>
public interface IVinDecoderService
{
    /// <summary>
    /// Decodes a VIN and returns the resolved vehicle details.
    /// </summary>
    /// <param name="vin">17-character Vehicle Identification Number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decoded vehicle information, or <c>null</c> if the VIN could not be resolved.</returns>
    Task<VinDecoderResult?> DecodeVinAsync(string vin, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a VIN decode operation from the NHTSA vPIC API.
/// </summary>
/// <param name="Vin">The original VIN that was decoded.</param>
/// <param name="Manufacturer">
/// Vehicle brand (vPIC <c>Make</c>). This is the brand registered to the maker's WMI, which can be the
/// wrong brand of a multi-brand maker even on a clean decode (an Arctic Fox decodes as NASH, issue #854).
/// </param>
/// <param name="Model">Vehicle model name, or the series or trim when vPIC has no model.</param>
/// <param name="Year">Model year; 0 when vPIC returned none.</param>
/// <param name="BodyType">The kind of RV inferred by <see cref="RvBodyTypeClassifier"/>, or <c>null</c> when vPIC's fields don't say.</param>
/// <param name="IsExactMatch">
/// <c>true</c> only when vPIC decoded the VIN cleanly (<c>ErrorCode</c> 0). A partial decode must not
/// override a year read off the data plate (Spec A-10).
/// </param>
public sealed record VinDecoderResult(string Vin, string Manufacturer, string Model, int Year, RvBodyType? BodyType, bool IsExactMatch);
