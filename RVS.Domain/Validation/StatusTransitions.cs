namespace RVS.Domain.Validation;

/// <summary>
/// Enforces valid status transitions for ServiceRequest entities against the tenant's
/// vocabulary (<c>Spec C-8</c>). Transitions are unrestricted: any status may move to any other
/// status in the vocabulary, self-transitions excepted. The source status is not checked, so a
/// request left in a status the tenant has since deleted can always be moved on.
/// </summary>
public static class StatusTransitions
{
    /// <summary>
    /// Returns true if the transition from <paramref name="from"/> to <paramref name="to"/> is valid.
    /// </summary>
    /// <param name="from">The current status value.</param>
    /// <param name="to">The desired target status value.</param>
    /// <param name="statuses">The tenant's resolved vocabulary (<see cref="StatusVocabulary.Resolve"/>).</param>
    /// <returns><c>true</c> if the transition is allowed; otherwise <c>false</c>.</returns>
    public static bool IsValid(string from, string to, IReadOnlyList<StatusDefinition> statuses)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentNullException.ThrowIfNull(statuses);

        return !string.Equals(from, to, StringComparison.Ordinal)
            && statuses.Any(s => string.Equals(s.Id, to, StringComparison.Ordinal));
    }
}
