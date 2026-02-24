namespace Buzlink.HR.UI.UI.Config.Goverment
{
    public partial class CreateHDMFForm : DevExpress.XtraEditors.XtraForm
    {
        private readonly IScopeFactory _scopeFactory;

        public CreateHDMFForm(IScopeFactory scopeFactory)
        {
            InitializeComponent();
            _scopeFactory = scopeFactory;
        }
        private async void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            await save();
        }
        async Task save()
        {
            try
            {
                advBandedGridView1.EndEdit();
                var models = gridControl1.DataSource as BindingList<HDMFTable>;
                if (models == null || models.Count() == 0) return;
                if (!Msg.ContinueSave()) return;
                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<HDMFService>();
                await service.AddOrUpdateRange(models.ToList());
                if (service.CommitChanges())
                {
                    Close();
                }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }

        private async Task repox_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            try
            {
                advBandedGridView1.EndEdit();
                var model = advBandedGridView1.GetFocusedRow() as HDMFTable;
                if (model == null) return;
                if (!Msg.ContinueDelete()) return;

                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<DeductionService>();
                await service.Delete(model.Id);
                if (service.CommitChanges())
                {
                    await LoadData();
                }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }

        public async Task LoadData()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<HDMFService>();
                //TODO change date here
                var date = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1);
                gridControl1.DataSource = new BindingList<HDMFTable>(await service.FindAllAsync(date));
            }
            catch (Exception ex)
            {
            }
        }

        private void gridControl1_Click(object sender, EventArgs e)
        {
        }

        private async void CreateHDMF_Load(object sender, EventArgs e)
        {
            await LoadData();
        }

        private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            ExcelExporter.Export(gridControl1, "hdmf");
            ;
        }
    }
}