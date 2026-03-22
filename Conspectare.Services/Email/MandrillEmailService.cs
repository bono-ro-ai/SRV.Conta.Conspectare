using System.Text;
using System.Text.Json;
using Conspectare.Services.Configuration;
using Conspectare.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Conspectare.Services.Email;

public class MandrillEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly MandrillSettings _settings;
    private readonly ILogger<MandrillEmailService> _logger;

    public MandrillEmailService(HttpClient httpClient, IOptions<MandrillSettings> settings, ILogger<MandrillEmailService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendMagicLinkEmailAsync(string email, string url)
    {
        var htmlBody = BuildMagicLinkHtml(url);

        var payload = new
        {
            key = _settings.ApiKey,
            message = new
            {
                html = htmlBody,
                subject = "Autentificare Conspectare",
                from_email = _settings.DefaultSender,
                from_name = _settings.DefaultSenderName,
                to = new[]
                {
                    new { email, type = "to" }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://mandrillapp.com/api/1.0/messages/send", content);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Mandrill API error {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Failed to send magic link email via Mandrill: {response.StatusCode}");
        }

        _logger.LogInformation("Magic link email sent to {MaskedEmail} via Mandrill", Auth.AuthTokenHelper.MaskEmail(email));
    }

    private static string BuildMagicLinkHtml(string url)
    {
        return $"""
                <!DOCTYPE html>
                <html>
                <head><meta charset="utf-8"></head>
                <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
                  <h2 style="color: #333;">Autentificare Conspectare</h2>
                  <p>Apasă butonul de mai jos pentru a te autentifica:</p>
                  <a href="{url}" style="display: inline-block; padding: 12px 24px; background-color: #2563eb; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold;">Autentificare</a>
                  <p style="margin-top: 20px; color: #666; font-size: 14px;">Linkul expiră în 15 minute. Dacă nu ai solicitat acest email, te rugăm să îl ignori.</p>
                  <p style="margin-top: 10px; color: #999; font-size: 12px;">Dacă butonul nu funcționează, copiază acest link în browser:<br/><a href="{url}">{url}</a></p>
                </body>
                </html>
                """;
    }
}
