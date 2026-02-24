namespace Buzlink.HR.UI
{
    public partial class DepartmentUI : DevExpress.XtraEditors.XtraForm
    {
        private readonly IScopeFactory _factory;
        private Department? department;
        public DepartmentUI(IScopeFactory factory)
        {
            InitializeComponent();
            _factory = factory;
        }
        private void repositoryItemButtonEdit2_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            gridView1.DeleteSelectedRows();
        }

        public void UpdateData()
        {
            try
            {
                if (department is null) return;
                departmentCode.Text = department.Code;
                DepartmentName.Text = department.Name;
                department.HeadId = department?.HeadId;
            }
            catch (Exception ex)
            {
            }
        }

        private async void repositoryItemButtonEdit1_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            try
            {
                var model = gridView1.GetFocusedRow() as Department;
                if (model is null) return;
                if (model.Status == "Cancelled") return;
                if (!Msg.ContinueCancel()) return;
                model.Status = "Cancelled";
                using var scope = _factory.CreateScope();
                var service = scope.GetRequiredService<DepartmentService>();
                await service.Delete(model.Id);
                if (service.CommitChanges())
                {
                    gridView1.DeleteSelectedRows();
                }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }

        private void gridView1_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                department = gridView1.GetFocusedRow() as Department;
                UpdateData();
            }
            catch (Exception)
            {
            }
        }
        async Task Save()
        {
            try
            {
                if (!Msg.ContinueSave()) return;
                if (department is null) department = new Department();
                department.Code = departmentCode.Text.Trim();
                department.Name = DepartmentName.Text.Trim();
                var head = departmentHead.EditValue as Employee;
                department.HeadId = head?.Id ?? null;
                department.BranchId = head?.BranchId ?? null;
                //department. = departmentHead.Text;
                //department.BranchId = globalInfo.Branch.Id;

                using var scope = _factory.CreateScope();
                var service = scope.GetRequiredService<DepartmentService>();
                await service.AddOrUpdateAsync(department);
                if (service.CommitChanges())
                {
                    await clear();
                }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }
        async Task LoadEmployees()
        {
            try
            {
                using var scope = _factory.CreateScope();
                var service = scope.GetRequiredService<DepartmentService>();
                var empService = scope.GetRequiredService<EmployeeService>();
                gridControl1.DataSource = await service.FindAllAsync();

                var employees = await empService.FindAll();
                repositoryItemLookUpEdit1.DataSource = employees;
                departmentHead.Properties.DataSource = employees;
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }
        async Task clear()
        {
            department = new Department();
            UpdateData();
            await LoadEmployees();
        }

        private void gridControl1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                popupMenu1.ShowPopup(Control.MousePosition);
            }
        }

        private void gridView1_RowStyle(object sender, DevExpress.XtraGrid.Views.Grid.RowStyleEventArgs e)
        {
            try
            {
                var View = gridView1;
                if (e.RowHandle >= 0)
                {
                    string category = View.GetRowCellDisplayText(e.RowHandle, View.Columns["Status"]);
                    if (category == "Cancelled")
                    {
                        e.Appearance.ForeColor = Color.Red;
                        e.HighPriority = true;
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        private async void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            await Save();
        }

        private async void barButtonItem4_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            await clear();
        }

        private void departmentHead_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            if (e.Button.Caption == "+")
            {
                var scope = _factory.CreateScope();
                var empfrm = scope.GetRequiredService<EmployeeUI>();
                empfrm.OnAddOrUpdate += Empfrm_OnAddOrUpdate;
                DisplayForms.ViewFormDialog(empfrm, scope);
            }
        }

        private async void Empfrm_OnAddOrUpdate(Employee employee)
        {
            await LoadEmployees();
        }
        private void departmentHead_EditValueChanged(object sender, EventArgs e)
        {
        }

        private async void DepartmentUI_Load(object sender, EventArgs e)
        {
            await clear();
        }
    }
}