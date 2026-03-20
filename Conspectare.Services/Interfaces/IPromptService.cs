namespace Conspectare.Services.Interfaces;

public interface IPromptService
{
    (string PromptText, string Version) GetPrompt(string phase, string documentType);
}
