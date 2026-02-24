namespace Buzlink.HR.UI
{
    public partial class HolidayMasterRecord : DevExpress.XtraEditors.XtraForm
    {
        private readonly IScopeFactory _scopeFactory;

        public HolidayMasterRecord(IScopeFactory scopeFactory)
        {
            InitializeComponent();
            curYear.EditValue = DateTime.Today.Year;
            _scopeFactory = scopeFactory;
        }

        private void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            CreateHolidayEntry(new Holiday());
        }

        private void CreateHolidayEntry(Holiday? model)
        {
            if (model == null) return;
            var scope = _scopeFactory.CreateScope();
            var frm = scope.GetRequiredService<CreateHoliday>();
            frm.OnSaved += Frm_OnSaved;
            frm.SetModel(model);
            DisplayForms.ViewFormDialog(frm, scope);
        }
        private void Frm_OnSaved(Form frm, Holiday model)
        {
            frm.Close();
            curYear.EditValue = model.HolYear;
            viewData();
        }

        private async void barButtonItem3_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            await delete();

        }

        private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            var model = gridView1.GetFocusedRow() as Holiday;
            CreateHolidayEntry(model);
        }
        void viewData()
        {
            try
            {
                var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<PositionService>();
                gridControl1.DataSource = service.FindAllAsync();
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }
        async Task delete()
        {
            try
            {
                if (!Msg.ContinueDelete()) return;
                var model = gridView1.GetFocusedRow() as Holiday;
                if (model == null) return;
                var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<HolidayService>();
                await service.Delete(model.Id);
                if (service.CommitChanges())
                {
                    gridView1.DeleteSelectedRows();
                    gridView1.RefreshData();
                }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }
        private void curYear_EditValueChanged(object sender, EventArgs e)
        {
            viewData();
        }
        private void HolidayMasterRecord_Load(object sender, EventArgs e)
        {
            viewData();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
        }

        private void barButtonItem4_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            viewData();
        }

        private void barButtonItem5_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            ExcelExporter.Export(gridControl1, Text);
        }
    }
}