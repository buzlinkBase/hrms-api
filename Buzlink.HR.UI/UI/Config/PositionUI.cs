namespace Buzlink.HR.UI
{
    public partial class PositionUI : DevExpress.XtraEditors.XtraForm
    {
        private Position position;
        private readonly IScopeFactory _scopeFactory;

        public PositionUI(IScopeFactory scopeFactory)
        {
            InitializeComponent();
            position = new Position();
            _scopeFactory = scopeFactory;
        }
        public async Task SetModel(Position position)
        {
            await clear();
            this.position = position;
            loadPositionInfo();
        }
        private void repositoryItemButtonEdit2_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            gridView1.DeleteSelectedRows();
        }
        public void loadPositionInfo()
        {
            code.Text = position.Code;
            positionname.Text = position.Name;
            rate.EditValue = position.Rate;
        }
        private async Task repositoryItemButtonEdit1_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<PositionService>();
                await service.Delete(position.Id);
                position.Status = "Cancelled";
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

        private void gridView1_DoubleClick(object sender, EventArgs e)
        {
            position = gridView1.GetFocusedRow() as Position;
            if (position == null) return;
            loadPositionInfo();
        }
        async Task Save()
        {
            try
            {
                if (!Msg.ContinueSave()) return;
                if (position == null) position = new Position();
                position.Code = code.Text;
                position.Name = positionname.Text;
                position.Rate = (double)rate.Value;

                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<PositionService>();
                await service.AddOrUpdate(position);
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

        private async void simpleButton1_Click(object sender, EventArgs e)
        {
            await Save();
        }

        private void PositionUI_Load(object sender, EventArgs e)
        {
        }
        async Task clear()
        {
            position = new Position();
            loadPositionInfo();
            await ListReload();
        }
        async Task ListReload()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<PositionService>();
                gridControl1.DataSource = await service.FindAllAsync();
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }
        private async void simpleButton1_Click_1(object sender, EventArgs e)
        {
            await clear();
        }

        private async void saveCommand_Click(object sender, EventArgs e)
        {
            await Save();
        }
    }
}