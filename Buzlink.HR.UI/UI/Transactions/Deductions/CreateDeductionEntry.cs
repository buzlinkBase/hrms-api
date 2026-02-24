namespace Buzlink.HR.UI
{
    public partial class CreateDeductionEntry : DevExpress.XtraEditors.XtraForm
    {
        public delegate void OnSavedHandler();
        public event OnSavedHandler OnSaved;
        private Deduction model;

        public CreateDeductionEntry()
        {
            InitializeComponent();
            clear();
        }

        public CreateDeductionEntry(Deduction model)
        {
            InitializeComponent();
            clear();
            this.model = model;
            LoadDataForUpdate();
        }
        private void LoadDataForUpdate()
        {
            try
            {
                //deductionName.EditValue = model.DeductionId;
                //employeeName.EditValue = model.EmployeeId;
                //principal.EditValue = model.TotalPrincipal;
                //interest.EditValue = model.InterestRate;
                //totalAmount.EditValue = model.TotalAmount;
                //startdate.DateTime = model.StartDate;
                //enddate.DateTime = model.EndDate;
                //terms.EditValue = model.Terms;
                //memoEdit1.Text = model.Note;

                //using (var command = new UCommand())
                //{
                //    if (model.Details == null)
                //    {
                //        model.Details = new DeductionRecordController(command).FindDetails(model.Id);
                //    }
                //}
                //gridControl1.DataSource = model.Details;

            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }
        void clear()
        {
            //try
            //{
            //    using (var c=new UCommand())
            //    {
            //        employeeName.Properties.DataSource = new EmployeeController(c).FindAll();
            //        deductionName.Properties.DataSource =  c.repository.Where<DeductionEntity>("", "","");
            //    }
            //    model = new Deduction();
            //    UpdateData(model); 
            //}
            //catch (Exception)
            //{
            //}
        }
        void UpdateData(Deduction record)
        {
            //if (record == null) record = new  Deduction();
            //gridControl1.DataSource = record.Details;
        }

        void ComputeTotal()
        {

            try
            {
                totalAmount.Value = principal.Value + principal.Value * (interest.Value / 100);
                ComputeBreakdown();
            }
            catch (Exception ex)
            {
            }
        }
        private void DeductionEntryUI_Load(object sender, EventArgs e)
        {
        }

        void ComputeBreakdown()
        {
            try
            {
                //model.Details.Clear();
                //var date = startdate.DateTime;
                //for (int i = 0; i < terms.Value; i++)
                //{
                //    var principalAmt = (principal.Value / terms.Value);
                //    var interestAmt = (principal.Value / terms.Value) * interest.Value / 100;
                //    model.Details.Add(new DeductionRecordDetailEntity() { Date = date, Description = i + 1 + "" + Ordinal.GetOrdinal(i + 1), Principal = principalAmt, Interest = interestAmt  });
                //    if (mode.Text == "SemiMonthly")
                //    {
                //        //base on employment type
                //        date = date.Date.AddDays(15);
                //    }
                //    else
                //    {
                //        //monthly
                //        //base on payment schedule.
                //        date = date.Date.AddMonths(1);
                //    }
                //}
                //gridControl1.DataSource = model.Details;
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
                //using (var c=new UCommand())
                //{
                //    var emp = employeeName.GetSelectedDataRow() as EmployeeEntity;
                //    var pos = c.repository.FindOne<Position>(emp.PositionId,"");
                //    position.Text = pos.Name;
                //    var dep = c.repository.FindOne<Department>(emp.DepartmentId,"");
                //    department.Text = dep.Name;
                //}
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
                //var ed = model.Details.Max(x => x.Date);
                //enddate.DateTime = ed.Date;
            }
            catch (Exception ex)
            {
            }
        }
        private void gridView1_CellValueChanged(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e)
        {
            GetEndDate();
        }
        bool valivation()
        {
            //var emp = employeeName.GetSelectedDataRow() as EmployeeEntity;
            //var deduction  = deductionName.GetSelectedDataRow() as DeductionEntity;
            //if (emp == null)
            //{
            //    Msg.Critical("Invalid employee name");
            //    return false;
            //}
            //if (deduction == null)
            //{
            //    Msg.Critical("Invalid deduction name");
            //    return false;
            //}
            return true;
        }

        void save()
        {
            try
            {
                if (!valivation()) return;
                if (!Msg.ContinueSave()) return;
                //           var emp = employeeName.GetSelectedDataRow() as EmployeeEntity;
                //           var deduction = deductionName.GetSelectedDataRow() as DeductionEntity;
                //           model.DeductionId = deduction.Id;
                //           model.DeductionName = deduction.Name;
                //           model.EmployeeId = emp.Id;
                //           model.FullName = emp.FullName;

                //           model.TotalPrincipal = principal.Value;
                //           model.InterestRate = interest.Value;
                //           model.TotalAmount = totalAmount.Value;

                //           model.StartDate = startdate.DateTime;
                //           model.EndDate = enddate.DateTime;
                //           model.EncodeDate = DateTime.Today;
                //           model.ProcessBy = globalInfo.user.FullName;
                //           model.Note = memoEdit1.Text.Trim();

                //           model.modeOfPayment = mode.Text;
                //           model.Terms =(int) terms.Value;

                //           using (var c = new UCommand())
                //           {
                //               new DeductionRecordController(c).AddOrUpdate(model);
                //if (c.Commit())
                //{
                //                   OnSaved?.Invoke();
                //                   Close();
                //               }
                //           }
            }
            catch (Exception ex)
            {
            }
        }


        private void employeeName_EditValueChanged_1(object sender, EventArgs e)
        {
            try
            {
                //using (var c=new UCommand())
                //{
                //    var emp = employeeName.GetSelectedDataRow() as EmployeeEntity;
                //    position.Text = new PositionController(c).FindOne(emp.PositionId)?.Name;
                //    department.Text = new DepartmentController(c).FindOne(emp.DepartmentId)?.Name;
                //} 
            }
            catch (Exception ex)
            {
            }
        }

        private void deductionName_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            //if (e.Button.Caption=="+")
            //{
            //    var frm = new DeductionSetupUI();
            //    frm.OnSaved += Frm_OnSaved;
            //    DisplayForms.ViewFormDialog(frm);

            //}
        }

        //private void Frm_OnSaved(DeductionEntity deduction)
        //{
        //    LoadDeductions();
        //}

        void LoadDeductions()
        {
            //try
            //{
            //    using (var c = new UCommand())
            //    {
            //        deductionName.Properties.DataSource = new DeductionSetupController(c).FindAll();
            //    }
            //}
            //catch (Exception ex)
            //{
            //}
        }

        private void repositoryItemButtonEdit1_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            gridView1.DeleteSelectedRows();
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