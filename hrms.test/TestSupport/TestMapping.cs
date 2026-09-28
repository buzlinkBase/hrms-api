using Hrms.Core;
using Mapster;

namespace hrms.test.TestSupport;

/// <summary>
/// Mapster's GlobalSettings, with the app's MappingProfile registered exactly once per test run.
///
/// Test classes used to each call GlobalSettings.Scan(...) themselves. xUnit runs classes in
/// parallel, so one class re-registering mappings on the shared global config while another was
/// compiling or using it made unrelated tests fail at random ("Adapt was already called", wrong
/// or missing mappings). The scan now happens once, behind a thread-safe Lazy, and every caller
/// waits for it to finish.
/// </summary>
public static class TestMapping
{
    private static readonly Lazy<TypeAdapterConfig> Scanned = new(() =>
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(MappingProfile).Assembly);
        return TypeAdapterConfig.GlobalSettings;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    public static TypeAdapterConfig GlobalSettings => Scanned.Value;
}
