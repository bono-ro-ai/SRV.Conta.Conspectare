using Conspectare.Domain.Entities;
using Conspectare.Services;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Conspectare.Tests;

public class ProcessorRegistryTests
{
    private readonly ProcessorRegistry _registry;
    private readonly FakeDocumentProcessor _pdfProcessor;
    private readonly FakeDocumentProcessor _xmlProcessor;

    public ProcessorRegistryTests()
    {
        _pdfProcessor = new FakeDocumentProcessor("pdf", "application/pdf");
        _xmlProcessor = new FakeDocumentProcessor("xml_efactura", "application/xml");

        _registry = new ProcessorRegistry(
            new IDocumentProcessor[] { _pdfProcessor, _xmlProcessor },
            NullLogger<ProcessorRegistry>.Instance);
    }

    [Fact]
    public void Resolve_MatchingProcessor_ReturnsIt()
    {
        var result = _registry.Resolve("pdf", "application/pdf");

        Assert.Same(_pdfProcessor, result);
    }

    [Fact]
    public void Resolve_NoMatch_ThrowsInvalidOperation()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => _registry.Resolve("unknown", "application/octet-stream"));

        Assert.Contains("No processor for format 'unknown'", ex.Message);
        Assert.Contains("application/octet-stream", ex.Message);
    }

    [Fact]
    public void Resolve_MultipleProcessors_ReturnsFirstMatch()
    {
        var firstPdf = new FakeDocumentProcessor("pdf", "application/pdf");
        var secondPdf = new FakeDocumentProcessor("pdf", "application/pdf");

        var registry = new ProcessorRegistry(
            new IDocumentProcessor[] { firstPdf, secondPdf },
            NullLogger<ProcessorRegistry>.Instance);

        var result = registry.Resolve("pdf", "application/pdf");

        Assert.Same(firstPdf, result);
    }

    [Fact]
    public void Resolve_SkipsNonMatching_ReturnsCorrect()
    {
        var result = _registry.Resolve("xml_efactura", "application/xml");

        Assert.Same(_xmlProcessor, result);
    }

    private class FakeDocumentProcessor : IDocumentProcessor
    {
        private readonly string _format;
        private readonly string _contentType;

        public FakeDocumentProcessor(string format, string contentType)
        {
            _format = format;
            _contentType = contentType;
        }

        public bool CanProcess(string inputFormat, string contentType) =>
            inputFormat == _format && contentType == _contentType;

        public Task<TriageResult> TriageAsync(Document doc, Stream rawFile, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ExtractionResult> ExtractAsync(Document doc, Stream rawFile, CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
