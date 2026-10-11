using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RVS.API.Integrations;
using RVS.Domain.Integrations;

namespace RVS.API.Tests.Integrations;

public class NhtsaVinDecoderClientTests
{
    private readonly Mock<ILogger<NhtsaVinDecoderClient>> _loggerMock = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DecodeVinAsync_WhenVinIsNullOrWhiteSpace_ShouldThrowArgumentException(string? vin)
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK));
        var act = () => client.DecodeVinAsync(vin!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DecodeVinAsync_WhenApiReturnsValidData_ShouldReturnVinDecoderResult()
    {
        var json = """
        {
            "Results": [{
                "Make": "Grand Design",
                "Model": "Momentum 395MS",
                "ModelYear": "2024",
                "Manufacturer": "Grand Design RV"
            }]
        }
        """;

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };

        var client = CreateClient(httpResponse);
        var result = await client.DecodeVinAsync("1RGDE4428R1000001");

        result.Should().NotBeNull();
        result!.Vin.Should().Be("1RGDE4428R1000001");
        result.Manufacturer.Should().Be("Grand Design");
        result.Model.Should().Be("Momentum 395MS");
        result.Year.Should().Be(2024);
    }

    [Fact]
    public async Task DecodeVinAsync_WhenErrorCodeIsZero_ShouldBeAnExactMatch()
    {
        // Shape of a clean vPIC DecodeVinValues response, trimmed to the fields the client reads.
        var json = """
        {
            "Count": 1,
            "Message": "Results returned successfully.",
            "Results": [{
                "ErrorCode": "0",
                "ErrorText": "0 - VIN decoded clean. Check Digit (9th position) is correct",
                "Make": "GRAND DESIGN",
                "Manufacturer": "GRAND DESIGN RV, LLC",
                "Model": "Momentum",
                "ModelYear": "2024",
                "Series": "395MS",
                "Trim": "",
                "VehicleType": "TRAILER",
                "BodyClass": "Trailer",
                "TrailerBodyType": "Camping or Travel Trailer",
                "TrailerType": "Fifth Wheel"
            }]
        }
        """;

        var result = await CreateClient(JsonResponse(json)).DecodeVinAsync("573FM4227R1000001");

        result.Should().NotBeNull();
        result!.IsExactMatch.Should().BeTrue();
        result.Manufacturer.Should().Be("GRAND DESIGN");
        result.Model.Should().Be("Momentum");
        result.Year.Should().Be(2024);
        result.BodyType.Should().Be(RvBodyType.FifthWheel);
    }

    [Fact]
    public async Task DecodeVinAsync_WithTheLiveArcticFoxResponse_ShouldReturnACleanNashDecode()
    {
        // Issue #854: captured from vPIC. ErrorCode 0, yet the make is NASH, not Arctic Fox. The
        // client reports what vPIC says; the intake wizard keeps the plate's brand over it.
        var result = await CreateClient(JsonResponse(VpicFixtures.ArcticFox2021)).DecodeVinAsync("4N15Z3525M0XXXXXX");

        result.Should().NotBeNull();
        result!.IsExactMatch.Should().BeTrue();
        result.Manufacturer.Should().Be("NASH");
        result.Model.Should().Be("Northwood Investments Corporation");
        result.Year.Should().Be(2021);
        result.BodyType.Should().Be(RvBodyType.FifthWheel);
    }

    [Fact]
    public async Task DecodeVinAsync_WhenErrorCodeIsNonZero_ShouldStillReturnTheMakeButNotAsAnExactMatch()
    {
        var json = """
        {
            "Count": 1,
            "Message": "Results returned successfully.",
            "Results": [{
                "ErrorCode": "1",
                "ErrorText": "1 - Check Digit (9th position) does not calculate properly",
                "Make": "NASH",
                "Manufacturer": "NORTHWOOD MANUFACTURING, INC. ",
                "Model": "",
                "ModelYear": "2021",
                "Series": "",
                "Trim": "",
                "VehicleType": "TRAILER",
                "BodyClass": "Trailer",
                "TrailerType": "Bumper Pull"
            }]
        }
        """;

        var result = await CreateClient(JsonResponse(json)).DecodeVinAsync("4N15Z3525M0XXXXXX");

        result.Should().NotBeNull();
        result!.IsExactMatch.Should().BeFalse();
        result.Manufacturer.Should().Be("NASH");
        result.Year.Should().Be(2021);
    }

    [Fact]
    public async Task DecodeVinAsync_WhenErrorCodeIsMissing_ShouldNotBeAnExactMatch()
    {
        var json = """{"Results": [{"Make": "JAYCO", "Model": "Eagle", "ModelYear": "2022"}]}""";

        var result = await CreateClient(JsonResponse(json)).DecodeVinAsync("1UJCJ0BT5N1000001");

        result!.IsExactMatch.Should().BeFalse();
    }

    [Theory]
    [InlineData("Momentum", "395MS", "Base", "Momentum")]
    [InlineData("", "395MS", "Base", "395MS")]
    [InlineData("", "  ", "Base", "Base")]
    [InlineData(null, null, null, "")]
    public async Task DecodeVinAsync_WhenModelIsBlank_ShouldFallBackToSeriesThenTrim(
        string? model, string? series, string? trim, string expected)
    {
        var json = JsonSerializer.Serialize(new
        {
            Results = new[]
            {
                new { ErrorCode = "0", Make = "GRAND DESIGN", Model = model, Series = series, Trim = trim, ModelYear = "2024" }
            }
        });

        var result = await CreateClient(JsonResponse(json)).DecodeVinAsync("573FM4227R1000001");

        result!.Model.Should().Be(expected);
    }

    [Theory]
    [InlineData("TRAILER", "Trailer", "Camping or Travel Trailer", "", "Toy Hauler", RvBodyType.ToyHauler)]
    [InlineData("TRAILER", "Trailer", "Camping or Travel Trailer", "Gooseneck", "", RvBodyType.FifthWheel)]
    [InlineData("TRAILER", "Trailer", "House Trailer", "", "", RvBodyType.ParkModel)]
    [InlineData("INCOMPLETE VEHICLE", "Incomplete - Cutaway", "Not Applicable", "Not Applicable", "", RvBodyType.ClassC)]
    [InlineData("MULTIPURPOSE PASSENGER VEHICLE (MPV)", "Motorhome", "Not Applicable", "Not Applicable", "", null)]
    [InlineData(null, null, null, null, null, null)]
    public async Task DecodeVinAsync_ShouldClassifyTheBodyTypeFromEveryVpicBodyField(
        string? vehicleType, string? bodyClass, string? trailerBodyType, string? trailerType, string? otherTrailerInfo, RvBodyType? expected)
    {
        var json = JsonSerializer.Serialize(new
        {
            Results = new[]
            {
                new { ErrorCode = "0", Make = "WINNEBAGO", Model = "Vista", ModelYear = "2023", VehicleType = vehicleType, BodyClass = bodyClass, TrailerBodyType = trailerBodyType, TrailerType = trailerType, OtherTrailerInfo = otherTrailerInfo }
            }
        });

        var result = await CreateClient(JsonResponse(json)).DecodeVinAsync("1F66F5DY0P0A00001");

        result!.BodyType.Should().Be(expected);
    }

    [Fact]
    public async Task DecodeVinAsync_WhenApiReturnsNoResults_ShouldReturnNull()
    {
        var json = """{"Results": []}""";
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };

        var client = CreateClient(httpResponse);
        var result = await client.DecodeVinAsync("1RGDE4428R1000001");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DecodeVinAsync_WhenApiReturnsEmptyMake_ShouldReturnNull()
    {
        var json = """{"Results": [{"Make": "", "Model": "", "ModelYear": ""}]}""";
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };

        var client = CreateClient(httpResponse);
        var result = await client.DecodeVinAsync("1RGDE4428R1000001");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DecodeVinAsync_WhenHttpRequestFails_ShouldReturnNull()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://vpic.nhtsa.dot.gov/api/")
        };

        var client = new NhtsaVinDecoderClient(httpClient, _loggerMock.Object);
        var result = await client.DecodeVinAsync("1RGDE4428R1000001");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DecodeVinAsync_WhenTaskCanceled_ShouldReturnNull()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://vpic.nhtsa.dot.gov/api/")
        };

        var client = new NhtsaVinDecoderClient(httpClient, _loggerMock.Object);
        var result = await client.DecodeVinAsync("1RGDE4428R1000001");

        result.Should().BeNull();
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
    };

    private NhtsaVinDecoderClient CreateClient(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://vpic.nhtsa.dot.gov/api/")
        };

        return new NhtsaVinDecoderClient(httpClient, _loggerMock.Object);
    }
}
