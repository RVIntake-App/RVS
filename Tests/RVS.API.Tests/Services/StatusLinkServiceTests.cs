using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RVS.API.Options;
using RVS.API.Services;
using RVS.Domain.Entities;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;

namespace RVS.API.Tests.Services;

/// <summary>
/// The emailed status link (issue #862): a customer types their email on <c>/status</c> and the
/// link to their status page goes to that inbox. Nothing about whether the address is on file
/// ever comes back to the caller, so every miss is silent rather than a
/// <see cref="KeyNotFoundException"/>.
/// </summary>
public class StatusLinkServiceTests
{
    private const string Email = "jane@example.com";
    private const string ExistingToken = "abc:existing";

    private readonly Mock<IGlobalCustomerAcctRepository> _repoMock = new();
    private readonly Mock<INotificationService> _emailMock = new();
    private readonly StatusLinkService _sut;

    public StatusLinkServiceTests()
    {
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalCustomerAcct a, CancellationToken _) => a);

        _sut = new StatusLinkService(
            _repoMock.Object,
            _emailMock.Object,
            Microsoft.Extensions.Options.Options.Create(new IntakeUrlOptions { BaseUrl = "https://rvintake.com/" }),
            Mock.Of<ILogger<StatusLinkService>>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendStatusLinkAsync_WhenEmailIsNullOrWhiteSpace_ShouldThrowArgumentException(string? email)
    {
        var act = () => _sut.SendStatusLinkAsync(email!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendStatusLinkAsync_WhenNoAccountHasTheEmail_ShouldSendNothingAndNotThrow()
    {
        _repoMock.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalCustomerAcct?)null);

        await _sut.SendStatusLinkAsync(Email);

        VerifyNoEmailSent();
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendStatusLinkAsync_WhenTheAccountHasNoRequests_ShouldSendNothing()
    {
        var acct = ArrangeAccount();
        acct.LinkedProfiles.Clear();

        await _sut.SendStatusLinkAsync(Email);

        VerifyNoEmailSent();
    }

    [Fact]
    public async Task SendStatusLinkAsync_ShouldLookUpTheNormalisedEmailAndSendToTheAccountAddress()
    {
        ArrangeAccount();

        await _sut.SendStatusLinkAsync("  Jane@Example.COM ");

        _repoMock.Verify(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>()), Times.Once);
        _emailMock.Verify(e => e.SendEmailAsync(
            Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendStatusLinkAsync_WhenTheTokenIsUnexpired_ShouldReuseItAndKeepItsExpiry()
    {
        var expiry = DateTime.UtcNow.AddDays(20);
        var acct = ArrangeAccount(token: ExistingToken, expiresAtUtc: expiry);

        await _sut.SendStatusLinkAsync(Email);

        acct.MagicLinkToken.Should().Be(ExistingToken);
        acct.MagicLinkExpiresAtUtc.Should().Be(expiry);
        _emailMock.Verify(e => e.SendEmailAsync(
            Email,
            It.IsAny<string>(),
            It.Is<string>(html => html.Contains("https://rvintake.com/status/abc:existing") && html.Contains("expires in 20 days")),
            It.Is<string>(text => text.Contains("https://rvintake.com/status/abc:existing")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendStatusLinkAsync_WhenTheTokenHasExpired_ShouldMintANewOneForNinetyDays()
    {
        var acct = ArrangeAccount(token: ExistingToken, expiresAtUtc: DateTime.UtcNow.AddDays(-1));

        await _sut.SendStatusLinkAsync(Email);

        acct.MagicLinkToken.Should().NotBeNullOrWhiteSpace().And.NotBe(ExistingToken);
        acct.MagicLinkExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(90), TimeSpan.FromMinutes(1));
        _repoMock.Verify(r => r.UpdateAsync(acct, It.IsAny<CancellationToken>()), Times.Once);
        _emailMock.Verify(e => e.SendEmailAsync(
            Email, It.IsAny<string>(), It.Is<string>(html => !html.Contains("abc:existing")),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendStatusLinkAsync_WhenThereIsNoToken_ShouldMintOne()
    {
        var acct = ArrangeAccount(token: null, expiresAtUtc: null);

        await _sut.SendStatusLinkAsync(Email);

        acct.MagicLinkToken.Should().NotBeNullOrWhiteSpace();
        acct.MagicLinkExpiresAtUtc.Should().NotBeNull();
        VerifyOneEmailSent();
    }

    [Fact]
    public async Task SendStatusLinkAsync_ShouldRecordWhenTheLinkWasEmailed()
    {
        var acct = ArrangeAccount();

        await _sut.SendStatusLinkAsync(Email);

        acct.StatusLinkEmailedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        _repoMock.Verify(r => r.UpdateAsync(acct, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendStatusLinkAsync_WhenALinkWasEmailedWithinTheCooldown_ShouldSendNothing()
    {
        // The per-IP rate limit does not stop many callers flooding one inbox; this does.
        var acct = ArrangeAccount();
        acct.StatusLinkEmailedAtUtc = DateTime.UtcNow.AddMinutes(-1);

        await _sut.SendStatusLinkAsync(Email);

        VerifyNoEmailSent();
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<GlobalCustomerAcct>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendStatusLinkAsync_WhenTheCooldownHasPassed_ShouldSendAgain()
    {
        var acct = ArrangeAccount();
        acct.StatusLinkEmailedAtUtc = DateTime.UtcNow - StatusLinkService.ResendCooldown - TimeSpan.FromSeconds(1);

        await _sut.SendStatusLinkAsync(Email);

        VerifyOneEmailSent();
    }

    [Fact]
    public async Task SendStatusLinkAsync_WhenTheSendFails_ShouldNotThrow()
    {
        ArrangeAccount();
        _emailMock.Setup(e => e.SendEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("SendGrid down"));

        var act = () => _sut.SendStatusLinkAsync(Email);

        await act.Should().NotThrowAsync();
    }

    private GlobalCustomerAcct ArrangeAccount(string? token = ExistingToken, DateTime? expiresAtUtc = null)
    {
        var acct = new GlobalCustomerAcct
        {
            Id = "gca_1",
            Email = Email,
            FirstName = "Jane",
            LastName = "Doe",
            MagicLinkToken = token,
            MagicLinkExpiresAtUtc = token is null ? expiresAtUtc : expiresAtUtc ?? DateTime.UtcNow.AddDays(30),
            LinkedProfiles = [new LinkedProfileEmbedded { TenantId = "ten_1", ProfileId = "cp_1", DealershipName = "Blue Compass RV" }],
        };
        _repoMock.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(acct);
        return acct;
    }

    private void VerifyOneEmailSent() =>
        _emailMock.Verify(e => e.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

    private void VerifyNoEmailSent() =>
        _emailMock.Verify(e => e.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
}
