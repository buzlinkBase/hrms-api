using Buzlink.HR.Business;
using Buzlink.HR.Domain;
using Buzlink.HR.UI.Utilities;
using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Buzlink.HR.UI.UI.Transactions.Application.Leave
{
    public partial class LeaveMasterRecord : DevExpress.XtraEditors.XtraForm
    {
        public LeaveMasterRecord()
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
                    var data= new LeaveApplicationController(command).FindRange(dateRangeFilter1.FromDate, dateRangeFilter1.ToDate, "%");
                    gridControl1.DataSource = data;
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
            var leaveFrm = new CreateLeaveApplication();
            leaveFrm.OnSaved += LeaveFrm_OnSaved;
            DisplayForms.ViewFormDialog(leaveFrm);
        }

        private void LeaveFrm_OnSaved()
        {
            ViewRecord();  
        }

        private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            var model = gridView1.GetFocusedRow() as LeaveApplicationEntity;
			if (model==null)
			{
                Msg.Inform("Select transaction first");
                return;
			}
            var leaveFrm = new CreateLeaveApplication(model);
            leaveFrm.OnSaved += LeaveFrm_OnSaved;
            DisplayForms.ViewFormDialog(leaveFrm);
        }

        private void barButtonItem3_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            Cancel();
        }
        void Cancel()
        {
			try
			{
                var model = gridView1.GetFocusedRow() as LeaveApplicationEntity;
                if (model == null)
                {
                    Msg.Inform("Select transaction first");
                    return;
                }
                if (!Msg.ContinueCancel()) return;
                using (var command = new UCommand())
                {
                    new LeaveApplicationController(command).Cancel(model);
                    if (command.Commit()) ViewRecord();
                }
            }
			catch (Exception ex)
			{
                Msg.Error(ex);
			}
        }

        void Approve()
        {
            try
            {
                var model = gridView1.GetFocusedRow() as LeaveApplicationEntity;
                if (model == null)
                {
                    Msg.Inform("Select transaction first");
                    return;
                }
                if (!Msg.ContinueApprove()) return;
                using (var command = new UCommand())
                {
                    new LeaveApplicationController(command).Approve(model);
					if (command.Commit()) ViewRecord();
                }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }

		private void barButtonItem7_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
		{
            Approve();
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
	}
}