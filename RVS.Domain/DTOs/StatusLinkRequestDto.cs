namespace RVS.Domain.DTOs;

/// <summary>
/// Request body for emailing a customer their status link (<c>Spec X-1</c>, issue #862). The
/// response never says whether <see cref="Email"/> is on file.
/// </summary>
public sealed record StatusLinkRequestDto
{
    /// <summary>The email address the customer gave on their service request.</summary>
    public string Email { get; init; } = string.Empty;
}
