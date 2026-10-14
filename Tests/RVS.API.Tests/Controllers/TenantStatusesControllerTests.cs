using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RVS.API.Controllers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

namespace RVS.API.Tests.Controllers;

public class TenantStatusesControllerTests
{
    private const string TenantId = "ten_test";

    private readonly Mock<ITenantStatusService> _serviceMock = new();
    private readonly TenantStatusesController _sut;

    public TenantStatusesControllerTests()
    {
        _sut = new TenantStatusesController(_serviceMock.Object, BuildClaimsService(TenantId));
    }

    [Fact]
    public async Task Get_ShouldReturnTheTenantsVocabulary()
    {
        _serviceMock.Setup(s => s.GetStatusesAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StatusVocabulary.Build(null));

        var result = await _sut.Get(CancellationToken.None);

        var dto = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<TenantStatusesResponseDto>().Subject;
        dto.Statuses.Should().HaveCount(6);
    }

    [Fact]
    public async Task Update_ShouldPassTheRequestToTheServiceAndReturnTheResult()
    {
        var request = new TenantStatusesUpdateRequestDto
        {
            IntermediateStatuses = [new TenantStatusUpdateItemDto { Id = null, Name = "Diagnosing", Color = "blue" }],
        };
        _serviceMock.Setup(s => s.UpdateStatusesAsync(TenantId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StatusVocabulary.Build([new TenantStatusEmbedded { Id = "st_1", Name = "Diagnosing", Color = "blue" }]));

        var result = await _sut.Update(request, CancellationToken.None);

        var dto = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<TenantStatusesResponseDto>().Subject;
        dto.Statuses.Select(s => s.Id).Should().Equal("New", "st_1", "Completed", "Cancelled");
        dto.CanEdit.Should().BeTrue("only a tenant-config manager reaches the update action");
    }

    [Fact]
    public async Task Get_WhenUserCanUpdateTenantConfig_ShouldSetCanEdit()
    {
        var sut = new TenantStatusesController(_serviceMock.Object,
            BuildClaimsService(TenantId, new Claim("permissions", "tenants:config:update")));
        _serviceMock.Setup(s => s.GetStatusesAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StatusVocabulary.Build(null));

        var result = await sut.Get(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<TenantStatusesResponseDto>().Subject
            .CanEdit.Should().BeTrue();
    }

    [Fact]
    public async Task Get_WhenUserCannotUpdateTenantConfig_ShouldNotSetCanEdit()
    {
        _serviceMock.Setup(s => s.GetStatusesAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StatusVocabulary.Build(null));

        var result = await _sut.Get(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<TenantStatusesResponseDto>().Subject
            .CanEdit.Should().BeFalse();
    }

    [Fact]
    public async Task Get_WhenUserHoldsOnlyTenantConfigRead_ShouldNotSetCanEdit()
    {
        var sut = new TenantStatusesController(_serviceMock.Object,
            BuildClaimsService(TenantId, new Claim("permissions", "tenants:config:read")));
        _serviceMock.Setup(s => s.GetStatusesAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StatusVocabulary.Build(null));

        var result = await sut.Get(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<TenantStatusesResponseDto>().Subject
            .CanEdit.Should().BeFalse();
    }

    [Fact]
    public void Get_ShouldBeReadableByAnyoneWhoCanReadServiceRequests()
    {
        PolicyOf(nameof(TenantStatusesController.Get)).Should().Be("CanReadServiceRequests");
    }

    [Fact]
    public void Update_ShouldRequireTheTenantConfigUpdatePermission()
    {
        PolicyOf(nameof(TenantStatusesController.Update)).Should().Be("CanUpdateTenantConfig");
    }

    private static string? PolicyOf(string action) =>
        typeof(TenantStatusesController).GetMethod(action)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Policy;

    private static ClaimsService BuildClaimsService(string tenantId, params Claim[] extraClaims)
    {
        var claims = new List<Claim>
        {
            new(ClaimsService.TenantIdClaimType, tenantId),
            new(ClaimTypes.NameIdentifier, "usr_test")
        };
        claims.AddRange(extraClaims);
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new ClaimsService(accessor.Object);
    }
}
