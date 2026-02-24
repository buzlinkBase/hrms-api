namespace Buzlink.HR.UI
{
    public partial class EmployeeMasterList : DevExpress.XtraEditors.XtraForm
    {
        public delegate void OnAddOrUpdateHandler(Employee employee);
        public event OnAddOrUpdateHandler OnAddOrUpdate;
        private int page = 0;
        private int limit = 50;
        private int roll = 0;
        private readonly IScopeFactory _scopeFactory;
        public EmployeeMasterList(IScopeFactory scopeFactory)
        {
            InitializeComponent();
            _scopeFactory = scopeFactory;
        }
        private void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            Create(new Employee());
        }

        private void Create(Employee? model)
        {
            try
            {
                if (model == null) return;
                var scope = _scopeFactory.CreateScope();
                var frm = scope.GetRequiredService<EmployeeUI>();
                frm!.OnAddOrUpdate += Frm_OnAddOrUpdate;
                DisplayForms.ViewFormDialog(frm, scope);
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }

        private void Frm_OnAddOrUpdate(Employee employee)
        {
            OnAddOrUpdate?.Invoke(employee);
        }

        private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            try
            {
                var emp = bandedGridView1.GetFocusedRow() as Employee;
                Create(emp);
            }
            catch (Exception ex)
            {
            }
        }

        private async void barButtonItem4_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            page = 0;
            roll = 1;
            await searchTopWhere();
            barHeaderItem1.Caption = roll * limit + 1 + $"-{page * limit}";
        }
        private async void EmployeeMasterList_Load(object sender, EventArgs e)
        {
            barHeaderItem1.Caption = 1 + "-" + limit.ToString();
            repositoryItemSearchControl1.ButtonClick += RepositoryItemSearchControl1_ButtonClick;
            repositoryItemSearchControl1.KeyDown += RepositoryItemSearchControl1_KeyDown;
            await searchTopWhere();
        }

        private async void RepositoryItemSearchControl1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            await searchTopWhere();
        }

        private async void RepositoryItemSearchControl1_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            await searchTopWhere();
        }
        async Task searchTopWhere()
        {
            string fn = string.Concat("%", barEditItem1.EditValue?.ToString() ?? "", "%");
            using (var scope = _scopeFactory.CreateScope())
            {
                var service = scope.GetRequiredService<EmployeeService>();
                var pagination = new PaginationPayload(fn, page, limit);
                gridControl1.DataSource = await service.LoadAll(pagination);
            }
        }

        private async void barButtonItem5_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            await Delete();
        }
        private async Task Delete()
        {
            try
            {
                if (!Msg.ContinueDelete()) return;
                var employee = bandedGridView1.GetFocusedRow() as Employee;
                if (employee == null) return;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var service = scope.GetRequiredService<EmployeeService>();
                    employee.Status = "Deleted";
                    await service.Delete(employee?.Id ?? Guid.Empty);
                    if (service.CommitChanges())
                    {
                        bandedGridView1.DeleteSelectedRows();
                    }
                }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }

        private async void barButtonItem6_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            roll -= 1;
            page = page - limit;
            if (page < 0) page = 0;
            if (roll <= 1) roll = 0;
            barHeaderItem1.Caption = roll * limit + 1 + $"-{page * limit}";
            await searchTopWhere();
        }

        private async void barButtonItem7_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            page += limit;
            roll += 1;
            await searchTopWhere();
            if (bandedGridView1.RowCount < limit)
            {
                page -= limit;
            }
            barHeaderItem1.Caption = roll * limit + 1 + $"-{page * limit}";
        }
    }
}