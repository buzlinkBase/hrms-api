namespace Hrms.Core.Services.AI;

public class TestCaller 
{
    public TestCaller()
    {
         //new TestUsage().Process(new AIContext());
    }
}

public interface IDocumentProcessor
{
    /// <summary>
    /// Runs in the background worker, never inside an HTTP request. jobId is the
    /// SignalR group key (AiGroups.ForJob) the frontend listens on for progress.
    /// </summary>
    Task Process(IDocumentPayload payload, string jobId, CancellationToken cancellationToken);
    Task Revert(IDocumentPayload payload, string jobId, CancellationToken cancellationToken);
} 

public class TestUsage
{
    private readonly StreamLoader _streamLoader;
    private readonly IAiExtractionService _aiExtractionService;
    private readonly IAiNotificationService _aiNotificationService;

    public TestUsage(StreamLoader streamLoader,
        IAiExtractionService aiExtractionService,
        IAiNotificationService aiNotificationService )
    {
        _streamLoader = streamLoader;
        _aiExtractionService = aiExtractionService;
        _aiNotificationService = aiNotificationService;
    }

    public async Task Process(IDocumentPayload payload, string jobId, CancellationToken cancellationToken)
    {
        if (payload is not AIContext context  ) return;
 
        var group = AiGroups.ForJob(jobId);
        try
        {
            await _aiNotificationService.NotifyGroupAsync(group,
                AiProcessingNotification.Started(jobId, "Extracting data from the attached document."), cancellationToken);

            using var stream = await _streamLoader.FromPathOrUrlAsync("file source path", cancellationToken);
            var document = await AiDocument.FromStreamAsync(stream.Stream, stream.ContentType, cancellationToken);

            var request = AiExtractionRequest
                .For("Extract the information details from this document.")
                .WithDocument(document)
                .WithSystemInstruction(
                    "You are an human resource " +
                    "Extract values exactly as they appear in the document. " +
                    "Return null for any field that is not present.");

            var result = await _aiExtractionService.ExtractAsync<AiExtractedInfoModelMain>(request, cancellationToken);

            if (!result.IsSuccess)
            {
                // Silently skip if AI parsing fails
                await _aiNotificationService.NotifyGroupAsync(group, AiProcessingNotification.Failed(jobId, result.ErrorMessage), cancellationToken);
                return;
            }  
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
        }
    }
}
public class AiExtractedInfoModelMain 
{
    [AiDescription("The description")]
    public string Description  { get; set; }

    [AiDescription("The individual  lines info")]
    public List<AiExtractedInfoModelDetail> Lines { get; set; }
}

public class AiExtractedInfoModelDetail 
{
    [AiDescription("The item code")]
    public string ItemCode { get; set; }
}

public interface IDocumentPayload { }

public class AIContext : IDocumentPayload
{
}