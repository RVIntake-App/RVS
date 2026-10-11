using System.Net;

namespace RVS.API.Integrations;

/// <summary>
/// Text for the status-link email a customer asks for on <c>/status</c> (issue #862). Anyone can
/// type an address there, so the email says why it was sent and that an owner who did not ask
/// can ignore it. It names no dealer: one link covers the customer's requests at every dealer.
/// Built by <see cref="Services.StatusLinkService"/>; the name is HTML-encoded in the HTML body.
/// </summary>
internal static class StatusLinkEmailContent
{
    public const string Subject = "Your RV service request status link";

    public static string BuildHtmlBody(string? customerFirstName, string statusUrl, int? expiresInDays) =>
        $"<p>{Greeting(customerFirstName is null ? null : WebUtility.HtmlEncode(customerFirstName))}</p>" +
        $"<p>{Intro}</p>" +
        $"<p><a href=\"{WebUtility.HtmlEncode(statusUrl)}\">Check request status</a></p>" +
        $"<p>{ReasonSentence(expiresInDays)}</p>";

    /// <summary>
    /// The same message for the plain-text alternative (issue #829), with the status link bare on
    /// its own line.
    /// </summary>
    public static string BuildPlainTextBody(string? customerFirstName, string statusUrl, int? expiresInDays) =>
        string.Join("\n\n", Greeting(customerFirstName), Intro, statusUrl, ReasonSentence(expiresInDays));

    private const string Intro = "Use the link below to check the status of your RV service requests at any time:";

    private static string Greeting(string? customerFirstName) =>
        string.IsNullOrWhiteSpace(customerFirstName) ? "Hi," : $"Hi {customerFirstName.Trim()},";

    private static string ReasonSentence(int? expiresInDays)
    {
        var expiry = expiresInDays is { } days
            ? $" This link expires in {days} {(days == 1 ? "day" : "days")}."
            : string.Empty;
        return "You're receiving this email from RV Intake because someone asked for a status link for this email address." +
               $"{expiry} If that wasn't you, you can ignore this email.";
    }
}
