namespace Conspectare.Services.Interfaces;

public interface IProcessorRegistry
{
    IDocumentProcessor Resolve(string inputFormat, string contentType);
}
