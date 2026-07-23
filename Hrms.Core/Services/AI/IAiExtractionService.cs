namespace Hrms.Core.Services.AI;

/// <summary>
/// Provider-agnostic structured extraction: send a prompt (optionally with
/// documents) and get back an instance of T shaped by T's properties.
/// Inject this interface — not the Gemini implementation — so callers stay
/// decoupled from the underlying AI provider.
/// </summary>
public interface IAiExtractionService
{
    Task<AiExtractionResult<T>> ExtractAsync<T>(AiExtractionRequest request, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>Prompt-only convenience overload.</summary>
    Task<AiExtractionResult<T>> ExtractAsync<T>(string prompt, CancellationToken cancellationToken = default)
        where T : class
        => ExtractAsync<T>(AiExtractionRequest.For(prompt), cancellationToken);

    /// <summary>Single-document convenience overload (e.g. a disbursement PDF).</summary>
    Task<AiExtractionResult<T>> ExtractFromDocumentAsync<T>(string prompt, byte[] documentBytes, string mimeType, CancellationToken cancellationToken = default)
        where T : class
        => ExtractAsync<T>(
            AiExtractionRequest.For(prompt).WithDocument(AiDocument.FromBytes(documentBytes, mimeType)),
            cancellationToken);
}
