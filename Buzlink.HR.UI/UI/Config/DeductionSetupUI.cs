using Buzlink.HR.UI.UI.Config;

namespace Buzlink.HR.UI
{
    public partial class DeductionSetupUI : DevExpress.XtraEditors.XtraForm
    {
        private Deduction? deduction;
        private readonly IScopeFactory _scopeFactory;
        public delegate void OnSavedHandler(Deduction deduction);
        public event OnSavedHandler OnSaved;

        public DeductionSetupUI(IScopeFactory scopeFactory)
        {
            InitializeComponent();
            deduction = new Deduction();
            _scopeFactory = scopeFactory;
        }
        public async Task SetModel(Deduction deduction)
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
            txtDescription.Text = deduction?.Name ?? "";
            category.Text = deduction?.Category?.Description ?? "";
            order.Value = deduction?.PriorityLevel ?? 0;
        }
        private async void repositoryItemButtonEdit1_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var service = scope.GetRequiredService<DeductionService>();
                    await service.Delete(deduction?.Id ?? Guid.Empty);
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
            deduction = gridView1.GetFocusedRow() as Deduction;
            if (deduction == null) return;
            UpdateData();
        }
        async Task Save()
        {
            try
            {
                if (!Msg.ContinueSave()) return;
                var classification = category.EditValue as DeductionType;
                deduction.CategoryId = classification?.Id;
                deduction.Name = txtDescription.Text;
                deduction.PriorityLevel = (int)order.Value;
                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<DeductionService>();
                await service.AddOrUpdateAsync(deduction);
                if (service.CommitChanges())
                {
                    await clear();
                    OnSaved?.Invoke(deduction);
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
            deduction = new Deduction();
            UpdateData();
            using var scope = _scopeFactory.CreateScope();
            var service = scope.GetRequiredService<DeductionService>();
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
                var service = scope.GetRequiredService<DeductionTypeService>();
                var desc = e.DisplayValue?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(desc)) return;
                var model = new DeductionType();
                model.Description = desc;
                await service.AddAsync(model);
                if (service.CommitChanges())
                {
                    var items = category.Properties.DataSource as List<DeductionType> ?? new List<DeductionType>();
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
                var service = scope.GetRequiredService<DeductionTypeService>();
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
                var frm = scope.GetRequiredService<DeductionCategory>();
                frm.OnChanged += Frm_OnChanged;
                DisplayForms.ViewFormDialog(frm, scope);
            }
        }

        private async void Frm_OnChanged(Form frm, DeductionType model)
        {
            await loadCategories();
            frm.Close();
        }

        private async void DeductionSetupUI_Load(object sender, EventArgs e)
        {
            await loadCategories();
        }
    }
}