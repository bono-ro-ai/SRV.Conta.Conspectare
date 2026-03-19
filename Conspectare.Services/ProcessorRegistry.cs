using Conspectare.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Conspectare.Services;

public class ProcessorRegistry : IProcessorRegistry
{
    private readonly IEnumerable<IDocumentProcessor> _processors;
    private readonly ILogger<ProcessorRegistry> _logger;

    public ProcessorRegistry(IEnumerable<IDocumentProcessor> processors, ILogger<ProcessorRegistry> logger)
    {
        _processors = processors;
        _logger = logger;
    }

    public IDocumentProcessor Resolve(string inputFormat, string contentType)
    {
        foreach (var processor in _processors)
        {
            if (processor.CanProcess(inputFormat, contentType))
            {
                _logger.LogDebug(
                    "Resolved processor {ProcessorType} for format '{InputFormat}' with content type '{ContentType}'",
                    processor.GetType().Name, inputFormat, contentType);
                return processor;
            }
        }

        _logger.LogWarning(
            "No processor found for format '{InputFormat}' with content type '{ContentType}'",
            inputFormat, contentType);

        throw new InvalidOperationException(
            $"No processor for format '{inputFormat}' with content type '{contentType}'");
    }
}
