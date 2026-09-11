namespace Hrms.Api.Documents.Bir;

// One value's placement on a BIR overlay template. Coordinates are in points (1/72 inch),
// measured from the page's TOP-LEFT corner (PdfOverlayEngine draws via
// XGraphics.FromPdfPage with no XPageDirection override, which is top-left-origin/Y-down --
// the opposite of raw PDF's native bottom-left-origin convention iText and similar tools use).
// Page is 0-based.
public record PdfField(string Name, int Page, double X, double Y, double FontSize, bool Bold = false);
