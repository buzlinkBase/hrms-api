using System;
using System.Linq;
using Buzlink.HR.Business;
using Buzlink.HR.Domain;
using Buzlink.HR.UI.Utilities;

namespace Buzlink.HR.UI
{
    public partial class CreateSalaryAdjustment  : DevExpress.XtraEditors.XtraForm
    {
        public delegate void OnSavedHandler();
        public event OnSavedHandler OnSaved;
        private SalaryAdjustmentEntity  model  ;

        public CreateSalaryAdjustment() 
        {
            InitializeComponent(); 
            clear();
        }
        public CreateSalaryAdjustment(SalaryAdjustmentEntity model)
        {
            InitializeComponent();
            clear();
            this.model = model;

            employeeName.EditValue = model.EmployeeId;
            Amount.EditValue = model.Amount;
            remarks.Text = model.Remarks;
            period.DateTime = model.PayrollPeriod;
        }
        void  clear()
         {
            try
            {
                using (var c=new UCommand())
                {
                    employeeName.Properties.DataSource = new EmployeeController(c).FindAll();
                }
                model = new SalaryAdjustmentEntity();
                remarks.Text = "";
                Amount.Value = 0;
            }
            catch (Exception)
            {
            }
           }
         
        private void simpleButton1_Click(object sender, EventArgs e)
        {
            clear();
        }
        private void employeeName_EditValueChanged(object sender, EventArgs e)
        {
            try
            {
                using (var c=new UCommand())
                {
                    var emp = employeeName.GetSelectedDataRow() as Employee;
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

        bool validation()
		{
            var emp = employeeName.GetSelectedDataRow() as Employee;
			if (emp==null)
			{
                Msg.Inform("select employee name");
                return false;
            }
            if (string.IsNullOrWhiteSpace(remarks.Text))
            {
                Msg.Inform("Invalid remarks");
                return false;
            }
            else if (Amount.Value == 0)
            {
                Msg.Inform("Invalid amount");
                return false;
            }
            return true;
        }
        void save()
        {
            try
            {
                if (!validation()) return; 
                if (!Msg.ContinueSave()) return;
                var emp = employeeName.GetSelectedDataRow() as Employee;
                model.EmployeeId = emp.Id;
                model.FullName = emp.FullName;
                model.ProcessBy = globalInfo.user.FullName;
                model.Amount = Amount.Value;
                model.Remarks = remarks.Text.Trim();
                model.PayrollPeriod = period.DateTime;
                
                using (var c = new UCommand())
                {
                    new SalaryAdjustmentController(c).AddOrUpdate(model);
                    if (c.Commit())
                    {
                        OnSaved?.Invoke();
                        Close();
                    }
                }

            }
            catch (Exception ex)
            {
                Msg.Error(ex);
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
                    var emp = employeeName.GetSelectedDataRow() as Employee;
                    position.Text = new PositionController(c).FindOne(emp.PositionId)?.Name;
                    department.Text = new DepartmentController(c).FindOne(emp.DepartmentId)?.Name;
                } 
            }
            catch (Exception ex)
            {
            }
        }

		private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
		{
            clear();
		}

		private void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
		{
            save();
		}
	}
}