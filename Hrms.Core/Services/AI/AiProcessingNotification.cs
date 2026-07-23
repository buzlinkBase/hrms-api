namespace Hrms.Core.Services.AI;

public enum AiProcessingStatus
{
    Started,
    Processing,
    Completed,
    Failed
}

/// <summary>
/// Event pushed to the frontend while an AI job runs. Created through the
/// factory methods so every notification carries a consistent shape.
/// </summary>
public sealed class AiProcessingNotification
{
    public string JobId { get; private set; }
    public AiProcessingStatus Status { get; private set; }
    public string Message { get; private set; }

    /// <summary>Optional payload, e.g. the extracted DTO on completion.</summary>
    public object Data { get; private set; }

    public DateTime TimestampUtc { get; private set; } = DateTime.UtcNow;

    private AiProcessingNotification() { }

    public static AiProcessingNotification Started(string jobId, string message = "AI processing started.")
        => new() { JobId = jobId, Status = AiProcessingStatus.Started, Message = message };

    public static AiProcessingNotification Processing(string jobId, string message)
        => new() { JobId = jobId, Status = AiProcessingStatus.Processing, Message = message };

    public static AiProcessingNotification Completed(string jobId, object data = null, string message = "AI processing completed.")
        => new() { JobId = jobId, Status = AiProcessingStatus.Completed, Message = message, Data = data };

    public static AiProcessingNotification Failed(string jobId, string errorMessage)
        => new() { JobId = jobId, Status = AiProcessingStatus.Failed, Message = errorMessage };
}

/// <summary>SignalR group-name conventions shared by backend and frontend.</summary>
public static class AiGroups
{
    public static string ForJob(string jobId) => $"ai-job-{jobId}";
}
