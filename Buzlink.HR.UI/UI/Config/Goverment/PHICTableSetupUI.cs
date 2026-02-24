namespace Buzlink.HR.UI
{
    public partial class PHICTableSetupUI : DevExpress.XtraEditors.XtraForm
    {
        private readonly IScopeFactory _scopeFactory;

        public PHICTableSetupUI(IScopeFactory scopeFactory)
        {
            InitializeComponent();
            _scopeFactory = scopeFactory;
        }
        async Task ListReload()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var service = scope.GetRequiredService<PHICService>();
                //TODO change date here
                var date = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1);
                var data = await service.FindAllAsync(date);
                gridControl1.DataSource = new BindingList<PHICTable>(data);
                bandedGridView1.BestFitColumns();
                bandedGridView1.OptionsView.ColumnAutoWidth = true;
            }
        }
        private async void spinEdit1_EditValueChanged(object sender, EventArgs e)
        {
            await ListReload();
        }

        private async void PHICTableSetupUI_Load(object sender, EventArgs e)
        {
            await ListReload();
        }
        void clear()
        {

        }
        async Task save()
        {
            try
            {
                //var handle = Progress.ShowProgressPanel(this);
                if (!Msg.ContinueSave()) return;
                var models = gridControl1.DataSource as List<PHICTable>;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var service = scope.GetRequiredService<PHICService>();
                    //await service.DeleteAllAsync();
                    foreach (var item in models)
                    {
                        await service.AddOrUpdateAsync(item);
                    }

                    if (await service.CommitChangesAsync())
                    {
                        clear();
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            save();
        }

        private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            gridControl1.Export(Text);
        }
    }
}