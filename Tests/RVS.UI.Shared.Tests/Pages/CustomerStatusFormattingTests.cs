using FluentAssertions;
using MudBlazor;
using RVS.Blazor.Intake.Pages;

namespace RVS.UI.Shared.Tests.Pages;

public class CustomerStatusFormattingTests
{
    // ---- The dealer's own status names (Spec C-8, issue #855) -------------------

    [Fact]
    public void GetStatusLabel_WithADealerAddedStatus_ShowsTheDealersName()
    {
        CustomerStatusFormatting.GetStatusLabel("st_diag", "Diagnosing").Should().Be("Diagnosing");
    }

    [Fact]
    public void GetStatusLabel_WithARenamedDefaultStatus_ShowsTheDealersName()
    {
        CustomerStatusFormatting.GetStatusLabel("InProgress", "Repairing").Should().Be("Repairing");
    }

    [Fact]
    public void GetStatusLabel_WithAnUnrenamedDefaultStatus_KeepsTheCustomerWording()
    {
        CustomerStatusFormatting.GetStatusLabel("WaitingOnCustomer", "Waiting on Customer").Should().Be("Waiting on You");
    }

    [Theory]
    [InlineData("New")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void GetStatusLabel_ForAFixedStatus_KeepsTheStandardWording(string status)
    {
        CustomerStatusFormatting.GetStatusLabel(status, "Anything").Should().Be(status);
    }

    [Fact]
    public void GetStatusLabel_WithNoDealerLabel_FallsBackToTheStoredValue()
    {
        CustomerStatusFormatting.GetStatusLabel("InProgress", null).Should().Be("In Progress");
    }

    [Fact]
    public void DescribeStatus_WithADealerNamedStatus_ReturnsTheGenericLine()
    {
        CustomerStatusFormatting.DescribeStatus("st_diag", "Diagnosing").Should().Be("Your request is being processed.");
        CustomerStatusFormatting.DescribeStatus("InProgress", "Repairing").Should().Be("Your request is being processed.");
    }

    [Fact]
    public void DescribeStatus_WithAnUnrenamedDefaultStatus_KeepsItsSentence()
    {
        CustomerStatusFormatting.DescribeStatus("InProgress", "In Progress")
            .Should().Be(CustomerStatusFormatting.DescribeStatus("InProgress"));
    }

    [Fact]
    public void GetStatusColor_WithADealerNamedStatus_ReadsAsOpenWork()
    {
        CustomerStatusFormatting.GetStatusColor("st_diag", "Diagnosing").Should().Be(Color.Warning);
    }

    [Fact]
    public void GetStatusColor_ForAFixedStatus_IsUnchanged()
    {
        CustomerStatusFormatting.GetStatusColor("Completed", "Completed").Should().Be(Color.Success);
    }

    // ---- DescribeStatus -------------------------------------------------------

    [Theory]
    [InlineData("New")]
    [InlineData(" new ")]
    public void DescribeStatus_New_ReturnsAwaitingReviewCopy(string status)
    {
        CustomerStatusFormatting.DescribeStatus(status)
            .Should().Be("Your request has been received and is awaiting review by a service advisor.");
    }

    [Theory]
    [InlineData("InProgress")]
    [InlineData("In Progress")]
    [InlineData("in-progress")]
    public void DescribeStatus_InProgress_ReturnsTechnicianWorkingCopy(string status)
    {
        CustomerStatusFormatting.DescribeStatus(status)
            .Should().Be("A technician is actively working on your service request.");
    }

    [Theory]
    [InlineData("WaitingOnParts")]
    [InlineData("Waiting on Parts")]
    public void DescribeStatus_WaitingOnParts_ReturnsPartsOrderedCopy(string status)
    {
        CustomerStatusFormatting.DescribeStatus(status)
            .Should().Be("Your repair requires parts that have been ordered. We'll update you when they arrive.");
    }

    [Theory]
    [InlineData("WaitingOnCustomer")]
    [InlineData("Waiting on Customer")]
    public void DescribeStatus_WaitingOnCustomer_ReturnsWaitingOnYouCopy(string status)
    {
        CustomerStatusFormatting.DescribeStatus(status)
            .Should().Be("We're waiting on information or approval from you before work can continue. Please contact the dealership.");
    }

    [Fact]
    public void DescribeStatus_Completed_ReturnsCompletedCopy()
    {
        CustomerStatusFormatting.DescribeStatus("Completed")
            .Should().Be("Your service has been completed. Please contact the dealership for pickup details.");
    }

    [Fact]
    public void DescribeStatus_Cancelled_ReturnsCancelledCopy()
    {
        CustomerStatusFormatting.DescribeStatus("Cancelled")
            .Should().Be("This service request has been cancelled.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SomethingElse")]
    public void DescribeStatus_UnknownOrEmpty_ReturnsGenericCopy(string? status)
    {
        CustomerStatusFormatting.DescribeStatus(status)
            .Should().Be("Your request is being processed.");
    }

    // ---- Status chip (issue #741) -------------------------------------------------

    [Theory]
    [InlineData("New", "New")]
    [InlineData("InProgress", "In Progress")]
    [InlineData("in-progress", "In Progress")]
    [InlineData("WaitingOnParts", "Waiting on Parts")]
    [InlineData("WaitingOnCustomer", "Waiting on You")]
    [InlineData("Completed", "Completed")]
    [InlineData("Cancelled", "Cancelled")]
    public void GetStatusLabel_KnownStatus_ReturnsCustomerFacingLabel(string status, string expected)
    {
        CustomerStatusFormatting.GetStatusLabel(status).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "Processing")]
    [InlineData("", "Processing")]
    [InlineData("  ", "Processing")]
    [InlineData("SomethingElse", "SomethingElse")]
    public void GetStatusLabel_UnknownOrEmpty_ReturnsRawValueOrGenericLabel(string? status, string expected)
    {
        CustomerStatusFormatting.GetStatusLabel(status).Should().Be(expected);
    }

