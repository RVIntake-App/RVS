using System.Text;
using MudBlazor;
using RVS.Domain.Validation;

namespace RVS.Blazor.Intake.Pages;

/// <summary>
/// Pure presentation helpers for the customer status page (<c>Spec X-1</c>): turning a
/// stored service-request status into a short customer-facing sentence and a status chip
/// (label, color, icon — issue #741), and a free-form
/// location phone string into a display form and a <c>tel:</c> dial target.
/// No customer identity, issue text, or conversation is involved — this is formatting only.
/// </summary>
public static class CustomerStatusFormatting
{
    private const string GenericStatusDescription = "Your request is being processed.";

    /// <summary>
    /// Whether the dealer has given this status its own name (<c>Spec C-8</c>, issue #855): a
    /// status they added, or a default they renamed. The fixed <c>New</c>, <c>Completed</c> and
    /// <c>Cancelled</c> never are, and an unrenamed default keeps this page's own wording.
    /// </summary>
    private static bool IsDealerNamed(string? status, string? dealerLabel)
    {
        if (string.IsNullOrWhiteSpace(status) || string.IsNullOrWhiteSpace(dealerLabel)
            || StatusVocabulary.IsFixed(status.Trim()))
        {
            return false;
        }

        var defaultName = StatusVocabulary.DefaultIntermediateStatuses()
            .FirstOrDefault(s => string.Equals(s.Id, status.Trim(), StringComparison.Ordinal))?.Name;

        return !string.Equals(defaultName, dealerLabel.Trim(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The one-line description, given the dealer's name for the status. A dealer-named
    /// status gets the generic line, since this page cannot know what the name means.
    /// </summary>
    public static string DescribeStatus(string? status, string? dealerLabel) =>
        IsDealerNamed(status, dealerLabel) ? GenericStatusDescription : DescribeStatus(status);

    /// <summary>The chip label, given the dealer's name for the status: the dealer's name when they chose one.</summary>
    public static string GetStatusLabel(string? status, string? dealerLabel) =>
        IsDealerNamed(status, dealerLabel) ? dealerLabel!.Trim() : GetStatusLabel(status);

    /// <summary>
    /// The chip color, given the dealer's name for the status. A dealer-named status sits
    /// between New and Completed, so it reads as open work, like the default holds.
    /// </summary>
    public static Color GetStatusColor(string? status, string? dealerLabel) =>
        IsDealerNamed(status, dealerLabel) ? Color.Warning : GetStatusColor(status);

    /// <summary>
    /// Maps a stored status value to a one-line customer-facing description. Recognises the
    /// decided vocabulary (<c>Spec C-3</c> / C-8): <c>New</c>, <c>InProgress</c>,
    /// <c>WaitingOnParts</c>, <c>WaitingOnCustomer</c>, <c>Completed</c>, <c>Cancelled</c>
    /// (spacing and hyphens are ignored, so legacy "In Progress" style values still match).
    /// Anything unrecognised, null, or blank gets a safe generic line.
    /// </summary>
    public static string DescribeStatus(string? status) => Canonicalize(status) switch
    {
        "new" => "Your request has been received and is awaiting review by a service advisor.",
        "inprogress" => "A technician is actively working on your service request.",
        "waitingonparts" => "Your repair requires parts that have been ordered. We'll update you when they arrive.",
        "waitingoncustomer" => "We're waiting on information or approval from you before work can continue. Please contact the dealership.",
        "completed" => "Your service has been completed. Please contact the dealership for pickup details.",
        "cancelled" => "This service request has been cancelled.",
        _ => GenericStatusDescription
    };

    /// <summary>
    /// Short label for the status chip. Recognised statuses get a spaced, customer-facing form
    /// (<c>WaitingOnCustomer</c> reads "Waiting on You"); an unrecognised value is shown as
    /// stored, and a null or blank one as "Processing".
    /// </summary>
    public static string GetStatusLabel(string? status) => Canonicalize(status) switch
    {
        "new" => "New",
        "inprogress" => "In Progress",
        "waitingonparts" => "Waiting on Parts",
        "waitingoncustomer" => "Waiting on You",
        "completed" => "Completed",
        "cancelled" => "Cancelled",
        "" => "Processing",
        _ => status!.Trim()
    };

    /// <summary>
    /// Chip color for a status — the same colors the manager service board uses for its
    /// columns, so a status reads the same on both sides of the counter.
    /// </summary>
    public static Color GetStatusColor(string? status) => Canonicalize(status) switch
    {
        "new" => Color.Info,
        "inprogress" or "waitingonparts" or "waitingoncustomer" => Color.Warning,
        "completed" => Color.Success,
        _ => Color.Default
    };

    /// <summary>
    /// Chip icon for a status. Several statuses share a color, so the icon (and the label) is
    /// what tells them apart — never color alone.
    /// </summary>
    public static string GetStatusIcon(string? status) => Canonicalize(status) switch
    {
        "new" => Icons.Material.Filled.FiberNew,
        "inprogress" => Icons.Material.Filled.Autorenew,
        "waitingonparts" => Icons.Material.Filled.HourglassEmpty,
        "waitingoncustomer" => Icons.Material.Filled.ContactPhone,
        "completed" => Icons.Material.Filled.CheckCircle,
        "cancelled" => Icons.Material.Filled.Cancel,
        _ => Icons.Material.Filled.Info
    };

    /// <summary>
    /// Formats a location phone string for display. A 10-digit North-American number becomes
    /// <c>(NXX) NXX-XXXX</c>; an 11-digit number starting with <c>1</c> becomes
    /// <c>+1 (NXX) NXX-XXXX</c>. Anything else is returned trimmed and otherwise unchanged.
    /// Returns <c>null</c> when there is no usable value.
    /// </summary>
    public static string? FormatPhoneForDisplay(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var trimmed = phone.Trim();
        var digits = DigitsOnly(trimmed);

        return digits.Length switch
        {
            10 => $"({digits[..3]}) {digits[3..6]}-{digits[6..]}",
            11 when digits[0] == '1' => $"+1 ({digits[1..4]}) {digits[4..7]}-{digits[7..]}",
            _ => trimmed
        };
    }

    /// <summary>
    /// Builds the <c>tel:</c> URI for a phone string. A leading <c>+</c> is preserved; a bare
    /// 10-digit number is assumed North-American and gets <c>+1</c>; an 11-digit number
    /// starting with <c>1</c> gets a <c>+</c>. Other digit strings are dialled as-is.
    /// Returns <c>null</c> when the input contains no digits.
    /// </summary>
    public static string? ToTelHref(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var trimmed = phone.Trim();
        var digits = DigitsOnly(trimmed);

        if (digits.Length == 0)
        {
            return null;
        }

        if (trimmed.StartsWith('+'))
        {
            return $"tel:+{digits}";
        }

        return digits.Length switch
        {
            10 => $"tel:+1{digits}",
            11 when digits[0] == '1' => $"tel:+{digits}",
            _ => $"tel:{digits}"
        };
    }

    private static string Canonicalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(status.Length);
        foreach (var ch in status)
        {
            if (ch is ' ' or '-' or '_')
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    private static string DigitsOnly(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
