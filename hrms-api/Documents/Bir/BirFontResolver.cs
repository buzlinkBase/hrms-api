using PdfSharp.Fonts;

namespace Hrms.Api.Documents.Bir;

// PdfSharp 6.x has no built-in standard/Base-14 fonts -- every typeface, including "Arial" or
// "Helvetica" by name, must be resolved to actual font file bytes via a registered
// GlobalFontSettings.FontResolver, or the first DrawString call throws. This matters most on
// Linux/Docker (this API's deployment target — see Dockerfile), where there's no OS font to
// fall back to the way there might be on a Windows dev machine.
//
// Resolves to Liberation Sans (wwwroot/Fonts/LiberationSans-*.ttf), bundled under the SIL Open
// Font License 1.1 (see wwwroot/Fonts/LICENSE-LiberationSans.txt) — metrically compatible with
// Arial/Helvetica so text width/wrapping matches, and freely embeddable in closed-source
// software. Register once at startup: GlobalFontSettings.FontResolver = new BirFontResolver(...).
public class BirFontResolver : IFontResolver
{
    public const string FamilyName = "Liberation Sans";

    private const string RegularFace = "LiberationSans-Regular";
    private const string BoldFace = "LiberationSans-Bold";

    private readonly string _fontDirectory;

    public BirFontResolver(string fontDirectory)
    {
        _fontDirectory = fontDirectory;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (!familyName.Equals(FamilyName, StringComparison.OrdinalIgnoreCase)
            && !familyName.Equals("Arial", StringComparison.OrdinalIgnoreCase)
            && !familyName.Equals("Helvetica", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // No italic face is bundled -- simulate it (PdfSharp skews the glyphs) rather than
        // returning null, since no field map in this project currently requests italic text.
        return new FontResolverInfo(isBold ? BoldFace : RegularFace, false, isItalic);
    }

    public byte[]? GetFont(string faceName)
    {
        var fileName = faceName switch
        {
            RegularFace => "LiberationSans-Regular.ttf",
            BoldFace => "LiberationSans-Bold.ttf",
            _ => null,
        };
        if (fileName == null) return null;

        var path = Path.Combine(_fontDirectory, fileName);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}
