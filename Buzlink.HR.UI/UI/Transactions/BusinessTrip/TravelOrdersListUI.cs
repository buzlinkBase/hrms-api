using System;
using System.Drawing;
using Buzlink.HR.Business;
using Buzlink.HR.Domain;
using Buzlink.HR.UI.Utilities;
using DevExpress.XtraGrid.Views.Grid;

namespace Buzlink.HR.UI.UI.Transactions
{
    public partial class TravelOrdersList : DevExpress.XtraEditors.XtraForm
    {
        public TravelOrdersList()
        {
            InitializeComponent();
            clear();
        }

        void clear()
        {
            using (var command=new UCommand())
            {
                var empcontroller =  new EmployeeController(command);
                var employees = empcontroller.FindAll();
                employeeName.Properties.DataSource = employees;
                repositoryItemLookUpEdit1.DataSource = employees;
            }
            ViewData();
        }
        void ViewData()
        {

            try
            {
                using (var command=new UCommand())
                {
                    if (checkEdit1.Checked || (int)employeeName.EditValue == 0)
                        gridControl1.DataSource = command.repository.Where<TravelOrderApplication>($"ApplicationDate between '{dateEdit1.DateTime.ToMysqlDate()}' and   '{dateEdit2.DateTime.ToMysqlDate()}'","", "");
                    else
                        gridControl1.DataSource = command.repository.Where<TravelOrderApplication>($"ApplicationDate between '{dateEdit1.DateTime.ToMysqlDate()}' and   '{dateEdit2.DateTime.ToMysqlDate()}' and EmployeeId={employeeName.EditValue}","", "");
                }
            }
            catch (Exception e)
            {
            }
        }

        private void TravelOrdersList_Load(object sender, EventArgs e)
        {
            dateEdit1.DateTime = DateTime.Now;
            dateEdit2.DateTime = DateTime.Now;
        }

        private void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            DisplayForms.ViewFormFloat(new TravelOrdersEntryUI());
            ViewData();
        }

        private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            try
            {
                var model = gridView1.GetFocusedRow() as TravelOrderApplication;
                if (model.Status=="Approved" || model.Status=="Cancelled")
                {
                    throw new Exception($"Cannot modify request that is already {model.Status.ToLower()}.");
                }

                DisplayForms.ViewFormFloat(new TravelOrdersEntryUI(model));

            }
            catch (Exception ex)
            {
                Msg.Critical(ex.Message);
            }
        }

        private void barButtonItem4_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            UpdateStatus("Cancelled");
        }

        void UpdateStatus(string status)
        {
            var model = gridView1.GetFocusedRow() as TravelOrderApplication;
            model.Status = status;
            using (var c=new UCommand())
            {
                c.repository.Update(model);
                c.Commit();
            } 
        }

        private void barButtonItem3_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            UpdateStatus("Approved");
        }

       
        private void gridView1_RowStyle(object sender, DevExpress.XtraGrid.Views.Grid.RowStyleEventArgs e)
        {
            GridView View = sender as GridView;
            if (e.RowHandle >= 0)
            {
                string category = View.GetRowCellDisplayText(e.RowHandle, View.Columns["Status"]);
                if (category == "Approved")
                {
                    e.Appearance.ForeColor = Color.White;
                    e.Appearance.BackColor = Color.LightSeaGreen;
                    e.HighPriority = true;
                }
                else if(category=="Cancelled")
                {
                    e.Appearance.ForeColor = Color.White;
                    e.Appearance.BackColor = Color.OrangeRed;
                    e.HighPriority = true;
                }
            }
        }

        private void barButtonItem5_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            ViewData();
        }
    }
}