using Conspectare.Domain.Enums;
using Conspectare.Services;
using Xunit;

namespace Conspectare.Tests;

public class DocumentStatusWorkflowTests
{
    private readonly DocumentStatusWorkflow _workflow = new();

    [Theory]
    [InlineData(DocumentStatus.Ingested, DocumentStatus.PendingTriage)]
    [InlineData(DocumentStatus.PendingTriage, DocumentStatus.Triaging)]
    [InlineData(DocumentStatus.Triaging, DocumentStatus.PendingExtraction)]
    [InlineData(DocumentStatus.Triaging, DocumentStatus.ReviewRequired)]
    [InlineData(DocumentStatus.Triaging, DocumentStatus.Rejected)]
    [InlineData(DocumentStatus.PendingExtraction, DocumentStatus.Extracting)]
    [InlineData(DocumentStatus.Extracting, DocumentStatus.Completed)]
    [InlineData(DocumentStatus.Extracting, DocumentStatus.ExtractionFailed)]
    [InlineData(DocumentStatus.Extracting, DocumentStatus.ReviewRequired)]
    [InlineData(DocumentStatus.Extracting, DocumentStatus.Failed)]
    [InlineData(DocumentStatus.ExtractionFailed, DocumentStatus.PendingTriage)]
    [InlineData(DocumentStatus.ExtractionFailed, DocumentStatus.Failed)]
    [InlineData(DocumentStatus.ReviewRequired, DocumentStatus.PendingTriage)]
    [InlineData(DocumentStatus.ReviewRequired, DocumentStatus.Rejected)]
    [InlineData(DocumentStatus.ReviewRequired, DocumentStatus.Completed)]
    public void CanTransition_ValidTransitions_ReturnsTrue(string from, string to)
    {
        Assert.True(_workflow.CanTransition(from, to));
    }

    [Theory]
    [InlineData(DocumentStatus.Completed, DocumentStatus.Ingested)]
    [InlineData(DocumentStatus.Completed, DocumentStatus.PendingTriage)]
    [InlineData(DocumentStatus.Completed, DocumentStatus.Rejected)]
    [InlineData(DocumentStatus.Failed, DocumentStatus.Ingested)]
    [InlineData(DocumentStatus.Failed, DocumentStatus.PendingTriage)]
    [InlineData(DocumentStatus.Failed, DocumentStatus.Completed)]
    [InlineData(DocumentStatus.Rejected, DocumentStatus.Ingested)]
    [InlineData(DocumentStatus.Rejected, DocumentStatus.PendingTriage)]
    [InlineData(DocumentStatus.Rejected, DocumentStatus.Completed)]
    [InlineData(DocumentStatus.Ingested, DocumentStatus.Completed)]
    [InlineData(DocumentStatus.Ingested, DocumentStatus.Extracting)]
    [InlineData(DocumentStatus.PendingTriage, DocumentStatus.Completed)]
    [InlineData(DocumentStatus.Extracting, DocumentStatus.Ingested)]
    [InlineData(DocumentStatus.PendingExtraction, DocumentStatus.Completed)]
    [InlineData(DocumentStatus.Triaging, DocumentStatus.Ingested)]
    public void CanTransition_InvalidTransitions_ReturnsFalse(string from, string to)
    {
        Assert.False(_workflow.CanTransition(from, to));
    }

    [Fact]
    public void CanTransition_UnknownStatus_ReturnsFalse()
    {
        Assert.False(_workflow.CanTransition("unknown_status", DocumentStatus.Completed));
        Assert.False(_workflow.CanTransition(DocumentStatus.Ingested, "unknown_status"));
        Assert.False(_workflow.CanTransition("foo", "bar"));
    }

    [Theory]
    [InlineData(DocumentStatus.Ingested, 1)]
    [InlineData(DocumentStatus.PendingTriage, 1)]
    [InlineData(DocumentStatus.Triaging, 3)]
    [InlineData(DocumentStatus.PendingExtraction, 1)]
    [InlineData(DocumentStatus.Extracting, 4)]
    [InlineData(DocumentStatus.ExtractionFailed, 2)]
    [InlineData(DocumentStatus.ReviewRequired, 3)]
    public void GetAvailableTransitions_ReturnsCorrectCount(string status, int expectedCount)
    {
        var transitions = _workflow.GetAvailableTransitions(status);
        Assert.Equal(expectedCount, transitions.Count);
    }

    [Fact]
    public void TerminalStates_HaveNoOutgoingTransitions()
    {
        Assert.Empty(_workflow.GetAvailableTransitions(DocumentStatus.Completed));
        Assert.Empty(_workflow.GetAvailableTransitions(DocumentStatus.Failed));
        Assert.Empty(_workflow.GetAvailableTransitions(DocumentStatus.Rejected));
    }

    [Theory]
    [InlineData(DocumentStatus.Ingested, ExternalDocumentStatus.Processing)]
    [InlineData(DocumentStatus.PendingTriage, ExternalDocumentStatus.Processing)]
    [InlineData(DocumentStatus.Triaging, ExternalDocumentStatus.Processing)]
    [InlineData(DocumentStatus.PendingExtraction, ExternalDocumentStatus.Processing)]
    [InlineData(DocumentStatus.Extracting, ExternalDocumentStatus.Processing)]
    [InlineData(DocumentStatus.ExtractionFailed, ExternalDocumentStatus.Processing)]
    [InlineData(DocumentStatus.Completed, ExternalDocumentStatus.Completed)]
    [InlineData(DocumentStatus.Failed, ExternalDocumentStatus.Failed)]
    [InlineData(DocumentStatus.ReviewRequired, ExternalDocumentStatus.ReviewRequired)]
    [InlineData(DocumentStatus.Rejected, ExternalDocumentStatus.Rejected)]
    public void GetExternalStatus_MapsCorrectly(string internalStatus, string expectedExternal)
    {
        Assert.Equal(expectedExternal, _workflow.GetExternalStatus(internalStatus));
    }

    [Fact]
    public void GetExternalStatus_UnknownStatus_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _workflow.GetExternalStatus("unknown_status"));
    }

    [Theory]
    [InlineData(DocumentStatus.Completed, true)]
    [InlineData(DocumentStatus.Failed, true)]
    [InlineData(DocumentStatus.Rejected, true)]
    [InlineData(DocumentStatus.Ingested, false)]
    [InlineData(DocumentStatus.PendingTriage, false)]
    [InlineData(DocumentStatus.Triaging, false)]
    [InlineData(DocumentStatus.PendingExtraction, false)]
    [InlineData(DocumentStatus.Extracting, false)]
    [InlineData(DocumentStatus.ExtractionFailed, false)]
    [InlineData(DocumentStatus.ReviewRequired, false)]
    public void IsTerminalState_CorrectForAllStatuses(string status, bool expected)
    {
        Assert.Equal(expected, _workflow.IsTerminalState(status));
    }
}