    [Theory]
    [InlineData("New", Color.Info)]
    [InlineData("InProgress", Color.Warning)]
    [InlineData("WaitingOnParts", Color.Warning)]
    [InlineData("WaitingOnCustomer", Color.Warning)]
    [InlineData("Completed", Color.Success)]
    [InlineData("Cancelled", Color.Default)]
    [InlineData("SomethingElse", Color.Default)]
    [InlineData(null, Color.Default)]
    public void GetStatusColor_MatchesManagerBoardColors(string? status, Color expected)
    {
        CustomerStatusFormatting.GetStatusColor(status).Should().Be(expected);
    }

    [Fact]
    public void GetStatusIcon_EachKnownStatus_HasDistinctIcon()
    {
        string[] statuses = ["New", "InProgress", "WaitingOnParts", "WaitingOnCustomer", "Completed", "Cancelled"];

        var icons = statuses.Select(CustomerStatusFormatting.GetStatusIcon).ToList();

        icons.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i));
        icons.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetStatusIcon_UnknownStatus_ReturnsIcon()
    {
        CustomerStatusFormatting.GetStatusIcon("SomethingElse").Should().NotBeNullOrWhiteSpace();
    }

    // ---- FormatPhoneForDisplay ---------------------------------------------------

    [Theory]
    [InlineData("8015550100")]
    [InlineData("(801) 555-0100")]
    [InlineData("801-555-0100")]
    [InlineData("  (801) 555-0100  ")]
    public void FormatPhoneForDisplay_TenDigits_ReturnsCanonicalNanpFormat(string phone)
    {
        CustomerStatusFormatting.FormatPhoneForDisplay(phone).Should().Be("(801) 555-0100");
    }

    [Theory]
    [InlineData("18015550100")]
    [InlineData("1-801-555-0100")]
    [InlineData("+1 (801) 555-0100")]
    public void FormatPhoneForDisplay_ElevenDigitsLeadingOne_ReturnsCanonicalWithCountryCode(string phone)
    {
        CustomerStatusFormatting.FormatPhoneForDisplay(phone).Should().Be("+1 (801) 555-0100");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatPhoneForDisplay_NullOrBlank_ReturnsNull(string? phone)
    {
        CustomerStatusFormatting.FormatPhoneForDisplay(phone).Should().BeNull();
    }

    [Theory]
    [InlineData("555-0100", "555-0100")]
    [InlineData("+44 20 7946 0000", "+44 20 7946 0000")]
    [InlineData("call the front desk", "call the front desk")]
    public void FormatPhoneForDisplay_Unrecognized_ReturnsTrimmedOriginal(string phone, string expected)
    {
        CustomerStatusFormatting.FormatPhoneForDisplay(phone).Should().Be(expected);
    }

    // ---- ToTelHref ----------------------------------------------------------------

    [Theory]
    [InlineData("(801) 555-0100")]
    [InlineData("8015550100")]
    [InlineData("1-801-555-0100")]
    [InlineData("+1 (801) 555-0100")]
    public void ToTelHref_NanpNumber_ReturnsE164TelUri(string phone)
    {
        CustomerStatusFormatting.ToTelHref(phone).Should().Be("tel:+18015550100");
    }

    [Fact]
    public void ToTelHref_InternationalWithPlus_PreservesCountryCode()
    {
        CustomerStatusFormatting.ToTelHref("+44 20 7946 0000").Should().Be("tel:+442079460000");
    }

    [Fact]
    public void ToTelHref_ShortLocalNumber_ReturnsDigitsWithoutCountryCode()
    {
        CustomerStatusFormatting.ToTelHref("555-0100").Should().Be("tel:5550100");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no digits here")]
    public void ToTelHref_NoDigits_ReturnsNull(string? phone)
    {
        CustomerStatusFormatting.ToTelHref(phone).Should().BeNull();
    }
}
