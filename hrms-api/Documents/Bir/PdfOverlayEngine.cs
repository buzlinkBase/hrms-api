using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Hrms.Api.Documents.Bir;

// Stamps computed values onto a copy of an official (non-fillable) BIR PDF template, at the
// coordinates given by each form's field map -- see Bir2316FieldMap/Bir1601CFieldMap. The
// template file on disk is never opened for writing: it's read into memory first, and only the
// resulting in-memory document is ever saved (to a fresh output stream).
public static class PdfOverlayEngine
{
    public static byte[] Render(
        string templatePath,
        IReadOnlyList<PdfField> fields,
        IReadOnlyDictionary<string, string?> values,
        bool debug = false)
    {
        var templateBytes = File.ReadAllBytes(templatePath);
        using var templateStream = new MemoryStream(templateBytes);
        using var pdf = PdfReader.Open(templateStream, PdfDocumentOpenMode.Modify);

        foreach (var pageFields in fields.GroupBy(f => f.Page))
        {
            var pageIndex = pageFields.Key;
            if (pageIndex < 0 || pageIndex >= pdf.Pages.Count)
            {
                throw new InvalidOperationException(
                    $"Field map references page {pageIndex}, but '{Path.GetFileName(templatePath)}' only has {pdf.Pages.Count} page(s).");
            }

            // One XGraphics per page, reused for every field on it -- XGraphics.FromPdfPage
            // should be called once per page, not once per DrawString.
            using var gfx = XGraphics.FromPdfPage(pdf.Pages[pageIndex]);

            foreach (var field in pageFields)
            {
                values.TryGetValue(field.Name, out var value);
                var hasValue = !string.IsNullOrEmpty(value);

                if (hasValue)
                {
                    var font = new XFont(
                        BirFontResolver.FamilyName,
                        field.FontSize,
                        field.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                    gfx.DrawString(value, font, XBrushes.Black, new XPoint(field.X, field.Y));
                }

                if (debug)
                {
                    DrawCalibrationMarker(gfx, field, hasValue);
                }
            }
        }

        using var outStream = new MemoryStream();
        pdf.Save(outStream, closeStream: false);
        return outStream.ToArray();
    }

    // Dev-only aid (see CalibrationOverlay in the controller) for lining up field coordinates
    // against the real form: a small red crosshair at the anchor point plus the field's name,
    // drawn regardless of whether a value was written, so an empty/blank field's position is
    // still visible while calibrating.
    static void DrawCalibrationMarker(XGraphics gfx, PdfField field, bool hasValue)
    {
        var markerColor = hasValue ? XColors.Red : XColors.Orange;
        var pen = new XPen(markerColor, 0.5) { DashStyle = XDashStyle.Solid };
        gfx.DrawLine(pen, field.X - 4, field.Y, field.X + 4, field.Y);
        gfx.DrawLine(pen, field.X, field.Y - 4, field.X, field.Y + 4);

        var labelFont = new XFont(BirFontResolver.FamilyName, 6, XFontStyleEx.Regular);
        gfx.DrawString(field.Name, labelFont, new XSolidBrush(markerColor), new XPoint(field.X + 5, field.Y - 3));
    }
}
