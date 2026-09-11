using System.Runtime.CompilerServices;

namespace hrms.test.DocumentTests;

// Locates hrms-api's wwwroot directly from the source tree (via CallerFilePath, resolved at
// compile time) rather than relying on the test project's own build output containing a copy
// of hrms-api's content files -- works the same regardless of test runner working directory or
// build configuration depth.
internal static class TestPaths
{
    public static string HrmsApiWebRoot([CallerFilePath] string sourceFile = "")
    {
        // sourceFile: .../hrms-api/hrms.test/DocumentTests/TestPaths.cs
        // repoRoot:   .../hrms-api  (contains both hrms.test/ and hrms-api/ as siblings)
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", ".."));
        return Path.Combine(repoRoot, "hrms-api", "wwwroot");
    }
}
