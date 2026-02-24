namespace Buzlink.HR.UI
{
    public partial class SectionUI : DevExpress.XtraEditors.XtraForm
    {
        private Section section;
        private readonly IScopeFactory _scopeFactory;

        public SectionUI(IScopeFactory scopeFactory)
        {
            InitializeComponent();
            section = new Section();
            _scopeFactory = scopeFactory;
        }
        public async void SetModel(Section section)
        {
            await clear();
            this.section = section;
            UpdateData();
        }

        private void repositoryItemButtonEdit2_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            gridView1.DeleteSelectedRows();
        }
        public void UpdateData()
        {
            try
            {
                if (section == null) return;
                code.Text = section.Code;
                sectioname.Text = section.Name;
                departmentName.EditValue = section.DepartmentId;
            }
            catch (Exception ex)
            {
            }
        }
        private async void repositoryItemButtonEdit1_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            try
            {
                var model = gridView1.GetFocusedRow() as Section;
                if (model == null) return;
                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<PositionService>();
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

        private void gridView1_DoubleClick(object sender, EventArgs e)
        {
            section = gridView1.GetFocusedRow() as Section;
            UpdateData();
        }
        async Task Save()
        {
            try
            {
                if (!Msg.ContinueSave()) return;
                section.Code = code.Text;
                section.Name = sectioname.Text;
                section.DepartmentId = (Guid)departmentName.EditValue;
                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<SectionService>();
                await service.AddOrUpdateAsync(section);
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

        private async void SectionUI_Load(object sender, EventArgs e)
        {
            await clear();
        }

        private async void saveCommand_Click(object sender, EventArgs e)
        {
            await Save();
        }
        async Task clear()
        {


            try
            {
                section = new Section();
                UpdateData();
                using var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<PositionService>();
                var depService = scope.GetRequiredService<PositionService>();

                departmentName.Properties.DataSource = await depService.FindAllAsync();
                gridControl1.DataSource = await service.FindAllAsync();
                gridView1.DeleteSelectedRows();
                gridView1.RefreshData();
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
    }
}