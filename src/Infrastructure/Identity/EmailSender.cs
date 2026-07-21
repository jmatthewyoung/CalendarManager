using Azure;
using Azure.Communication.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CalendarManager.Infrastructure.Identity;

/// <summary>
/// Sends account emails (password reset, confirmation) via Azure Communication Services.
/// When <see cref="EmailOptions.ConnectionString"/> isn't configured (local dev), emails are
/// logged instead of sent so the reset/confirm flow can still be exercised end to end.
/// </summary>
public class EmailSender : IEmailSender<ApplicationUser>
{
    private readonly EmailClient? _client;
    private readonly EmailOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IOptions<EmailOptions> options, IHttpContextAccessor httpContextAccessor, ILogger<EmailSender> logger)
    {
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _client = string.IsNullOrWhiteSpace(_options.ConnectionString) ? null : new EmailClient(_options.ConnectionString);
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your CalendarManager email",
            $"<p>Confirm your CalendarManager account:</p><p><a href=\"{confirmationLink}\">Confirm email</a></p>");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your CalendarManager password",
            $"<p>Reset your CalendarManager password:</p><p><a href=\"{resetLink}\">Reset password</a></p>");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var link = BuildResetLink(email, resetCode);
        return SendAsync(email, "Reset your CalendarManager password",
            $"<p>Reset your CalendarManager password:</p><p><a href=\"{link}\">Reset password</a></p>");
    }

    private string BuildResetLink(string email, string resetCode)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        var baseUrl = request is not null ? $"{request.Scheme}://{request.Host}" : _options.FallbackBaseUrl;

        return $"{baseUrl}/reset-password?email={Uri.EscapeDataString(email)}&code={Uri.EscapeDataString(resetCode)}";
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        if (_client is null)
        {
            _logger.LogWarning("Email:ConnectionString is not configured — logging instead of sending. To: {Email}, Subject: {Subject}\n{Body}", toEmail, subject, htmlBody);
            return;
        }

        var message = new EmailMessage(
            senderAddress: _options.SenderAddress,
            recipients: new EmailRecipients([new EmailAddress(toEmail)]),
            content: new EmailContent(subject) { Html = htmlBody });

        await _client.SendAsync(WaitUntil.Started, message);
    }
}
