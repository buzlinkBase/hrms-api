using Buzlink.HR.UI.Utilities;
using Hrms.Domain;
using System.Data;
using System.IO;

namespace Buzlink.HR.UI
{
    public partial class EmployeeUI : DevExpress.XtraEditors.XtraForm
    {
        public delegate void OnAddOrUpdateHandler(Employee employee);
        public event OnAddOrUpdateHandler OnAddOrUpdate;
        private Employee employee;
        private List<string> statuses = new List<string>() { "Active", "Retired", "Resigned", "AWOL", "Terminated" };
        private string[] days = new string[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        private readonly IScopeFactory _scopeFactory;
        public EmployeeUI(IScopeFactory scopeFactory)
        {
            InitializeComponent();
            _scopeFactory = scopeFactory;
            employee = new Employee();
        }

        public void LoadModel(Employee model)
        {
            employee = model;
            SetControlValues();
        }

        async Task reloadInitData()
        {
            try
            {

                var wsTask = Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    return await scope.GetRequiredService<TimeShiftService>().FindAllAsync();
                });

                var deptTask = Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    return await scope.GetRequiredService<DepartmentService>().FindAllAsync();
                });

                var posTask = Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    return await scope.GetRequiredService<PositionService>().FindAllAsync();
                });

                // Now results will be an array of object[] (or whatever type FindAllAsync returns)
                await Task.WhenAll(wsTask, deptTask, posTask);

                // Assign results to your controls
                workschedule.Properties.DataSource = await wsTask;
                department.Properties.DataSource = await deptTask;
                Position.Properties.DataSource = await posTask;

                //using (var scope = _scopeFactory.CreateScope())
                //{
                //    var ws = await scope.GetRequiredService<TimeShiftService>().FindAllAsync();
                //    var dept = await scope.GetRequiredService<DepartmentService>().FindAllAsync();
                //    var pos = await scope.GetRequiredService<PositionService>().FindAllAsync();

                //    Position.Properties.DataSource = pos;
                //    workschedule.Properties.DataSource = ws;
                //    department.Properties.DataSource = dept;
                //}

                var stats = statuses.Select(x => new { Name = x }).ToList();
                empstatus.Properties.DataSource = stats;
                empstatus.EditValue = "Active";
            }
            catch (Exception ex)
            {
                ErrorHandler.Handle(ex);
            }
        }

        async Task loadDepartment()
        {

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dept = await scope.GetRequiredService<DepartmentService>().FindAllAsync();
                department.Properties.DataSource = dept;
            }
            catch (Exception ex)
            {
            }
        }
        async Task LoadSection(Guid deptId)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dept = await scope.GetRequiredService<SectionService>().FindAllAsync();
                department.Properties.DataSource = dept;
            }
            catch (Exception ex)
            {
            }
        }
        async Task loadPosition()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var pos = await scope.GetRequiredService<PositionService>().FindAllAsync();
                Position.Properties.DataSource = pos;
            }
            catch (Exception ex)
            {
            }
        }

        void SetControlValues()
        {
            try
            {
                bioId.EditValue = employee.BioId;
                empNo.Text = employee.EmployeeNo;
                firstName.Text = employee.FirstName;
                LastName.Text = employee.LastName;
                middleName.Text = employee.MiddleName;
                Suffix.Text = employee.Suffix;
                dateOfBirth.DateTime = employee.DOB ?? DateTime.MinValue;
                gender.Text = employee.Gender;
                bloodType.Text = employee.BloodType;
                empstatus.EditValue = employee.Status;
                civilstatus.Text = employee.CivilStatus;

                //employment
                department.EditValue = employee.DepartmentId;
                section.EditValue = employee.SectionId;
                Position.EditValue = employee.PositionId;
                workschedule.EditValue = employee.TimeShiftId;
                setOff();

                //var empType = Enum.GetNames(typeof(EmployeeType)).Select(x => new { Name = x });
                //var empTypes = Enum.GetNames(typeof(EmploymentType)).Select(x => new { Name = x });
                //employeetypelk.EditValue = employee.EmployeeType;
                //employeetypelk.Properties.DataSource = empType;
                //employmentType.Properties.DataSource = empTypes;
                employmentType.EditValue = employee.EmploymentStatus;
                rate.EditValue = employee.MonthlyRate;
                colarate.EditValue = employee.Cola;
                startDate.DateTime = employee.ContractStart ?? DateTime.MinValue;
                endDate.DateTime = employee.ContractEnd ?? DateTime.MinValue;
                resignedDate.DateTime = employee.DateResigned ?? DateTime.MinValue;
                bankname.Text = employee.BankName;
                BankAccountNo.Text = employee.BankNo;

                SSS.Text = employee.SSSNo;
                PHIC.Text = employee.PHICNo;
                HDMF.Text = employee.HDMFNo;
                TIN.Text = employee.TIN;

                skillsGrid.DataSource = new BindingList<Skill>(employee.Skills.ToList());
                educGrid.DataSource = new BindingList<Education>(employee.Educations.ToList());
                employmentGrid.DataSource = new BindingList<EmploymentHistory>(employee.Employments.ToList());
                dependentGrid.DataSource = new BindingList<Dependent>(employee.Dependents.ToList());
                employeeRecordGrid.DataSource = new BindingList<EmployeeRecord>(employee.EmployeeRecords.ToList());
                assetsGrid.DataSource = new BindingList<AssignAsset>(employee.Assets.ToList());
                //pictureEdit1.EditValue = StringToImage(employee.Image?.Image);
            }
            catch (Exception ex)
            {
            }
        }
        void setOff()
        {
            try
            {
                var schedOff = (days.Select(x => new DayOff { Selection = false, Days = x })).ToList();
                //string[] empOff = employee?.RestDays.ToList().Select(x=>x.).ToArray();
                //?.Split(',');
                //if (empOff != null)
                //{
                //    schedOff.ForEach(x =>
                //    {
                //        foreach (var item in empOff)
                //        {
                //            if (item == x.Days)
                //            {
                //                x.Selection = true;
                //            }
                //        }
                //    });
                //}
                gridControl1.DataSource = schedOff;
            }
            catch (Exception ex)
            {
            }
        }
        void getOff()
        {
            var schedOff = (List<DayOff>)gridControl1.DataSource;
            string offs = "";
            schedOff.ForEach(x =>
            {
                offs += "";
            });
            if (offs.EndsWith("|", StringComparison.InvariantCultureIgnoreCase))
            {
                offs = offs.Remove(offs.Length - 1, 1);
            }
            //employee.RestDay = offs;
        }
        void setEmployeeModel()
        {
            gridView1.EndEdit();
            gridView2.EndEdit();
            gridView4.EndEdit();
            gridView5.EndEdit();
            gridView7.EndEdit();
            gridView8.EndEdit();
            gridView9.EndEdit();

            int.TryParse(bioId.Text, out int BioId);
            employee.BioId = BioId;
            employee.EmployeeNo = empNo.Text;
            employee.FirstName = firstName.Text;
            employee.LastName = LastName.Text;
            employee.MiddleName = middleName.Text;
            employee.Suffix = Suffix.Text;
            employee.DOB = dateOfBirth.DateTime;
            employee.Gender = gender.Text;
            employee.BloodType = bloodType.Text;
            employee.Status = empstatus.EditValue.ToString() ?? "Active";
            employee.CivilStatus = civilstatus.Text;
            employee.Address1 = address1.Text;
            employee.Address2 = address2.Text;

            //employment
            //string DepartmentId = 0;
            //string SectionId = 0;
            //string positionId = 0;
            //int workSchedule = 0;
            //var x1 = int.TryParse(department.EditValue.ToString(), out DepartmentId);
            //var x2 = int.TryParse(section.EditValue.ToString(), out SectionId);
            //var x3 = int.TryParse(Position.EditValue.ToString(), out positionId);
            //var x4 = int.TryParse(workschedule.EditValue.ToString(), out workSchedule);

            employee.DepartmentId = (Guid)department.EditValue;
            employee.SectionId = (Guid)section.EditValue;
            employee.PositionId = (Guid)Position.EditValue;
            employee.TimeShiftId = (Guid)workschedule.EditValue;
            getOff();
            //employee.EmployeeType = employeetypelk.EditValue == null ? "" : employeetypelk.EditValue.ToString();
            Enum.TryParse<EmploymentStatus>(employmentType.EditValue.ToString(), true, out var empstat);
            employee.EmploymentStatus = empstat;
            employee.MonthlyRate = rate.Value;
            employee.Cola = colarate.Value;
            employee.ContractStart = startDate.DateTime;
            employee.ContractEnd = endDate.DateTime;
            employee.DateResigned = resignedDate.DateTime;
            employee.BankName = bankname.Text;
            employee.BankNo = BankAccountNo.Text;

            employee.SSSNo = SSS.Text;
            employee.PHICNo = PHIC.Text;
            employee.HDMFNo = HDMF.Text;
            employee.TIN = TIN.Text;

            employee.Skills = ((BindingList<Skill>)skillsGrid.DataSource).ToList();
            employee.Educations = ((BindingList<Education>)educGrid.DataSource).ToList();
            employee.Employments = ((BindingList<EmploymentHistory>)employmentGrid.DataSource).ToList();
            employee.Dependents = ((BindingList<Dependent>)dependentGrid.DataSource).ToList();
            employee.EmployeeRecords = ((BindingList<EmployeeRecord>)employeeRecordGrid.DataSource).ToList();
            employee.Assets = ((BindingList<AssignAsset>)assetsGrid.DataSource).ToList();

        }

        public byte[] imageToByteArray(System.Drawing.Image imageIn)
        {
            try
            {
                MemoryStream ms = new MemoryStream();
                imageIn.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        string ImgToString()
        {
            try
            {
                var img = imageToByteArray(pictureEdit1.Image);
                string data = Convert.ToBase64String(img);
                if (string.IsNullOrWhiteSpace(data))
                    data = "";
                return data;
            }
            catch (Exception ex)
            {
                return "";
            }
        }
        string getDefaultProfileImage()
        {
            return "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";
        }
        private static Image resizeImage(Image imgToResize, Size size)
        {
            return (Image)(new Bitmap(imgToResize, size));
        }

        byte[] StringToImage(string base64)
        {
            try
            {
                byte[] imageBytes = Convert.FromBase64String(base64);
                return imageBytes;
            }
            catch (Exception ex)
            {

                return null;
            }
        }

        async Task Save()
        {
            try
            {
                if (!Msg.ContinueSave()) return;
                setEmployeeModel();
                using (var scope = _scopeFactory.CreateScope())
                {
                    var service = scope.GetRequiredService<EmployeeService>();
                    await service.AddOrUpdateAsync(employee);
                    await service.CommitChangesAsync();
                    OnAddOrUpdate?.Invoke(employee);
                    clear();
                }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }
        private async void simpleButton1_Click(object sender, System.EventArgs e)
        {
            await Save();
        }
        private void repositoryItemButtonEdit1_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            gridView2.DeleteSelectedRows();
        }
        private void repositoryItemButtonEdit2_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            gridView5.DeleteSelectedRows();
        }

        private void repositoryItemButtonEdit3_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            gridView4.DeleteSelectedRows();
        }

        private void repositoryItemButtonEdit4_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            gridView1.DeleteSelectedRows();
        }

        private void xtraTabPage2_Paint(object sender, PaintEventArgs e)
        {

        }

        private async void EmployeeUI_Load(object sender, EventArgs e)
        {
            //BindingSource source = new BindingSource();
            //source.DataSource = typeof(Employee); 
            await reloadInitData();
        }

        private void labelControl15_Click(object sender, EventArgs e)
        {

        }

        private void repositoryItemButtonEdit8_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            gridView8.DeleteSelectedRows();
        }

        private void repositoryItemButtonEdit7_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            gridView7.DeleteSelectedRows();
        }
        void clear()
        {
            employee = new Employee();
            SetControlValues();
        }
        private void simpleButton2_Click(object sender, EventArgs e)
        {
            clear();
        }

        private void restdaylkgrid_QueryResultValue(object sender, DevExpress.XtraEditors.Controls.QueryResultValueEventArgs e)
        {
            try
            {
                gridView9.CloseEditor();
                gridView9.UpdateCurrentRow();
                var schedOff = (List<DayOff>)gridControl1.DataSource;
                string offs = "";
                schedOff.ForEach(x =>
                {
                    if (x.Selection)
                    {
                        offs += x.Days + "|";
                    }
                });
                if (offs.EndsWith("|", StringComparison.InvariantCultureIgnoreCase))
                {
                    offs = offs.Remove(offs.Length - 1, 1);
                }
                e.Value = offs;
            }
            catch (Exception ex)
            {
            }
        }

        private async void department_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            if (e.Button.Caption == "+")
            {
                var scope = _scopeFactory.CreateScope();
                var frm = scope.GetRequiredService<DepartmentUI>();
                DisplayForms.ViewFormDialog(frm, scope);
                await loadDepartment();
            }
        }

        private async void section_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            if (e.Button.Caption == "+")
            {
                var scope = _scopeFactory.CreateScope();
                var frm = scope.GetRequiredService<SectionUI>();
                DisplayForms.ViewFormDialog(frm, scope);
                Guid deptId = department.EditValue is Guid ? (Guid)department.EditValue : Guid.Empty;
                await LoadSection(deptId);
            }
            else if (e.Button.Caption == "s")
            {
                Guid deptId = department.EditValue is Guid ? (Guid)department.EditValue : Guid.Empty;
                await LoadSection(deptId);
            }
        }

        private void workschedule_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            //if (e.Button.Caption == "+")
            //{
            //    var scope = _scopeFactory.CreateScope();
            //    var frm = scope.GetRequiredService<WorkScheduleUI>();
            //    DisplayForms.ViewFormDialog(frm, scope);
            //    loadWS(); 
            //}
        }

        private async void Position_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            if (e.Button.Caption == "+")
            {
                var scope = _scopeFactory.CreateScope();
                var frm = scope.GetRequiredService<PositionUI>();
                DisplayForms.ViewFormDialog(frm, scope);
                await loadPosition();
            }
        }
        private async void department_EditValueChanged(object sender, EventArgs e)
        {
            Guid deptId = department.EditValue is Guid ? (Guid)department.EditValue : Guid.Empty;
            await LoadSection(deptId);
        }


        private void simpleButton3_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void simpleButton4_Click(object sender, EventArgs e)
        {
            var f = new OpenFileDialog();
            if (f.ShowDialog() == DialogResult.OK)
            {
                pictureEdit1.EditValue = resizeImage(Image.FromFile(f.FileName), new Size(100, 100));
            }
        }

        private void simpleButton5_Click(object sender, EventArgs e)
        {
            try
            {
                pictureEdit1.EditValue = null;
            }
            catch (Exception)
            {
            }
        }

        private void Position_EditValueChanged(object sender, EventArgs e)
        {
            try
            {
                var pos = Position.GetSelectedDataRow() as Position;
                if (pos == null) return;
                rate.EditValue = pos.Rate;
            }
            catch (Exception)
            {
            }
        }
    }
}