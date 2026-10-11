using System.Text.RegularExpressions;

namespace RVS.Domain.Integrations;

/// <summary>
/// The NHTSA vPIC fields that describe a vehicle's body, as returned by <c>DecodeVinValues</c>.
/// </summary>
/// <param name="VehicleType">vPIC <c>VehicleType</c>, e.g. "TRAILER", "INCOMPLETE VEHICLE".</param>
/// <param name="BodyClass">vPIC <c>BodyClass</c>, e.g. "Trailer", "Incomplete - Cutaway".</param>
/// <param name="TrailerBodyType">vPIC <c>TrailerBodyType</c>, e.g. "Camping or Travel Trailer".</param>
/// <param name="TrailerType">vPIC <c>TrailerType</c>: the hitch ("Fifth Wheel", "Bumper Pull"), not the kind of RV.</param>
/// <param name="OtherTrailerInfo">vPIC <c>OtherTrailerInfo</c>: the maker's free text, e.g. "Fifth Wheel Travel Trailer".</param>
public sealed record VinBodyFields(
    string? VehicleType,
    string? BodyClass,
    string? TrailerBodyType,
    string? TrailerType,
    string? OtherTrailerInfo);

/// <summary>
/// Infers an <see cref="RvBodyType"/> from vPIC's body fields (Spec A-3, issue #854). Rules run from
/// most to least specific; when the fields don't say which kind of RV it is, the result is <c>null</c>
/// rather than a guess.
/// </summary>
public static class RvBodyTypeClassifier
{
    private const string CampingTrailer = "Camping or Travel Trailer";
    private const string HouseTrailer = "House Trailer";

    // Order matters: "Fifth Wheel Toy Hauler" is a toy hauler, "Fifth Wheel Travel Trailer" a fifth wheel.
    private static readonly (Regex Pattern, RvBodyType Type)[] OtherTrailerInfoKeywords =
    [
        (Keyword(@"toy\s*hauler"), RvBodyType.ToyHauler),
        (Keyword(@"(fifth|5th)\s*wheel"), RvBodyType.FifthWheel),
        (Keyword(@"pop[\s-]?up|tent|folding"), RvBodyType.PopUpCamper),
        (Keyword(@"park\s*model"), RvBodyType.ParkModel),
        (Keyword(@"travel\s*trailer"), RvBodyType.TravelTrailer),
    ];

    private static readonly string[] FifthWheelHitches = ["Fifth Wheel", "Gooseneck", "Kingpin"];

    /// <summary>Infers the kind of RV, or returns <c>null</c> when the fields don't say.</summary>
    public static RvBodyType? Classify(VinBodyFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var otherTrailerInfo = Present(fields.OtherTrailerInfo);
        if (otherTrailerInfo is not null)
        {
            foreach (var (pattern, type) in OtherTrailerInfoKeywords)
            {
                if (pattern.IsMatch(otherTrailerInfo))
                {
                    return type;
                }
            }
        }

        var trailerBody = Present(fields.TrailerBodyType);
        // A camping trailer on a fifth-wheel-style hitch is a fifth wheel; any other is a travel trailer.
        if (Is(trailerBody, CampingTrailer))
        {
            var hitch = Present(fields.TrailerType);
            return FifthWheelHitches.Any(h => Is(hitch, h)) ? RvBodyType.FifthWheel : RvBodyType.TravelTrailer;
        }

        if (Is(trailerBody, HouseTrailer))
        {
            return RvBodyType.ParkModel;
        }

        // vPIC can't tell a Class A from a Class C by "Motorhome" alone, so only the chassis decides.
        var bodyClass = Present(fields.BodyClass);
        if (bodyClass is null)
        {
            return null;
        }

        if (Is(bodyClass, "Incomplete - Motor Home Chassis") || Is(bodyClass, "Incomplete - Stripped Chassis"))
        {
            return RvBodyType.ClassA;
        }

        if (Is(bodyClass, "Incomplete - Cutaway") || bodyClass.StartsWith("Incomplete - Chassis Cab", StringComparison.OrdinalIgnoreCase))
        {
            return RvBodyType.ClassC;
        }

        if (Is(bodyClass, "Van") || Is(bodyClass, "Cargo Van"))
        {
            return RvBodyType.ClassB;
        }

        return null;
    }

    private static Regex Keyword(string pattern) =>
        new($@"\b(?:{pattern})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static bool Is(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    /// <summary>A trimmed value, or <c>null</c> when vPIC left it blank or "Not Applicable".</summary>
    private static string? Present(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) || Is(trimmed, "Not Applicable") ? null : trimmed;
    }
}
