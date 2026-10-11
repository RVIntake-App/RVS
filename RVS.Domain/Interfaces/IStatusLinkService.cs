namespace RVS.Domain.Interfaces;

/// <summary>
/// Emails a customer the link to their status page (<c>Spec X-1</c>, issue #862), so they can
/// reach it from <c>/status</c> without the confirmation number. Anonymous and cross-tenant: the
/// link belongs to the <see cref="Entities.GlobalCustomerAcct"/>, which is keyed by email.
/// </summary>
public interface IStatusLinkService
{
    /// <summary>
    /// Emails the status link to <paramref name="email"/> when an account with service requests
    /// has that address, minting a new token when the account has none or it has expired. Does
    /// nothing, silently, when no such account exists or a link went to it within the resend
    /// cooldown, so the caller can never learn whether an address is on file. The send itself is
    /// not awaited and never throws.
    /// </summary>
    /// <param name="email">The address the customer typed, in any case or padding.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException"><paramref name="email"/> is null, empty or whitespace.</exception>
    Task SendStatusLinkAsync(string email, CancellationToken cancellationToken = default);
}
