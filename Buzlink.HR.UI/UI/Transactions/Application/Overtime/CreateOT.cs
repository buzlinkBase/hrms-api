using System;
using Buzlink.HR.UI.Utilities;
using Buzlink.HR.Domain;
using Buzlink.HR.Business;
using Buzlink.HR.Domain.MasterRecord.Application;

namespace Buzlink.HR.UI
{
    public partial class CreateOT : DevExpress.XtraEditors.XtraForm
    {
        public delegate void OnSavedHandler();
        public event OnSavedHandler OnSaved;
        private OverTimeApplicationEntity  model;
        private EmployeeScheduleTemplate Schedule;
        public CreateOT()
        {
            InitializeComponent();
            clear();
        }
        public CreateOT(OverTimeApplicationEntity model)
        {
            InitializeComponent();
            clear();
            this.model = model;
            employeeName.Enabled = false;
            employeeName.EditValue = model.EmployeeId;
            memoEdit1.Text = model.Reason;
            startdate.DateTime = model.StartDate;
            endDate.DateTime = model.EndDate;
        }

        void clear()
        {
            try
            {
                model = new OverTimeApplicationEntity();
                employeeName.Enabled = true;
                memoEdit1.Text = "";
                startdate.DateTime = DateTime.Today;
                endDate.DateTime = DateTime.Today;
                using (var command = new UCommand())
                {
                    employeeName.Properties.DataSource = new EmployeeController(command).FindAll();
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
            if (emp == null)
            {
                Msg.Critical("Invalid employee name");
                return false;
            }
            var day = (endDate.DateTime - startdate.DateTime).Hours;
			if (day<0)
			{
                Msg.Critical("Invalid OT time range");
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
                model.EmployeeId = emp.Id;
                model.FullName = emp.FullName;
                model.StartDate = startdate.DateTime;
                model.EndDate = endDate.DateTime;
                model.Reason = memoEdit1.Text;
                model.Day = (endDate.DateTime - startdate.DateTime).Days;
                model.Hour = (endDate.DateTime - startdate.DateTime).Hours;
                model.Minutes = (endDate.DateTime - startdate.DateTime).Minutes;
                using (var c = new UCommand())
                {
                    new OverTimeController(c).AddOrUpdate(model);
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
                   
                    Schedule = new EmployeeScheduleController(command).FindEmployeeSchedule(emp);
                    if (Schedule == null)
                    {
                        Msg.Critical("Setup employee Work schedule first");
                        //Close();
                    }
					else
					{
                        startdate.DateTime = new DateTime(startdate.DateTime.Year, startdate.DateTime.Month, startdate.DateTime.Day, Schedule.StartOT.Hour, startdate.DateTime.Minute, 0);
                        endDate.DateTime = new DateTime(startdate.DateTime.Year, startdate.DateTime.Month, startdate.DateTime.Day, Schedule.StartOT.Hour, startdate.DateTime.Minute, 0);
					}
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
                //if (Schedule == null) return; 
                //TODO add OT Schedule validator
            }
            catch (Exception ex)
			{
			}
        }

		private void endDate_EditValueChanged(object sender, EventArgs e)
		{
            //TODO add OT Schedule validator
        }
    }
}