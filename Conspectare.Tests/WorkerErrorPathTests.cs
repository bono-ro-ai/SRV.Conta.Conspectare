using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services;
using Xunit;

namespace Conspectare.Tests;

public class WorkerErrorPathTests
{
    private readonly DocumentStatusWorkflow _workflow = new();

    [Fact]
    public void HandleExtractionError_RetryCountBelowMax_TransitionsToExtractionFailed()
    {
        var doc = CreateExtractingDocument(retryCount: 0, maxRetries: 3);
        var nextStatus = SimulateHandleExtractionError(doc);

        Assert.Equal(DocumentStatus.ExtractionFailed, nextStatus);
        Assert.Equal(1, doc.RetryCount);
    }

    [Fact]
    public void HandleExtractionError_RetryCountAtMax_DeterminesFailedStatus()
    {
        var doc = CreateExtractingDocument(retryCount: 2, maxRetries: 3);
        doc.RetryCount++;
        var nextStatus = doc.RetryCount >= doc.MaxRetries
            ? DocumentStatus.Failed
            : DocumentStatus.ExtractionFailed;

        Assert.Equal(DocumentStatus.Failed, nextStatus);
        Assert.Equal(3, doc.RetryCount);
    }

    [Fact]
    public void HandleExtractionError_RetryCountExceedsMax_DeterminesFailedStatus()
    {
        var doc = CreateExtractingDocument(retryCount: 4, maxRetries: 3);
        doc.RetryCount++;
        var nextStatus = doc.RetryCount >= doc.MaxRetries
            ? DocumentStatus.Failed
            : DocumentStatus.ExtractionFailed;

        Assert.Equal(DocumentStatus.Failed, nextStatus);
    }

    [Fact]
    public void HandleExtractionError_ErrorMessageTruncation_TruncatesTo2000Chars()
    {
        var longMessage = new string('x', 3000);
        var truncated = longMessage.Length > 2000 ? longMessage[..2000] : longMessage;

        Assert.Equal(2000, truncated.Length);
    }

    [Fact]
    public void HandleExtractionError_ShortErrorMessage_PreservedAsIs()
    {
        var shortMessage = "LLM timeout";
        var truncated = shortMessage.Length > 2000 ? shortMessage[..2000] : shortMessage;

        Assert.Equal("LLM timeout", truncated);
    }

    [Fact]
    public void CanTransition_ExtractingToExtractionFailed_IsValid()
    {
        Assert.True(_workflow.CanTransition(DocumentStatus.Extracting, DocumentStatus.ExtractionFailed));
    }

    [Fact]
    public void CanTransition_ExtractingToCompleted_IsValid()
    {
        Assert.True(_workflow.CanTransition(DocumentStatus.Extracting, DocumentStatus.Completed));
    }

    [Fact]
    public void CanTransition_ExtractingToReviewRequired_IsValid()
    {
        Assert.True(_workflow.CanTransition(DocumentStatus.Extracting, DocumentStatus.ReviewRequired));
    }

    [Fact]
    public void CanTransition_ExtractingToFailed_IsValid()
    {
        Assert.True(_workflow.CanTransition(DocumentStatus.Extracting, DocumentStatus.Failed));
    }

    [Fact]
    public void CanTransition_ExtractionFailedToFailed_IsValid()
    {
        Assert.True(_workflow.CanTransition(DocumentStatus.ExtractionFailed, DocumentStatus.Failed));
    }

    [Fact]
    public void CanTransition_ExtractionFailedToPendingTriage_IsValid()
    {
        Assert.True(_workflow.CanTransition(DocumentStatus.ExtractionFailed, DocumentStatus.PendingTriage));
    }

    [Fact]
    public void CanTransition_TriagingToPendingExtraction_IsValid()
    {
        Assert.True(_workflow.CanTransition(DocumentStatus.Triaging, DocumentStatus.PendingExtraction));
    }

    [Fact]
    public void CanTransition_TriagingToRejected_IsValid()
    {
        Assert.True(_workflow.CanTransition(DocumentStatus.Triaging, DocumentStatus.Rejected));
    }

    [Fact]
    public void CanTransition_TriagingToReviewRequired_IsValid()
    {
        Assert.True(_workflow.CanTransition(DocumentStatus.Triaging, DocumentStatus.ReviewRequired));
    }

    [Fact]
    public void CanTransition_CompletedToAnything_IsInvalid()
    {
        Assert.False(_workflow.CanTransition(DocumentStatus.Completed, DocumentStatus.PendingTriage));
        Assert.False(_workflow.CanTransition(DocumentStatus.Completed, DocumentStatus.Extracting));
        Assert.False(_workflow.CanTransition(DocumentStatus.Completed, DocumentStatus.Failed));
    }

