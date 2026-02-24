using System;
using Buzlink.HR.UI.Utilities;
using Buzlink.HR.Domain;
using Buzlink.HR.Business;

namespace Buzlink.HR.UI
{
    public partial class CreateLeaveApplication : DevExpress.XtraEditors.XtraForm
    {
        public delegate void OnSavedHandler();
        public event OnSavedHandler OnSaved  ;

        private LeaveApplicationEntity model;

        public CreateLeaveApplication()
        {
            InitializeComponent();
            clear();
        }
        public CreateLeaveApplication(LeaveApplicationEntity model)
        {
            InitializeComponent();
            clear();
            this.model = model;
            employeeName.Enabled = false;
            employeeName.EditValue = model.EmployeeId;
            leaveType.EditValue = model.leaveId;
            startdate.DateTime = model.StartDate;
            endDate.DateTime = model.EndDate;
            IsPaid.Checked = model.WithPay;
            memoEdit1.Text = model.Reason;
            type.Text = model.Type;
        }

        void clear()
        {
            try
            {
                model = new LeaveApplicationEntity();
                employeeName.Enabled = true;
                memoEdit1.Text = "";
                IsPaid.Checked = true;
                type.Text = "Whole Day";
                startdate.DateTime = DateTime.Today;
                endDate.DateTime = DateTime.Today;
                using (var command = new UCommand())
                {
                    employeeName.Properties.DataSource = new EmployeeController(command).FindAll();
                    leaveType.Properties.DataSource = new LeaveCreditSetupController(command).FindAll();
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            clear();
        }

        bool valivation()
        {
            var emp = employeeName.GetSelectedDataRow() as EmployeeEntity;
            var leave = leaveType.GetSelectedDataRow() as LeaveNameEntity;
            if (emp == null)
            {
                Msg.Critical("Invalid employee name");
                return false;
            }
            if (leave == null)
            {
                Msg.Critical("Invalid leave name");
                return false;
            }
            var day = (endDate.DateTime - startdate.DateTime).Days + 1;
			if (day<1)
			{
                Msg.Critical("Invalid leave date range");
                return false;
            }
            return true;
        }
        void Save()
        {
            try
            {
                if (!valivation()) return; 
                if (!Msg.ContinueSave()) return;
                var emp = employeeName.GetSelectedDataRow() as EmployeeEntity;
                var leave  = leaveType.GetSelectedDataRow() as LeaveNameEntity;
                model.EmployeeId = emp.Id;
                model.FullName = emp.FullName;
                model.leaveId = leave.Id;
                model.LeaveName = leave.Description;

                model.StartDate = startdate.DateTime;
                model.EndDate = endDate.DateTime;
                model.WithPay = IsPaid.Checked;
                model.Reason = memoEdit1.Text;
                
                var day = (endDate.DateTime - startdate.DateTime).Days + 1;
                if (day>1)
                {
                    model.Type = "Whole Day";
                    model.LeaveHour = (day ) * 8;
                }
                else
                {
                    model.Type = type.Text;
                    if (model.Type=="Half Day")
                        model.LeaveHour = 4;
                    else
                        model.LeaveHour = (day) * 8;
                }
                using (var c = new UCommand())
                {
                    new LeaveApplicationController(c).AddOrUpdate(model);
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
            Save();
        }

        private void employeeName_EditValueChanged(object sender, EventArgs e)
        {
            try
            {
                var emp = employeeName.GetSelectedDataRow() as EmployeeEntity;
                if (emp == null) return;

                using (var command=new UCommand())
                {
                    departmenttxt.Text = new DepartmentController(command).FindOne(emp.DepartmentId)?.Name;
                    positiontxt.Text = new PositionController(command).FindOne(emp.PositionId)?.Name;
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            Save();
        }

        private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            clear();
        }

		private void startdate_EditValueChanged(object sender, EventArgs e)
		{
            try
            {
                var day = (endDate.DateTime - startdate.DateTime).Days + 1;
                type.Enabled = day == 1;
                if (day > 1)
                {
                    type.Text = "Whole Day";
                }
            }
            catch (Exception ex)
            {
            }
        } 
	}
}