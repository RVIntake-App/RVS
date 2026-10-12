using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RVS.API.Mappers;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;

namespace RVS.API.Controllers;

/// <summary>
/// The tenant's service-request status vocabulary (<c>Spec C-8</c>, issue #855). Everyone who
/// can read service requests can read it, because the board, the list and the request pages
/// are built from it; only a holder of <c>tenants:config:update</c> can change it.
/// </summary>
[ApiController]
[Route("api/tenants/statuses")]
[Authorize]
public class TenantStatusesController : ControllerBase
{
    private readonly ITenantStatusService _tenantStatusService;
    private readonly ClaimsService _claimsService;

    /// <summary>
    /// Initializes a new instance of <see cref="TenantStatusesController"/>.
    /// </summary>
    public TenantStatusesController(ITenantStatusService tenantStatusService, ClaimsService claimsService)
    {
        _tenantStatusService = tenantStatusService;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Returns the tenant's statuses in board order: <c>New</c>, the tenant's own, <c>Completed</c>, <c>Cancelled</c>.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "CanReadServiceRequests")]
    public async Task<ActionResult<TenantStatusesResponseDto>> Get(CancellationToken cancellationToken)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var statuses = await _tenantStatusService.GetStatusesAsync(tenantId, cancellationToken);

        return Ok(statuses.ToResponseDto() with { CanEdit = _claimsService.CanUpdateTenantConfig() });
    }

    /// <summary>
    /// Replaces the tenant's statuses between <c>New</c> and <c>Completed</c>. Requests in a
    /// deleted status move to the status named for it in <c>reassignments</c>.
    /// </summary>
    [HttpPut]
    [Authorize(Policy = "CanUpdateTenantConfig")]
    public async Task<ActionResult<TenantStatusesResponseDto>> Update(
        [FromBody] TenantStatusesUpdateRequestDto request,
        CancellationToken cancellationToken)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var statuses = await _tenantStatusService.UpdateStatusesAsync(tenantId, request, cancellationToken);

        // The policy on this action already established the user may edit.
        return Ok(statuses.ToResponseDto() with { CanEdit = true });
    }
}
