

namespace Buzlink.HR.UI
{
    public partial class CreateLeaveMaster : DevExpress.XtraEditors.XtraForm
    {
        private readonly IScopeFactory _scopeFactory;

        public CreateLeaveMaster(IScopeFactory scopeFactory)
        {
            InitializeComponent();
            _scopeFactory = scopeFactory;
        }
        private void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            Create(new Leave());
        }

        private void Create(Leave? model)
        {
            try
            {
                if (model == null) return;
                var scope = _scopeFactory.CreateScope();
                var frm = scope.GetRequiredService<CreateLeave>();
                frm!.OnSaved += CreateLeaveMaster_OnSaved; ;
                frm.LoadModel(model);
                DisplayForms.ViewFormDialog(frm, scope);
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }

        private async void CreateLeaveMaster_OnSaved(Form arg1, Leave arg2)
        {
            arg1.Close();
            await LoadData();
        }

        private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            try
            {
                var model = gridView1.GetFocusedRow() as Leave;
                Create(model);
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }

        private async void barButtonItem3_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            await Cancel();
        }

        private async Task Cancel()
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var model = gridView1.GetFocusedRow() as Leave;
                    if (model == null) return;
                    if (!scope.TryGetService<LeaveService>(out var service)) return;
                    await service!.Delete(model.Id);
                    await service!.CommitChangesAsync();
                    await LoadData();
                }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }
        private void barButtonItem5_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            ExcelExporter.Export(gridControl1, Text);
        }

        private async void barButtonItem4_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            await LoadData();
        }
        private async Task LoadData()
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var service = scope.GetRequiredService<LeaveService>();
                    var data = await service!.FindAllAsync();
                    gridControl1.DataSource = data;
                }
            }
            catch (Exception x)
            {
                Msg.Error(x);
            }
        }

        private async void CreateLeaveMaster_Load(object sender, EventArgs e)
        {
            await LoadData();
        }
    }
}