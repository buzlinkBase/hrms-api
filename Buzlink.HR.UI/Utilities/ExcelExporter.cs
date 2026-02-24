using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.BandedGrid;
using DevExpress.XtraGrid.Views.Grid;
using System.Diagnostics;


namespace Buzlink.HR.UI
{
    public static class ExcelExporter
    {
        public static void Export(this GridControl gridcontrol, string fileName)
        {
            if (!gridcontrol.IsPrintingAvailable)
            {
                Msg.Inform("Printer library not found.");
            }
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.DefaultExt = ".xlsx";
            dlg.Filter = "Excel File|*.xlsx|All Files|*.*";
            dlg.FileName = fileName;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                gridcontrol.ExportToXlsx(fileName);
                Process.Start(fileName);
            }
        }
        public static void Export(this GridView view, string fileName)
        {
            if (!view.GridControl.IsPrintingAvailable)
            {
                Msg.Inform("Printer library not found.");
            }
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.DefaultExt = ".xlsx";
            dlg.Filter = "Excel File|*.xlsx|All Files|*.*";
            dlg.FileName = fileName;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                view.ExportToXlsx(fileName);
                Process.Start(fileName);
            }
        }
        public static void Export(this BandedGridView view, string fileName)
        {
            if (!view.GridControl.IsPrintingAvailable)
            {
                Msg.Inform("Printer library not found.");
            }
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.DefaultExt = ".xlsx";
            dlg.Filter = "Excel File|*.xlsx|All Files|*.*";
            dlg.FileName = fileName;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                view.ExportToXlsx(fileName);
                Process.Start(fileName);
            }
        }
    }
}
