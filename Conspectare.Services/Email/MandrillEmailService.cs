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
                <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
                <body style="margin: 0; padding: 0; background-color: #fdeef4; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;">
                  <table role="presentation" cellpadding="0" cellspacing="0" style="width: 100%; background-color: #fdeef4;">
                    <tr>
                      <td align="center" style="padding: 40px 20px;">
                        <table role="presentation" cellpadding="0" cellspacing="0" style="max-width: 520px; width: 100%; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.05);">
                          <tr>
                            <td style="background: linear-gradient(135deg, #ee4379 0%, #f472a8 100%); padding: 32px 40px; text-align: center;">
                              <h1 style="margin: 0; color: #ffffff; font-size: 24px; font-weight: 700; letter-spacing: 1px;">CONSPECTARE</h1>
                              <p style="margin: 4px 0 0; color: rgba(255,255,255,0.85); font-size: 13px;">by Bono</p>
                            </td>
                          </tr>
                          <tr>
                            <td style="padding: 36px 40px;">
                              <p style="margin: 0 0 16px; color: #1a1a1a; font-size: 16px; font-weight: 600;">Salut!</p>
                              <p style="margin: 0 0 28px; color: #4a4a4a; font-size: 15px; line-height: 1.6;">Ai solicitat un link de autentificare. Apasă butonul de mai jos pentru a te conecta la contul tău:</p>
                              <table role="presentation" cellpadding="0" cellspacing="0" style="margin: 0 auto;">
                                <tr>
                                  <td align="center" style="border-radius: 28px; background: linear-gradient(135deg, #ee4379 0%, #d63031 100%);">
                                    <a href="{url}" style="display: inline-block; padding: 14px 48px; color: #ffffff; text-decoration: none; font-size: 16px; font-weight: 600; letter-spacing: 0.3px;">Conectează-te</a>
                                  </td>
                                </tr>
                              </table>
                              <p style="margin: 28px 0 0; color: #888888; font-size: 13px; line-height: 1.5;">Dacă butonul nu funcționează, copiază și lipește următorul link în browser:</p>
                              <p style="margin: 8px 0 0;"><a href="{url}" style="color: #ee4379; font-size: 13px; word-break: break-all;">{url}</a></p>
                              <p style="margin: 24px 0 0; color: #888888; font-size: 13px; line-height: 1.5;">Link-ul expiră în 15 minute. Dacă nu ai solicitat acest link, poți ignora acest email în siguranță — contul tău nu a fost compromis.</p>
                            </td>
                          </tr>
                        </table>
                        <p style="margin: 20px 0 0; color: #c4849e; font-size: 12px;">Trimis prin <a href="https://conspectare.bono.ro" style="color: #ee4379; text-decoration: none;">Conspectare by Bono</a></p>
                      </td>
                    </tr>
                  </table>
                </body>
                </html>
                """;
    }
}
