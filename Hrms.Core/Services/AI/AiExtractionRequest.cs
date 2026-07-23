namespace Hrms.Core.Services.AI;

/// <summary>
/// Everything the extraction service needs for one call: the prompt, optional
/// documents, and optional per-call overrides. Built fluently:
/// <code>
/// AiExtractionRequest.For("Extract the disbursement details")
///     .WithDocument(AiDocument.FromBytes(pdfBytes, AiDocument.MimeTypes.Pdf))
///     .WithSystemInstruction("You are an accounting data-entry assistant.");
/// </code>
/// </summary>
public sealed class AiExtractionRequest
{
    private readonly List<AiDocument> _documents = new();

    public string Prompt { get; }
    public string SystemInstruction { get; private set; }
    public IReadOnlyList<AiDocument> Documents => _documents;

    /// <summary>Overrides GeminiOptions.Model for this call only.</summary>
    public string Model { get; private set; }

    /// <summary>Overrides GeminiOptions.Temperature for this call only.</summary>
    public double? Temperature { get; private set; }

    private AiExtractionRequest(string prompt)
    {
        Prompt = prompt;
    }

    public static AiExtractionRequest For(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("A prompt is required.", nameof(prompt));

        return new AiExtractionRequest(prompt);
    }

    public AiExtractionRequest WithDocument(AiDocument document)
    {
        _documents.Add(document ?? throw new ArgumentNullException(nameof(document)));
        return this;
    }

    public AiExtractionRequest WithSystemInstruction(string systemInstruction)
    {
        SystemInstruction = systemInstruction;
        return this;
    }

    public AiExtractionRequest WithModel(string model)
    {
        Model = model;
        return this;
    }

    public AiExtractionRequest WithTemperature(double temperature)
    {
        Temperature = temperature;
        return this;
    }
}
