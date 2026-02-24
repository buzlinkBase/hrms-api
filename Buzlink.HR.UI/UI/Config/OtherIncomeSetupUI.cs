using Buzlink.HR.UI.UI.Config;

namespace Buzlink.HR.UI
{
    public partial class OtherIncomeSetupUI : DevExpress.XtraEditors.XtraForm
    {
        private OtherIncome? OtherIncome;
        private readonly IScopeFactory _scopeFactory;
        public delegate void OnSavedHandler(OtherIncome OtherIncome);
        public event OnSavedHandler OnSaved;

        public OtherIncomeSetupUI(IScopeFactory scopeFactory)
        {
            InitializeComponent();
            OtherIncome = new OtherIncome();
            _scopeFactory = scopeFactory;
        }
        public async Task SetModel(OtherIncome OtherIncome)
        {
            InitializeComponent();
            await clear();
        }
        private void repositoryItemButtonEdit2_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            gridView1.DeleteSelectedRows();
        }
        public void UpdateData()
        {
            txtDescription.Text = OtherIncome?.Name ?? "";
            category.Text = OtherIncome?.IncomeType?.Description ?? "";
        }
        private async void repositoryItemButtonEdit1_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var service = scope.GetRequiredService<OtherIncomeService>();
                    await service.Delete(OtherIncome?.Id ?? Guid.Empty);
                    if (service.CommitChanges())
                    {
                        gridView1.DeleteSelectedRows();
                        //gridControl1.RefreshDataSource();
                    }
                }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }

        private void gridView1_DoubleClick(object sender, EventArgs e)

        {
            OtherIncome = gridView1.GetFocusedRow() as OtherIncome;
            if (OtherIncome == null) return;
            UpdateData();
        }
        async Task Save()
        {
            try
            {
                if (!Msg.ContinueSave()) return;
                if (OtherIncome == null) return;
                var classification = category.EditValue as OtherIncomeType;
                OtherIncome.IncomeTypeId = classification?.Id;
                OtherIncome.Name = txtDescription.Text;
                OtherIncome.IsTaxable = IsWithPay.Checked;
                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<OtherIncomeService>();
                await service.AddOrUpdateAsync(OtherIncome);
                if (service.CommitChanges())
                {
                    await clear();
                    OnSaved?.Invoke(OtherIncome);
                }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }

        private async void simpleButton1_Click(object sender, EventArgs e)
        {
            await Save();
        }
        async Task clear()
        {
            OtherIncome = new OtherIncome();
            UpdateData();
            using var scope = _scopeFactory.CreateScope();
            var service = scope.GetRequiredService<OtherIncomeService>();
            gridControl1.DataSource = await service.FindAllAsync();
        }

        private async void simpleButton1_Click_1(object sender, EventArgs e)
        {
            await clear();
        }

        private async void saveCommand_Click(object sender, EventArgs e)
        {
            await Save();
        }

        private async void category_ProcessNewValue(object sender, DevExpress.XtraEditors.Controls.ProcessNewValueEventArgs e)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<OtherIncomeTypeService>();
                var desc = e.DisplayValue?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(desc)) return;
                var model = new OtherIncomeType();
                model.Description = desc;
                await service.AddAsync(model);
                if (service.CommitChanges())
                {
                    var items = category.Properties.DataSource as List<OtherIncomeType> ?? new List<OtherIncomeType>();
                    items.Add(model);
                }
            }
            catch (Exception ex)
            {
            }
        }
        async Task loadCategories()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<OtherIncomeTypeService>();
                category.Properties.DataSource = await service.FindAllAsync();
            }
            catch (Exception ex)
            {
            }
        }
        private void category_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            if (e.Button.Caption == "+")
            {
                var scope = _scopeFactory.CreateScope();
                var frm = scope.GetRequiredService<OtherIncomeCategory>();
                frm.OnChanged += Frm_OnChanged;
                DisplayForms.ViewFormDialog(frm, scope);
            }
        }

        private async void Frm_OnChanged(Form frm, OtherIncomeType model)
        {
            await loadCategories();
            frm.Close();
        }

        private async void OtherIncomeSetupUI_Load(object sender, EventArgs e)
        {
            await loadCategories();
        }
    }
}