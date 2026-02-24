using System;
using System.Collections.Generic;
using Buzlink.HR.Business;
using Buzlink.HR.Domain;
using Buzlink.HR.UI.Utilities;

namespace Buzlink.HR.UI
{
    public partial class TravelOrdersEntryUI : DevExpress.XtraEditors.XtraForm
    {
        private TravelOrderApplication model;
        public TravelOrdersEntryUI()
        {
            InitializeComponent();
            model = new TravelOrderApplication();
            LoadEmployee();
            loadClassification();
            UpdateModel();
        }
        
        void LoadEmployee()
        {
            using (var command=new UCommand())
            {
                employeeNameLk.Properties.DataSource =command.repository.Where<Employee>("", "","");
            }
        }

        public TravelOrdersEntryUI(TravelOrderApplication model)
        {
            InitializeComponent();
            this.model = model;
            LoadEmployee();
            loadClassification();
            UpdateModel();
        }

        void UpdateModel()
        {
            appdate.DateTime = model.ApplicationDate;
            fromdate.DateTime = model.StartDate;
            todate.DateTime = model.EndDate;
            noOfDays.Value = model.Days;
            destination.Text = model.Destination;
            purpose.Text  = model.Purpose;
            cost.Value  =(Decimal) model.Cost;
            employeeNameLk.EditValue = model.EmployeeId;
            model.Purpose = purpose.Text.Trim();
            model.Cost = (double)cost.Value;
            model.EmployeeId = (int)employeeNameLk.EditValue;
            classification.Text = model.Classification;
        }
        void clear()
        {
            model = new TravelOrderApplication();
            UpdateModel();
        }
        void save()
        {

            if (!Msg.ContinueSave()) return; 
            model.ApplicationDate = appdate.DateTime;
            model.Days =(int) noOfDays.Value;
            model.StartDate = fromdate.DateTime;
            model.EndDate = todate.DateTime;
            model.Destination = destination.Text;
            model.Classification = classification.Text;
            model.Purpose = purpose.Text.Trim();
            model.Cost =(double) cost.Value;
            model.EmployeeId =(int) employeeNameLk.EditValue;
          
            using (var c = new UCommand())
            {
                c.repository.AddOrUpdate(model);
                string yr = appdate.DateTime.ToString("yyyy");
                model.Reference = "TRVL" + yr.Substring(2,2)  + "-" +
                appdate.DateTime.Month.ToString("##") + "-" + model.Id;
                c.repository.AddOrUpdate(model);
                if (c.Commit()) clear(); 
            }
        }
        private void simpleButton1_Click(object sender, EventArgs e)
        {
            clear();
        }

        bool validation()
        {
            if ((int)employeeNameLk.EditValue==0)
            {
                Msg.Inform("Invalid Employee Name");
                return false;
            }
            else if(fromdate.DateTime==todate.DateTime)
            {
                Msg.Inform("From and to Date time is invalid");
                return false;
            } 
            return true;
        }
        private void saveCommand_Click(object sender, EventArgs e)
        {
            save();
        }
         string getDays()
        {

            return (todate.DateTime.Subtract(fromdate.DateTime)).Days.ToString();
        }
        private void fromdate_EditValueChanged(object sender, EventArgs e)
        {
            noOfDays.Text = getDays();
        }

        private void todate_EditValueChanged(object sender, EventArgs e)
        {
            noOfDays.Text = getDays();
        }

        private void TravelOrdersEntryUI_Load(object sender, EventArgs e)
        {
            appdate.DateTime = DateTime.Today;
            fromdate.DateTime = DateTime.Now;
            todate.DateTime = DateTime.Now;
        }
        void loadClassification()
        {
            using (var command=new UCommand())
            {
                var classcontrol = new ClassController(command);
                classification.Properties.DataSource = classcontrol.FindAllByType("TravelOrder");
            }

        }

        private void classification_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            if (e.Button.Caption=="+")
            {
                DisplayForms.ViewFormDialog(new CategoryUI("TravelOrder"));
                loadClassification();
            }
        }

        private void classification_ProcessNewValue(object sender, DevExpress.XtraEditors.Controls.ProcessNewValueEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.DisplayValue.ToString())) return;
            using (var c=new UCommand(true))
            {
                var classModel = new GroupingSetup();
                classModel.Name = e.DisplayValue.ToString();
                classModel.Types = "TravelOrder";
                c.repository.Add(classModel);
                if (c.Commit()) {
                    var items = classification.Properties.DataSource as List<GroupingSetup>;
                    items.Add(classModel);
                }
            }
        }
    }
}