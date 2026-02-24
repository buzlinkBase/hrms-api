namespace Hrms.Core.Specs;

public record struct PayrollrunLedgerCacheKey(string ruleKey, Guid EmployeeId);

public class PayrollrunLedger
{
    private readonly Dictionary<PayrollrunLedgerCacheKey, object> _allocations = new();
    public void Record(PayrollrunLedgerCacheKey key, object IPipeData)
    {
        _allocations[key] = IPipeData;
    }

    public void RecordByTag(string keyName, Guid EmployeeId, object data)
    {
        var key = CreateKey(keyName, EmployeeId);
        Record(key, data);
    }

    public bool GetByKey(PayrollrunLedgerCacheKey key, out object data)
    {
        return _allocations.TryGetValue(key, out data);
    }

    public object? GetByTag(string tagName, Guid EmployeeId, object data)
    {
        var key = CreateKey(tagName, EmployeeId);
        if (GetByKey(key, out var result))
            return result;
        return default;
    }

    public object? GetByKey(PayrollrunLedgerCacheKey key)
    {
        if (GetByKey(key, out var result))
            return result;
        return default;
    }
    public IReadOnlyDictionary<PayrollrunLedgerCacheKey, object> Snapshot() => _allocations;
    public static PayrollrunLedgerCacheKey CreateKey(string KeyName, Guid EmployeeId)
        => new(KeyName, EmployeeId);
}

