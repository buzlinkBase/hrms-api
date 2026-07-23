namespace Hrms.Core.Services.AI;


/// <summary>
/// Strongly typed settings bound from the "Gemini" section of appsettings.json.
/// The API key may also be supplied through the GEMINI_API_KEY environment
/// variable (loaded from .env by DotNetEnv), which takes over when ApiKey is blank.
/// </summary>
public class GeminiOptions
{
    public const string SectionName = "Gemini";
    public const string ApiKeyEnvironmentVariable = "GEMINI_API_KEY";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.5-flash-lite";

    /// <summary>Low temperature keeps document extraction deterministic.</summary>
    public double? Temperature { get; set; } = 0.0;
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// Thinking budget in tokens. 0 disables thinking (the default here) —
    /// newer Flash models think by default, which multiplies latency for
    /// extraction tasks. -1 restores the model's automatic behavior.
    /// </summary>
    public int? ThinkingBudget { get; set; } = 0;

    /// <summary>Per-request timeout in milliseconds for calls to Gemini.</summary>
    public int? RequestTimeoutMs { get; set; } = 120000;
}
