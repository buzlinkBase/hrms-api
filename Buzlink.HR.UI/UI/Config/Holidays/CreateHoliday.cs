namespace Buzlink.HR.UI
{
    public partial class CreateHoliday : DevExpress.XtraEditors.XtraForm
    {
        public delegate void OnSavedHandler(Form frm, Holiday model);
        public event OnSavedHandler OnSaved;
        private Holiday model;
        private readonly IScopeFactory _scopeFactory;

        public CreateHoliday(IScopeFactory scopeFactory)
        {
            InitializeComponent();
            _scopeFactory = scopeFactory;
        }

        public void SetModel(Holiday holiday)
        {
            clear();
            model = holiday;
            holname.Text = model.Description;
            holdate.DateOnly = model.HolDate;
            islegal.Checked = model.HolType == Hrms.Domain.HolidayType.LEGAL;
            isspecial.Checked = model.HolType != Hrms.Domain.HolidayType.LEGAL;
            recuring.Checked = model.IsRecuring;
        }

        async Task save()
        {
            try
            {
                var scope = _scopeFactory.CreateScope();
                var service = scope.GetRequiredService<HolidayService>();
                model.Description = holname.Text.Trim();
                model.HolDate = holdate.DateOnly;
                model.HolType = isspecial.Checked ? Hrms.Domain.HolidayType.SPECIAL : Hrms.Domain.HolidayType.LEGAL;
                model.IsRecuring = recuring.Checked;
                await service.AddOrUpdateAsync(model);
                if (service.CommitChanges())
                {
                    OnSaved?.Invoke(this, model);
                    clear();
                }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }
        void clear()
        {
            model = new Holiday();
            holdate.DateTime = DateTime.Today;
            holname.Text = "";
            isspecial.Checked = true;
            recuring.Checked = false;
        }
        private async void saveCommand_Click(object sender, EventArgs e)
        {
            await save();
        }

        private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            clear();
        }

        private async void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            await save();
        }
    }
}