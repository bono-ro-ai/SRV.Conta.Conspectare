namespace Conspectare.Services.Interfaces;

public interface IEmailService
{
    Task SendMagicLinkEmailAsync(string email, string url);
}
