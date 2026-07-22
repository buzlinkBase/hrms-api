namespace Hrms.Core.Services.AI;

/// <summary>
/// Outcome of an extraction call. Check IsSuccess before using Data;
/// RawJson always carries the model's raw output when one was produced,
/// which is useful for logging and debugging.
/// </summary>
public sealed class AiExtractionResult<T> where T : class
{
    public bool IsSuccess { get; }
    public T Data { get; }
    public string RawJson { get; }
    public string Model { get; }
    public string ErrorMessage { get; }

    private AiExtractionResult(bool isSuccess, T data, string rawJson, string model, string errorMessage)
    {
        IsSuccess = isSuccess;
        Data = data;
        RawJson = rawJson;
        Model = model;
        ErrorMessage = errorMessage;
    }

    public static AiExtractionResult<T> Success(T data, string rawJson, string model)
        => new(true, data, rawJson, model, null);

    public static AiExtractionResult<T> Failure(string errorMessage, string rawJson = null, string model = null)
        => new(false, null, rawJson, model, errorMessage);
}
