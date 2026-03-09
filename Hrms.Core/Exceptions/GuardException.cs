
using FluentValidation.Results;

namespace Hrms.Core;

public record EvaluationResult(string Message = "", bool Success = false)
{
    public static EvaluationResult OK => new EvaluationResult("", true);
    public static EvaluationResult Fail(string message) => new EvaluationResult(message);
    public static EvaluationResult Fail(List<ValidationFailure> failures)
    {
        var message = string.Join(Environment.NewLine,
             failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));

        return new EvaluationResult(message);
    }
    public static EvaluationResult Check(ValidationResult? result)
    {
        if (result == null || result.IsValid) return OK;
        return Fail(result.Errors);
    }
}


public static class Guard
{
    public static void ThrowIfNull<T>(T? value, string paramName) where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName ?? nameof(value));
    }
    public static void ThrowIfEmpty(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentNullException(paramName ?? nameof(value));
    }


    public static void EnsureTrue(bool condition, string message)
    {
        if (!condition)
            throw new ValidationException(message);
    }

    public static void EnsureFalse(bool condition, string message)
    {
        if (condition)
            throw new ValidationException(message);
    }

    public static void ThrowIfError(EvaluationResult? result)
    {
        if (result is null)
            throw new ValidationException("Validation result cannot be null.");

        if (!result.Success)
            throw new ValidationException(result.Message);
    }

    public static async Task ModelGuardAsync<T>(Func<T, CancellationToken, Task<EvaluationResult>> validator, T model, CancellationToken token)
            where T : class, IEntity
    {
        if (validator == null) return;
        var result = await validator.Invoke(model, token);
        if (result is null) return;
        if (!result.Success)
        {
            throw new ValidationException(result.Message);
        }
    }

    public static async Task ModelGuardAsync<T>(Func<T, CancellationToken, Task<EvaluationResult>> validator, IEnumerable<T> models, CancellationToken token)
        where T : class, IEntity
    {
        foreach (var model in models)
        {
            await ModelGuardAsync(validator, model, token);
        }
    }
}