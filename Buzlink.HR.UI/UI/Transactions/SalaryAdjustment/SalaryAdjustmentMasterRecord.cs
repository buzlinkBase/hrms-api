using Buzlink.HR.Business;
using Buzlink.HR.Domain;
using Buzlink.HR.UI.Utilities;
using System;
using System.Drawing;

namespace Buzlink.HR.UI
{
	public partial class SalaryAdjustmentMasterRecord   : DevExpress.XtraEditors.XtraForm
    {
        public SalaryAdjustmentMasterRecord()
        {
            InitializeComponent();
        }

        private void barButtonItem4_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            ViewRecord();
        }

        void ViewRecord()
        {
            try
            {
                using (var command=new UCommand())
                {
					var data = new SalaryAdjustmentController(command).FindAll(dateRangeFilter1.FromDate, dateRangeFilter1.ToDate);
                    GridControl2.DataSource = data;
                    gridView1.OptionsView.ColumnAutoWidth = true;
                    gridView1.BestFitColumns();
                }
            } 
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }

        private void dateRangeFilter1_OnFilterChange(Extensions.DateRange range)
        {
            ViewRecord();
        }

        private void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            var frm = new CreateSalaryAdjustment();
			frm.OnSaved += Frm_OnSaved; ;
            DisplayForms.ViewFormDialog(frm);
        }

		private void Frm_OnSaved()
		{ 
            ViewRecord();
            var handle = gridView1.FocusedRowHandle;
            gridView1.CollapseMasterRow(handle);
        }

		private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            var model = gridView1.GetFocusedRow() as SalaryAdjustmentEntity;
			if (model==null)
			{
                Msg.Inform("Select transaction first");
                return;
			}
            var frm = new CreateSalaryAdjustment(model);
            frm.OnSaved += Frm_OnSaved; ;
            DisplayForms.ViewFormDialog(frm);
        }

        private void barButtonItem3_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            Cancel();
        }
        void Cancel()
        {
			try
			{
                var model = gridView1.GetFocusedRow() as SalaryAdjustmentEntity;
                if (model == null)
                {
                    Msg.Inform("Select transaction first");
                    return;
                }
                if (!Msg.ContinueCancel()) return;
                using (var command = new UCommand())
                {
                    new SalaryAdjustmentController(command).Cancel(model);
                    if (command.Commit()) ViewRecord();
                }
            }
			catch (Exception ex)
			{
                Msg.Error(ex);
			}
        }

        
	 

		private void gridView1_RowCellStyle(object sender, DevExpress.XtraGrid.Views.Grid.RowCellStyleEventArgs e)
		{
			try
			{
				if (e.Column.FieldName=="Status")
				{
                    var status = e.CellValue.ToString();
					if (status == "Cancelled")
					{
                        e.Appearance.BackColor = Color.Red;
                        e.Appearance.ForeColor = Color.White;
					}
					else if (status=="Approved")
					{
                        e.Appearance.BackColor = Color.Green;
                        e.Appearance.ForeColor = Color.White;
                    }
					else
					{
                        e.Appearance.BackColor = Color.Salmon;
                        e.Appearance.ForeColor = Color.White;
                    }
                }
            }
			catch (Exception ex)
			{
			}
		}

		private void barButtonItem5_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
		{
            ExcelExporter.Export(GridControl2, Text);
		} 
	}
}