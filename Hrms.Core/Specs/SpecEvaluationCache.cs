namespace Hrms.Core.Specs;

public record struct SpecEvaluationCacheKey(string SpecName, Guid EmployeeId);
public class SpecEvaluationCache
{
    private readonly Dictionary<SpecEvaluationCacheKey, bool> _results = new();
    public void Record(SpecEvaluationCacheKey key, bool result) => _results[key] = result;
    public bool GetByKey(SpecEvaluationCacheKey key, out bool result) => _results.TryGetValue(key, out result);
    public bool GetByTag(string tag, Guid EmployeeId) => GetByKey(CreateKey(tag, EmployeeId), out bool result);
    public static SpecEvaluationCacheKey CreateKey(string specName, Guid EmployeeId)
        => new(specName, EmployeeId);
}
