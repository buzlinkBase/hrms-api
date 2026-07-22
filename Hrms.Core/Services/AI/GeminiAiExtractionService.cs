using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Hrms.Core.Services.AI;

/// <summary>
/// Gemini-backed implementation of IAiExtractionService (adapter over the
/// Google.GenAI SDK). Sends the prompt and any attached documents with a
/// response schema generated from T, so the model must answer with JSON
/// matching T, then deserializes that JSON into T.
/// Registered as a singleton; the underlying client is thread-safe.
/// </summary>
public sealed class GeminiAiExtractionService : IAiExtractionService, IDisposable
{
    private readonly Client _client;
    private readonly IAiSchemaGenerator _schemaGenerator;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiAiExtractionService> _logger;

    public GeminiAiExtractionService(
        IOptions<GeminiOptions> options,
        IAiSchemaGenerator schemaGenerator,
        ILogger<GeminiAiExtractionService> logger)
    {
        _options = options.Value;
        _schemaGenerator = schemaGenerator;
        _logger = logger;

        var apiKey = !string.IsNullOrWhiteSpace(_options.ApiKey)
            ? _options.ApiKey
            : System.Environment.GetEnvironmentVariable(GeminiOptions.ApiKeyEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                $"Gemini API key is not configured. Set '{GeminiOptions.SectionName}:ApiKey' in appsettings.json " +
                $"or the {GeminiOptions.ApiKeyEnvironmentVariable} environment variable (.env).");

        _client = new Client(apiKey: apiKey);
    }

    public async Task<AiExtractionResult<T>> ExtractAsync<T>(AiExtractionRequest request, CancellationToken cancellationToken = default)
        where T : class
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var model = !string.IsNullOrWhiteSpace(request.Model) ? request.Model : _options.Model;

        var parts = request.Documents
            .Select(document => Part.FromBytes(document.Bytes, document.MimeType))
            .ToList();
        parts.Add(Part.FromText(request.Prompt));

        var contents = new List<Content>
        {
            new Content { Role = "user", Parts = parts }
        };

        var config = new GenerateContentConfig
        {
            ResponseMimeType = "application/json",
            ResponseSchema = _schemaGenerator.Generate(typeof(T)),
            Temperature = request.Temperature ?? _options.Temperature,
            MaxOutputTokens = _options.MaxOutputTokens
        };

        if (_options.ThinkingBudget.HasValue)
            config.ThinkingConfig = new ThinkingConfig { ThinkingBudget = _options.ThinkingBudget };

        if (_options.RequestTimeoutMs.HasValue)
            config.HttpOptions = new HttpOptions { Timeout = _options.RequestTimeoutMs };

        if (!string.IsNullOrWhiteSpace(request.SystemInstruction))
        {
            config.SystemInstruction = new Content
            {
                Parts = new List<Part> { Part.FromText(request.SystemInstruction) }
            };
        }

        string rawJson = null;
        try
        {
            var response = await _client.Models.GenerateContentAsync(model, contents, config, cancellationToken);
            rawJson = response?.Text;

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                _logger.LogWarning("Gemini returned an empty response for {TargetType} using model {Model}.", typeof(T).Name, model);
                return AiExtractionResult<T>.Failure("The AI model returned an empty response.", rawJson, model);
            }

            var data = JsonConvert.DeserializeObject<T>(rawJson);
            if (data == null)
                return AiExtractionResult<T>.Failure("The AI response could not be mapped to " + typeof(T).Name + ".", rawJson, model);

            return AiExtractionResult<T>.Success(data, rawJson, model);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Gemini response into {TargetType}. Raw response: {RawJson}", typeof(T).Name, rawJson);
            return AiExtractionResult<T>.Failure("The AI response was not valid JSON for " + typeof(T).Name + ": " + ex.Message, rawJson, model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini extraction failed for {TargetType} using model {Model}.", typeof(T).Name, model);
            return AiExtractionResult<T>.Failure("AI extraction failed: " + ex.Message, rawJson, model);
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