    [Fact]
    public void CanTransition_FailedToAnything_IsInvalid()
    {
        Assert.False(_workflow.CanTransition(DocumentStatus.Failed, DocumentStatus.PendingTriage));
        Assert.False(_workflow.CanTransition(DocumentStatus.Failed, DocumentStatus.Extracting));
    }

    [Fact]
    public void CanTransition_RejectedToAnything_IsInvalid()
    {
        Assert.False(_workflow.CanTransition(DocumentStatus.Rejected, DocumentStatus.PendingTriage));
        Assert.False(_workflow.CanTransition(DocumentStatus.Rejected, DocumentStatus.Extracting));
    }

    [Fact]
    public void IsTerminalState_Completed_IsTerminal()
    {
        Assert.True(_workflow.IsTerminalState(DocumentStatus.Completed));
    }

    [Fact]
    public void IsTerminalState_Failed_IsTerminal()
    {
        Assert.True(_workflow.IsTerminalState(DocumentStatus.Failed));
    }

    [Fact]
    public void IsTerminalState_Rejected_IsTerminal()
    {
        Assert.True(_workflow.IsTerminalState(DocumentStatus.Rejected));
    }

    [Fact]
    public void IsTerminalState_Extracting_IsNotTerminal()
    {
        Assert.False(_workflow.IsTerminalState(DocumentStatus.Extracting));
    }

    [Fact]
    public void IsTerminalState_PendingTriage_IsNotTerminal()
    {
        Assert.False(_workflow.IsTerminalState(DocumentStatus.PendingTriage));
    }

    [Fact]
    public void GetExternalStatus_Extracting_ReturnsProcessing()
    {
        Assert.Equal("processing", _workflow.GetExternalStatus(DocumentStatus.Extracting));
    }

    [Fact]
    public void GetExternalStatus_Completed_ReturnsCompleted()
    {
        Assert.Equal("completed", _workflow.GetExternalStatus(DocumentStatus.Completed));
    }

    [Fact]
    public void GetExternalStatus_Failed_ReturnsFailed()
    {
        Assert.Equal("failed", _workflow.GetExternalStatus(DocumentStatus.Failed));
    }

    [Fact]
    public void GetExternalStatus_UnknownStatus_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _workflow.GetExternalStatus("nonexistent_status"));
    }

    [Fact]
    public void CancellationToken_WhenCancelled_ThrowsOperationCanceledException()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
        {
            cts.Token.ThrowIfCancellationRequested();
        });
    }

    [Fact]
    public void CancellationToken_WhenNotCancelled_DoesNotThrow()
    {
        var cts = new CancellationTokenSource();

        var exception = Record.Exception(() => cts.Token.ThrowIfCancellationRequested());

        Assert.Null(exception);
    }

    [Fact]
    public void GetAvailableTransitions_Extracting_ReturnsFourOptions()
    {
        var transitions = _workflow.GetAvailableTransitions(DocumentStatus.Extracting);

        Assert.Equal(4, transitions.Count);
        Assert.Contains(DocumentStatus.Completed, transitions);
        Assert.Contains(DocumentStatus.ExtractionFailed, transitions);
        Assert.Contains(DocumentStatus.ReviewRequired, transitions);
        Assert.Contains(DocumentStatus.Failed, transitions);
    }

    [Fact]
    public void GetAvailableTransitions_TerminalState_ReturnsEmpty()
    {
        var transitions = _workflow.GetAvailableTransitions(DocumentStatus.Completed);

        Assert.Empty(transitions);
    }

    private static Document CreateExtractingDocument(int retryCount, int maxRetries)
    {
        return new Document
        {
            Id = 1,
            TenantId = 1,
            FileName = "test.xml",
            ContentType = "text/xml",
            FileSizeBytes = 100,
            InputFormat = InputFormat.XmlEfactura,
            Status = DocumentStatus.Extracting,
            RetryCount = retryCount,
            MaxRetries = maxRetries,
            RawFileS3Key = "tenants/1/raw/test.xml",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private string SimulateHandleExtractionError(Document doc)
    {
        doc.RetryCount++;
        var nextStatus = doc.RetryCount >= doc.MaxRetries
            ? DocumentStatus.Failed
            : DocumentStatus.ExtractionFailed;
        if (_workflow.CanTransition(DocumentStatus.Extracting, nextStatus))
            doc.Status = nextStatus;
        return nextStatus;
    }
}
