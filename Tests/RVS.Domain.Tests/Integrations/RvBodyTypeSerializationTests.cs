using System.Text.Json;
using FluentAssertions;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;

namespace RVS.Domain.Tests.Integrations;

/// <summary>
/// The body type is stored and sent as the enum name, so its display text can change without a
/// data migration (issue #865).
/// </summary>
public class RvBodyTypeSerializationTests
{
    [Fact]
    public void SystemTextJson_ShouldWriteTheEnumName()
    {
        var json = JsonSerializer.Serialize(new AssetInfoDto { AssetId = "X", BodyType = RvBodyType.FifthWheel });

        json.Should().Contain("\"FifthWheel\"");
    }

    [Fact]
    public void SystemTextJson_ShouldReadTheEnumName()
    {
        var dto = JsonSerializer.Deserialize<AssetInfoDto>("""{"AssetId":"X","BodyType":"ClassC"}""");

        dto!.BodyType.Should().Be(RvBodyType.ClassC);
    }

    [Fact]
    public void SystemTextJson_WhenAbsent_ShouldReadNull()
    {
        var dto = JsonSerializer.Deserialize<AssetInfoDto>("""{"AssetId":"X"}""");

        dto!.BodyType.Should().BeNull();
    }

    [Fact]
    public void Newtonsoft_ShouldStoreTheEnumNameOnTheRequest()
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(new AssetInfoEmbedded { AssetId = "X", BodyType = RvBodyType.ToyHauler });

        json.Should().Contain("\"bodyType\":\"ToyHauler\"");
    }

    [Fact]
    public void Newtonsoft_ShouldReadTheEnumNameOffTheRequest()
    {
        var asset = Newtonsoft.Json.JsonConvert.DeserializeObject<AssetInfoEmbedded>("""{"assetId":"X","bodyType":"TruckCamper"}""");

        asset!.BodyType.Should().Be(RvBodyType.TruckCamper);
    }

    [Fact]
    public void Newtonsoft_ShouldStoreTheEnumNameOnTheLedger()
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(new AssetLedgerEntry { AssetId = "X", BodyType = RvBodyType.ParkModel });

        json.Should().Contain("\"bodyType\":\"ParkModel\"");
    }

    [Fact]
    public void Newtonsoft_WhenARequestPredatesTheField_ShouldReadNull()
    {
        var asset = Newtonsoft.Json.JsonConvert.DeserializeObject<AssetInfoEmbedded>("""{"assetId":"X","manufacturer":"Jayco"}""");

        asset!.BodyType.Should().BeNull();
    }
}
