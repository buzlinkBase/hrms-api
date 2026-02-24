
namespace Buzlink.HR.UI
{
    public partial class CreateLeave : DevExpress.XtraEditors.XtraForm
    {
        public event Action<Form, Leave> OnSaved;
        private Leave model;
        private readonly IScopeFactory _scopeFactory;
        public CreateLeave(IScopeFactory scopeFactory)
        {
            InitializeComponent();
            _scopeFactory = scopeFactory;
            LoadModel(new Leave());
        }

        public void LoadModel(Leave model)
        {
            this.model = model;
            SetFieldValues();
        }

        void SetFieldValues()
        {
            description.Text = model.Description;
            remarks.Text = model.Remarks;
            credits.EditValue = model.Credits;
            statustoggle.IsOn = model.Status == "Active";
            IsWithPay.Checked = model.PaySource != Hrms.Domain.PaySource.Unpaid;
        }

        async Task Save()
        {
            try
            {
                if (!Msg.ContinueSave()) return;
                model.Description = description.Text;
                model.Remarks = remarks.Text;
                model.Credits = (int)credits.Value;
                //model.IsPaid = IsWithPay.Checked;
                model.Status = statustoggle.IsOn ? "Active" : "Deactivated";
                using var scope = _scopeFactory.CreateScope();
                var ls = scope.GetRequiredService<LeaveService>();
                await ls.AddOrUpdateAsync(model);
                if (ls.CommitChanges())
                {
                    OnSaved?.Invoke(this, model);
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

        void clear()
        {
            model = new Leave();
            SetFieldValues();
        }

        private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            clear();
        }

        private async void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            await Save();
        }
    }
}