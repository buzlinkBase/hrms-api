using System;
using System.Linq;
using Buzlink.HR.Business;
using Buzlink.HR.Domain;
using Buzlink.HR.UI.Utilities;

namespace Buzlink.HR.UI
{
    public partial class AllowanceEntryUI : DevExpress.XtraEditors.XtraForm
    {
        private AllowanceMainRecordEntity allowance ;

        public AllowanceEntryUI()
        {
            InitializeComponent(); 
            clear();
        }
          void  clear()
         {
            try
            {
                using (var c=new UCommand())
                {
                    employeeName.Properties.DataSource = new EmployeeController(c).FindAll();
                    allowancelk.Properties.DataSource =  c.repository.Where<Allowance>("", "","");
                }
                allowance = new AllowanceMainRecordEntity();
                UpdateData(allowance); 
            }
            catch (Exception)
            {
            }
           }
        void UpdateData(AllowanceMainRecordEntity record)
        {
            if (record == null) record = new AllowanceMainRecordEntity();
            gridControl1.DataSource = record.Details;
        } 
        private void simpleButton1_Click(object sender, EventArgs e)
        {
            clear();
        }
        void ComputeTotal()
        {
            ComputeBreakdown();
        }
        private void DeductionEntryUI_Load(object sender, EventArgs e)
        { 
        } 

        void ComputeBreakdown()
        {
            try
            {
                allowance.Details.Clear();
                var date = startdate.DateTime;
                for (int i = 0; i < terms.Value; i++)
                {
                    var principalAmt = (Amount.Value / terms.Value);
                    allowance.Details.Add(new AllowanceDetailEntity() { Date = date, Description = i + 1 + "" + Ordinal.GetOrdinal(i + 1),Amount= principalAmt });
                    if (mode.Text == "SemiMonthly")
                    {
                        //base on employment type
                        date = date.Date.AddDays(15);
                    }
                    else
                    {
                        //monthly
                        //base on payment schedule.
                        date = date.Date.AddMonths(1);
                    }
                }
                gridControl1.DataSource = allowance.Details;
                GetEndDate();
            }
            catch (Exception ex)
            {
            }
        }

        private void principal_EditValueChanged(object sender, EventArgs e)
        {
            ComputeTotal();
        }

        private void interest_EditValueChanged(object sender, EventArgs e)
        {
            ComputeTotal();
        }

        private void employeeName_EditValueChanged(object sender, EventArgs e)
        {
            try
            {
                using (var c=new UCommand())
                {
                    var emp = employeeName.GetSelectedDataRow() as EmployeeEntity;
                    var pos = c.repository.FindOne<Position>(emp.PositionId,"");
                    position.Text = pos.Name;
                    var dep = c.repository.FindOne<Department>(emp.DepartmentId,"");
                    department.Text = dep.Name;
                }
            }
            catch (Exception ex)
            {
            }
        }
        private void terms_EditValueChanged(object sender, EventArgs e)
        {
            ComputeBreakdown();
        }

        private void startdate_EditValueChanged(object sender, EventArgs e)
        {
            ComputeBreakdown();
        }

        private void mode_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComputeBreakdown();
        }

        private void period_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComputeBreakdown();
        }

        void GetEndDate()
        {
            try
            { 
                var ed = allowance.Details.Max(x => x.Date);
                enddate.DateTime = ed.Date;
            }
            catch (Exception ex)
            {
            }
        }
        private void gridView1_CellValueChanged(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e)
        {
            GetEndDate();
        }
        void save()
        {
            try
            {
                using (var c = new UCommand())
                {
                    allowance.Amount = Amount.Value;
                    //if (Allowance.Id == 0) 
                    allowance.EncodeDate = DateTime.Today;
                    allowance.StartDate = startdate.DateTime;
                    allowance.EndDate = enddate.DateTime;
                    allowance.EmployeeId = (int)employeeName.EditValue ;
                    //Allowance.Period = period.Text;
                    allowance.ProcessBy = globalInfo.user.FullName;
                    c.repository.AddOrUpdate(allowance);
                    foreach (var item in allowance.Details)
                    {
                        item.EmployeeId = allowance.EmployeeId;
                        item.mainId = allowance.Id;
                        c.repository.AddOrUpdate(item);
                    }
                    c.Commit();
                }
            }
            catch (Exception ex)
            {
            }
        }
        private void saveCommand_Click(object sender, EventArgs e)
        {
            save();
        }

        private void employeeName_EditValueChanged_1(object sender, EventArgs e)
        {
            try
            {
                using (var c=new UCommand())
                {
                    var emp = employeeName.GetSelectedDataRow() as EmployeeEntity;
                    position.Text = new PositionController(c).FindOne(emp.PositionId)?.Name;
                    department.Text = new DepartmentController(c).FindOne(emp.DepartmentId)?.Name;
                } 
            }
            catch (Exception ex)
            {
            }
        }

        private void deductionName_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            if (e.Button.Caption=="+")
            {
                var frm = new AllowanceSetupUI();
                frm.OnSaved += Frm_OnSaved;
                DisplayForms.ViewFormDialog(frm);

            }
        }

        private void Frm_OnSaved(Allowance Allowance)
        {
            loadAllowance();
        }

        void loadAllowance()
        {
            try
            {
                using (var c = new UCommand())
                {
                    allowancelk.Properties.DataSource = new AllowanceSetupController(c).FindAll();
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void repositoryItemButtonEdit1_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            gridView1.DeleteSelectedRows();
        }

        private void deductionName_EditValueChanged(object sender, EventArgs e)
        {

        }
    }
}