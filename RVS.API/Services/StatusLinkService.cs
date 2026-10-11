using Microsoft.Extensions.Options;
using RVS.API.Integrations;
using RVS.API.Options;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Emails a customer the link to their status page on request from <c>/status</c> (issue #862),
/// replacing the confirmation-number form. Whether the address is on file never reaches the
/// caller: a miss, an account without requests and a cooldown hit all return as a send does,
/// and the send is not awaited, so its latency does not tell either.
/// </summary>
public sealed class StatusLinkService : IStatusLinkService
{
    /// <summary>
    /// Minimum time between two emailed links to one account. The per-IP rate limit does not
    /// stop many callers flooding one inbox; this does.
    /// </summary>
    internal static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(5);

    /// <summary>Lifetime of a newly minted token, as at intake.</summary>
    private static readonly TimeSpan NewTokenLifetime = TimeSpan.FromDays(90);

    private readonly IGlobalCustomerAcctRepository _repository;
    private readonly INotificationService _emailService;
    private readonly IntakeUrlOptions _intakeUrlOptions;
    private readonly ILogger<StatusLinkService> _logger;

    public StatusLinkService(
        IGlobalCustomerAcctRepository repository,
        INotificationService emailService,
        IOptions<IntakeUrlOptions> intakeUrlOptions,
        ILogger<StatusLinkService> logger)
    {
        _repository = repository;
        _emailService = emailService;
        _intakeUrlOptions = intakeUrlOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendStatusLinkAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var account = await _repository.GetByEmailAsync(normalizedEmail, cancellationToken);

        // The address is PII and stays out of the logs; the account id identifies a hit.
        if (account is null || account.LinkedProfiles.Count == 0)
        {
            _logger.LogInformation("Status link requested for an address with no service requests; nothing sent");
            return;
        }

        var now = DateTime.UtcNow;
        if (account.StatusLinkEmailedAtUtc is { } lastSent && now - lastSent < ResendCooldown)
        {
            _logger.LogInformation("Status link for GlobalCustomerAcct {AcctId} was emailed within the cooldown; nothing sent", account.Id);
            return;
        }

        // Same rule as intake (#716): an unexpired token is reused with its expiry, so links the
        // customer already holds keep working.
        if (account.MagicLinkToken is null || (account.MagicLinkExpiresAtUtc is { } expiry && expiry <= now))
        {
            account.MagicLinkToken = GlobalCustomerAcctService.GenerateMagicLinkToken(normalizedEmail);
            account.MagicLinkExpiresAtUtc = now.Add(NewTokenLifetime);
        }

        account.StatusLinkEmailedAtUtc = now;
        account.MarkAsUpdated("status-link");
        await _repository.UpdateAsync(account, cancellationToken);

        var statusUrl = $"{_intakeUrlOptions.BaseUrl.TrimEnd('/')}/status/{account.MagicLinkToken}";
        int? expiresInDays = account.MagicLinkExpiresAtUtc is { } expiresAtUtc
            ? (int)Math.Ceiling((expiresAtUtc - now).TotalDays)
            : null;

        _ = SendAsync(account.Id, account.Email, account.FirstName, statusUrl, expiresInDays);
    }

    private async Task SendAsync(string accountId, string toEmail, string? firstName, string statusUrl, int? expiresInDays)
    {
        try
        {
            await _emailService.SendEmailAsync(
                toEmail,
                StatusLinkEmailContent.Subject,
                StatusLinkEmailContent.BuildHtmlBody(firstName, statusUrl, expiresInDays),
                StatusLinkEmailContent.BuildPlainTextBody(firstName, statusUrl, expiresInDays),
                CancellationToken.None);
            _logger.LogInformation("Emailed status link to GlobalCustomerAcct {AcctId}", accountId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to email status link to GlobalCustomerAcct {AcctId}", accountId);
        }
    }
}
