using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;

namespace RVS.API.Controllers;

/// <summary>
/// Customer-facing status page accessed via magic-link token (<c>Spec X-1</c>).
/// All routes are anonymous — no authentication required — and rate-limited per IP.
/// The response carries the unit, issue category, submission date, current status, the servicing location's
/// name, logo and phone number, and any manager-authored status note (<c>Spec C-9</c>). It is display-only for
/// the customer: there is no inbound path here — no reply, no message, no file upload.
/// </summary>
[ApiController]
[Route("api/status")]
[AllowAnonymous]
[EnableRateLimiting("StatusEndpoint")]
public class StatusController : ControllerBase
{
    private readonly IGlobalCustomerAcctService _globalCustomerAcctService;
    private readonly ICustomerProfileService _customerProfileService;
    private readonly IServiceRequestService _serviceRequestService;
    private readonly ILocationService _locationService;
    private readonly IStatusLinkService _statusLinkService;

    /// <summary>
    /// Initializes a new instance of <see cref="StatusController"/>.
    /// </summary>
    public StatusController(
        IGlobalCustomerAcctService globalCustomerAcctService,
        ICustomerProfileService customerProfileService,
        IServiceRequestService serviceRequestService,
        ILocationService locationService,
        IStatusLinkService statusLinkService)
    {
        _globalCustomerAcctService = globalCustomerAcctService;
        _customerProfileService = customerProfileService;
        _serviceRequestService = serviceRequestService;
        _locationService = locationService;
        _statusLinkService = statusLinkService;
    }

    /// <summary>
    /// Emails the customer the link to their status page (issue #862), for a customer who no
    /// longer has it. Always <c>202 Accepted</c> for a well-formed address, whether or not it is
    /// on file, so the endpoint cannot be used to learn who is a customer. Rate-limited more
    /// tightly than the status read, since each call can send an email.
    /// </summary>
    /// <param name="request">The customer's email address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <example>
    /// POST /api/status/link  { "email": "jane@example.com" }
    /// </example>
    [HttpPost("link")]
    [EnableRateLimiting("StatusLinkEndpoint")]
    public async Task<IActionResult> RequestStatusLink([FromBody] StatusLinkRequestDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var emailValidation = EmailValidator.Validate(request.Email);
        if (!emailValidation.IsValid)
        {
            ModelState.AddModelError(nameof(request.Email), emailValidation.ErrorMessage!);
            return UnprocessableEntity(ModelState);
        }

        await _statusLinkService.SendStatusLinkAsync(request.Email, ct);

        return Accepted();
    }

    /// <summary>
    /// Returns the customer's service request status across all dealerships.
    /// Validates the magic-link token and retrieves one minimal summary per request:
    /// unit, issue category, submission date, current status, and the servicing location's name, logo
    /// and phone number (<c>Spec X-1</c>; logo issue #793). Each location is looked up once, however
    /// many of the customer's requests it services. Free-text problem descriptions are never included, nor logged.
    /// </summary>
    /// <param name="token">Magic-link token for customer identification.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <example>
    /// GET /api/status/abc123def456
    /// </example>
    [HttpGet("{token}")]
    public async Task<ActionResult<CustomerStatusResponseDto>> GetStatus(string token, CancellationToken ct)
    {
        var acct = await _globalCustomerAcctService.ValidateMagicLinkTokenAsync(token, ct);

        var serviceRequests = new List<CustomerStatusItemResponseDto>();
        var locations = new Dictionary<(string TenantId, string LocationId), Location?>();

        foreach (var link in acct.LinkedProfiles)
        {
            var profile = await _customerProfileService.GetByIdAsync(link.TenantId, link.ProfileId, ct);

            foreach (var srId in profile.ServiceRequestIds)
            {
                var sr = await _serviceRequestService.GetByIdAsync(link.TenantId, srId, ct);
                var key = (link.TenantId, sr.LocationId);
                if (!locations.TryGetValue(key, out var location))
                {
                    location = await ResolveLocationAsync(link.TenantId, sr.LocationId, ct);
                    locations[key] = location;
                }

                serviceRequests.Add(sr.ToCustomerStatusItemDto(location));
            }
        }

        return Ok(new CustomerStatusResponseDto
        {
            ServiceRequests = serviceRequests,
            MagicLinkExpiresAtUtc = acct.MagicLinkExpiresAtUtc
        });
    }

    private async Task<Location?> ResolveLocationAsync(string tenantId, string locationId, CancellationToken ct)
    {
        try
        {
            return await _locationService.GetByIdAsync(tenantId, locationId, ct);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }
}
