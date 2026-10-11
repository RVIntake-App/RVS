using FluentAssertions;
using RVS.API.Integrations;

namespace RVS.API.Tests.Integrations;

public class StatusLinkEmailContentTests
{
    private const string StatusUrl = "https://rvintake.com/status/abc123token";
    private const string FirstName = "Jane";

    [Fact]
    public void Subject_ShouldNameTheStatusLink()
    {
        StatusLinkEmailContent.Subject.Should().Be("Your RV service request status link");
    }

    [Fact]
    public void BuildHtmlBody_ShouldGreetTheCustomerByFirstName()
    {
        var body = StatusLinkEmailContent.BuildHtmlBody(FirstName, StatusUrl, 30);

        body.Should().StartWith($"<p>Hi {FirstName},</p>");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void BuildHtmlBody_WhenFirstNameIsMissing_ShouldGreetWithoutAName(string? firstName)
    {
        var body = StatusLinkEmailContent.BuildHtmlBody(firstName, StatusUrl, 30);

        body.Should().StartWith("<p>Hi,</p>");
    }

    [Fact]
    public void BuildHtmlBody_ShouldLinkTheStatusPageWithACallToAction()
    {
        var body = StatusLinkEmailContent.BuildHtmlBody(FirstName, StatusUrl, 30);

        body.Should().Contain($"<a href=\"{StatusUrl}\">Check request status</a>");
    }

    [Fact]
    public void BuildHtmlBody_ShouldSayWhyItWasSentAndThatItCanBeIgnored()
    {
        // Anyone can type an address into /status, so the email has to tell an owner who did not
        // ask that nothing is needed of them (issue #862).
        var body = StatusLinkEmailContent.BuildHtmlBody(FirstName, StatusUrl, 30);

        body.Should().Contain("someone asked for a status link for this email address")
            .And.Contain("If that wasn't you, you can ignore this email.");
    }

    [Fact]
    public void BuildHtmlBody_ShouldSayWhenTheLinkExpires()
    {
        StatusLinkEmailContent.BuildHtmlBody(FirstName, StatusUrl, 30)
            .Should().Contain("This link expires in 30 days.");
        StatusLinkEmailContent.BuildHtmlBody(FirstName, StatusUrl, 1)
            .Should().Contain("This link expires in 1 day.");
    }

    [Fact]
    public void BuildHtmlBody_WhenExpiryIsUnknown_ShouldOmitTheExpirySentence()
    {
        StatusLinkEmailContent.BuildHtmlBody(FirstName, StatusUrl, null).Should().NotContain("expires");
    }

    [Fact]
    public void BuildHtmlBody_ShouldHtmlEncodeTheName()
    {
        var body = StatusLinkEmailContent.BuildHtmlBody("<b>Jane</b>", StatusUrl, 30);

        body.Should().Contain("&lt;b&gt;Jane&lt;/b&gt;").And.NotContain("<b>Jane</b>");
    }

    [Fact]
    public void BuildPlainTextBody_ShouldSayWhatTheHtmlSaysWithTheBareStatusLink()
    {
        var text = StatusLinkEmailContent.BuildPlainTextBody(FirstName, StatusUrl, 30);

        text.Should().StartWith($"Hi {FirstName},")
            .And.Contain($"\n{StatusUrl}\n", "a plain-text client shows the link on its own line, unwrapped")
            .And.Contain("This link expires in 30 days.")
            .And.EndWith("If that wasn't you, you can ignore this email.")
            .And.NotContain("<");
    }
}
