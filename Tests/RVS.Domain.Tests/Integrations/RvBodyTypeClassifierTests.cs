using FluentAssertions;
using RVS.Domain.Integrations;

namespace RVS.Domain.Tests.Integrations;

public class RvBodyTypeClassifierTests
{
    private const string Camping = "Camping or Travel Trailer";

    [Fact]
    public void Classify_WithTheLiveArcticFoxFields_ShouldBeAFifthWheel()
    {
        // Issue #854: TrailerType is empty; only OtherTrailerInfo says fifth wheel.
        var fields = new VinBodyFields("TRAILER", "Trailer", Camping, "", "Fifth Wheel Travel Trailer");

        RvBodyTypeClassifier.Classify(fields).Should().Be(RvBodyType.FifthWheel);
    }

    [Theory]
    [InlineData("Fifth Wheel Toy Hauler", RvBodyType.ToyHauler)]
    [InlineData("TOY HAULER", RvBodyType.ToyHauler)]
    [InlineData("5th Wheel", RvBodyType.FifthWheel)]
    [InlineData("Fifth Wheel Travel Trailer", RvBodyType.FifthWheel)]
    [InlineData("Pop-Up Camper", RvBodyType.PopUpCamper)]
    [InlineData("Popup", RvBodyType.PopUpCamper)]
    [InlineData("Folding Camping Trailer", RvBodyType.PopUpCamper)]
    [InlineData("Tent Trailer", RvBodyType.PopUpCamper)]
    [InlineData("Park Model", RvBodyType.ParkModel)]
    [InlineData("Travel Trailer", RvBodyType.TravelTrailer)]
    public void Classify_ShouldReadOtherTrailerInfoFirst(string otherTrailerInfo, RvBodyType expected)
    {
        // The hitch says bumper pull, which the free text overrides.
        var fields = new VinBodyFields("TRAILER", "Trailer", Camping, "Bumper Pull", otherTrailerInfo);

        RvBodyTypeClassifier.Classify(fields).Should().Be(expected);
    }

    [Fact]
    public void Classify_ShouldNotMatchTentInsideAnotherWord()
    {
        var fields = new VinBodyFields("TRAILER", "Trailer", Camping, "", "Content trailer");

        RvBodyTypeClassifier.Classify(fields).Should().Be(RvBodyType.TravelTrailer);
    }

    [Theory]
    [InlineData("Fifth Wheel", RvBodyType.FifthWheel)]
    [InlineData("Gooseneck", RvBodyType.FifthWheel)]
    [InlineData("Kingpin", RvBodyType.FifthWheel)]
    [InlineData("Bumper Pull", RvBodyType.TravelTrailer)]
    [InlineData("Ball Type Pull", RvBodyType.TravelTrailer)]
    [InlineData("Ball Hitch", RvBodyType.TravelTrailer)]
    public void Classify_ForACampingTrailer_ShouldUseTheHitch(string trailerType, RvBodyType expected)
    {
        var fields = new VinBodyFields("TRAILER", "Trailer", Camping, trailerType, null);

        RvBodyTypeClassifier.Classify(fields).Should().Be(expected);
    }

    [Theory]
    [InlineData(Camping, RvBodyType.TravelTrailer)]
    [InlineData("House Trailer", RvBodyType.ParkModel)]
    public void Classify_ShouldFallBackToTheTrailerBody(string trailerBodyType, RvBodyType expected)
    {
        var fields = new VinBodyFields("TRAILER", "Trailer", trailerBodyType, "Not Applicable", "");

        RvBodyTypeClassifier.Classify(fields).Should().Be(expected);
    }

    [Theory]
    [InlineData("Car Hauler Trailer", "Gooseneck")]
    [InlineData("Utility Trailer", "Ball Hitch")]
    [InlineData("Boat Trailer", "")]
    [InlineData("", "Fifth Wheel")]
    public void Classify_WhenTheTrailerIsNotACamper_ShouldNotGuess(string trailerBodyType, string trailerType)
    {
        var fields = new VinBodyFields("TRAILER", "Trailer", trailerBodyType, trailerType, null);

        RvBodyTypeClassifier.Classify(fields).Should().BeNull();
    }

    [Theory]
    [InlineData("Incomplete - Motor Home Chassis", RvBodyType.ClassA)]
    [InlineData("Incomplete - Stripped Chassis", RvBodyType.ClassA)]
    [InlineData("Incomplete - Cutaway", RvBodyType.ClassC)]
    [InlineData("Incomplete - Chassis Cab (Single Cab)", RvBodyType.ClassC)]
    [InlineData("Incomplete - Chassis Cab (Number of Cab Unknown)", RvBodyType.ClassC)]
    [InlineData("Van", RvBodyType.ClassB)]
    [InlineData("Cargo Van", RvBodyType.ClassB)]
    public void Classify_ForAMotorizedRv_ShouldUseTheBodyClass(string bodyClass, RvBodyType expected)
    {
        var fields = new VinBodyFields("INCOMPLETE VEHICLE", bodyClass, "Not Applicable", "Not Applicable", null);

        RvBodyTypeClassifier.Classify(fields).Should().Be(expected);
    }

    [Theory]
    [InlineData("MULTIPURPOSE PASSENGER VEHICLE (MPV)", "Motorhome")]
    [InlineData("PASSENGER CAR", "Sedan/Saloon")]
    [InlineData("TRUCK", "Pickup")]
    [InlineData(null, null)]
    public void Classify_WhenTheFieldsDoNotSayWhichKind_ShouldBeNull(string? vehicleType, string? bodyClass)
    {
        var fields = new VinBodyFields(vehicleType, bodyClass, null, null, null);

        RvBodyTypeClassifier.Classify(fields).Should().BeNull();
    }

    [Fact]
    public void Classify_WhenFieldsIsNull_ShouldThrowArgumentNullException()
    {
        var act = () => RvBodyTypeClassifier.Classify(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(RvBodyType.TravelTrailer, "Travel Trailer")]
    [InlineData(RvBodyType.FifthWheel, "Fifth Wheel")]
    [InlineData(RvBodyType.ToyHauler, "Toy Hauler")]
    [InlineData(RvBodyType.PopUpCamper, "Pop-Up Camper")]
    [InlineData(RvBodyType.ParkModel, "Park Model")]
    [InlineData(RvBodyType.ClassA, "Class A Motorhome")]
    [InlineData(RvBodyType.ClassB, "Class B Camper Van")]
    [InlineData(RvBodyType.ClassC, "Class C Motorhome")]
    [InlineData(RvBodyType.TruckCamper, "Truck Camper")]
    public void ToDisplayName_ShouldReturnTheCustomerFacingName(RvBodyType type, string expected)
    {
        type.ToDisplayName().Should().Be(expected);
    }
}
