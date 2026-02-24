namespace Buzlink.HR.UI.UI.Config
{
    public partial class DeductionCategory : DevExpress.XtraEditors.XtraForm
    {
        private readonly IScopeFactory _scopeFactory;
        private DeductionType? model;
        public delegate void OnSavedHandler(Form frm, DeductionType model);
        public event OnSavedHandler OnChanged;
        public DeductionCategory(IScopeFactory scopeFactory)
        {
            InitializeComponent();
            model = new DeductionType();
            _scopeFactory = scopeFactory;
            repositoryItemButtonEdit1.ButtonClick += RepositoryItemButtonEdit1_ButtonClick;
        }

        private async void RepositoryItemButtonEdit1_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            await Delete();
        }
        private async Task Delete()
        {

            try
            {
                if (!Msg.ContinueDelete()) return;
                var row = gridView1.GetFocusedRow() as DeductionType;
                if (row == null) return;
                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<DeductionTypeService>();
                await service.Delete(row.Id);
                if (service.CommitChanges())
                {
                    OnChanged?.Invoke(this, row);
                    gridView1.DeleteSelectedRows();
                }
            }
            catch (Exception ex)
            {

                Msg.Error(ex);
            }
        }
        private async void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            await Save();
        }

        private async Task Reset()
        {
            try
            {
                model = new DeductionType();
                textEdit1.Text = "";
                barButtonItem1.Caption = "Save";
                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<DeductionTypeService>();
                gridControl1.DataSource = await service.FindAllAsync();
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }

        private async Task Save()
        {
            try
            {
                if (!Msg.ContinueSave()) return;
                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<DeductionTypeService>();
                model.Description = textEdit1.Text.Trim();
                await service.AddOrUpdateAsync(model);
                if (service.CommitChanges())
                {
                    OnChanged?.Invoke(this, model);
                    await Reset();

                }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }

        private async void barButtonItem5_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            await Delete();
        }

        private async void barButtonItem4_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            await Reset();
        }

        private void gridControl1_DoubleClick(object sender, EventArgs e)
        {
            model = gridView1.GetFocusedRow() as DeductionType;
            if (model == null) return;
            textEdit1.Text = model.Description;
            barButtonItem1.Caption = "Update";
        }
    }
}