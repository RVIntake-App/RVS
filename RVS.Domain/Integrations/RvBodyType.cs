using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace RVS.Domain.Integrations;

/// <summary>
/// The kind of RV, as the service department talks about it. Inferred from a VIN decode by
/// <see cref="RvBodyTypeClassifier"/>; <see cref="TruckCamper"/> is never inferred, since a truck
/// camper carries a serial number rather than a VIN (issue #854). The customer can pick or change
/// it on Step 4, and a dealer can edit it in the Manager.
/// <para>
/// Stored and sent as the member name (<c>FifthWheel</c>), by both serializers, so the display
/// text can change without a data migration (issue #865). Renaming a member is a migration.
/// </para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RvBodyType>))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum RvBodyType
{
    TravelTrailer,
    FifthWheel,
    ToyHauler,
    PopUpCamper,
    ParkModel,
    ClassA,
    ClassB,
    ClassC,
    TruckCamper
}

/// <summary>Customer-facing names for <see cref="RvBodyType"/>.</summary>
public static class RvBodyTypeExtensions
{
    /// <summary>Returns the name shown to the customer, e.g. "Fifth Wheel" or "Class A Motorhome".</summary>
    public static string ToDisplayName(this RvBodyType type) => type switch
    {
        RvBodyType.TravelTrailer => "Travel Trailer",
        RvBodyType.FifthWheel => "Fifth Wheel",
        RvBodyType.ToyHauler => "Toy Hauler",
        RvBodyType.PopUpCamper => "Pop-Up Camper",
        RvBodyType.ParkModel => "Park Model",
        RvBodyType.ClassA => "Class A Motorhome",
        RvBodyType.ClassB => "Class B Camper Van",
        RvBodyType.ClassC => "Class C Motorhome",
        RvBodyType.TruckCamper => "Truck Camper",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
}
