using System.Runtime.CompilerServices;
using Hrms.Api.Documents.Bir;
using PdfSharp.Fonts;

namespace hrms.test.DocumentTests;

// PdfOverlayEngine (via PdfSharp) needs GlobalFontSettings.FontResolver set before the first
// DrawString call, same as Program.cs does at app startup -- the test host never runs
// Program.Main, so it has to be registered here instead. A module initializer runs exactly
// once, before any test in this assembly, regardless of which test class executes first.
internal static class TestFontSetup
{
    [ModuleInitializer]
    public static void Register()
    {
        GlobalFontSettings.FontResolver = new BirFontResolver(
            Path.Combine(TestPaths.HrmsApiWebRoot(), "Fonts"));
    }
}
