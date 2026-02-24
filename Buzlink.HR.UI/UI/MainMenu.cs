using Buzlink.HR.UI.Providers;
using BuzlinkRepository;

namespace Buzlink.HR.UI
{
    public partial class MainMenu : DevExpress.XtraEditors.XtraForm
    {
        private AuthUI.Login login;
        private readonly IScopeFactory _scopefactory;
        private readonly ITenantProvider _tenantProvider;

        public MainMenu(IScopeFactory scopefactory,
            ITenantProvider tenantProvider)
        {
            InitializeComponent();
            _scopefactory = scopefactory;
            _tenantProvider = tenantProvider;
            login = new AuthUI.Login();
        }
        private void MainMenu_FormClosing(object sender, FormClosingEventArgs e)
        {
            string str = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
            Properties.Settings.Default.theme = str;
            Properties.Settings.Default.Save();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //globalInfo.user = new User() { Id = "xxxxxxxxxxx",  Branch = new Common.Domain.BranchEntity { Id  ="1", CompanyId="1" } , FullName = "jhoncee" };
            login = new AuthUI.Login();
            login.OnLogin += Login_OnLogin; ; ;
            DisplayForms.ViewFormDialog(login);
            //DisplayForms.ViewForm(new TileMenu());
        }
        private void Login_OnLogin(bool success, Auth.Domain.UserEntity user)
        {
            try
            {
                _tenantProvider.SetTenantId(user.TenantId);
                new UserContextComposer().SetUserContext(user);
                Task.Factory.StartNew(() =>
                {
                    new TaskRunnder(MainProgress.handler).Looper();
                });
                if (!success || user == null)
                {
                    Application.Exit(new CancelEventArgs(false));
                }
                else
                {
                    userLabel.Caption = user?.FullName ?? "";
                    login.Hide();
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            var scope = _scopefactory.CreateScope();
            var emp = scope.GetRequiredService<EmployeeMasterList>();
            DisplayForms.ViewForm(emp, scope);
        }

        private void barButtonItem3_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //DisplayForms.ViewFormFloat(new PositionUI());
        }

        private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //DisplayForms.ViewFormFloat(new DepartmentUI());
        }

        private void barButtonItem11_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //DisplayForms.ViewFormFloat(new SectionUI());
        }

        private void barButtonItem4_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //DisplayForms.ViewFormFloat(new DeductionSetupUI());
        }

        private void barButtonItem5_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //DisplayForms.ViewFormFloat(new AllowanceSetupUI());
        }
        private void MainMenu_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F2)
            {
                //DisplayForms.ViewFormDialog(login);
            }
        }

        private void barButtonItem24_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //DisplayForms.ViewFormFloat(new SSSTableSetupUI());
        }

        private void barButtonItem25_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //DisplayForms.ViewFormFloat(new PHICTableSetupUI());
        }

        private void barButtonItem7_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            var scope = _scopefactory.CreateScope();
            var frm = scope.GetRequiredService<CreateDeductionEntry>();
            DisplayForms.ViewForm(frm, scope);
        }
        private void barButtonItem8_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //  DisplayForms.ViewFormFloat(new AllowanceSetupUI());
        }

        private void barButtonItem47_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //DisplayForms.ViewFormFloat(new Migrate_EmployeeUI());
        }

        private void barDockingMenuItem1_ListItemClick(object sender, DevExpress.XtraBars.ListItemClickEventArgs e)
        {
        }

        private void barButtonItem23_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //DisplayForms.ViewFormFloat(new CreateSalaryAdjustment());
        }

        private void barButtonItem30_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //DisplayForms.ViewFormFloat(new CreateOT());
        }

        private void barButtonItem31_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            // DisplayForms.ViewForm(new OTMasterRecord());
        }

        private void barButtonItem19_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //   DisplayForms.ViewFormFloat(new CreateLeaveApplication());

        }

        private void barButtonItem20_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            // DisplayForms.ViewForm(new  LeaveMasterRecord());
        }

        private void barButtonItem40_ItemClick_1(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            // DisplayForms.ViewFormFloat(new WorkScheduleUI());
        }



        private void barButtonItem45_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //  DisplayForms.ViewFormFloat(new AllowanceSetupUI());
        }

        private void barButtonItem21_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {

        }

        private void barButtonItem22_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {

        }

        private void barButtonItem65_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            // DisplayForms.ViewFormFloat(new TravelOrdersEntryUI());
        }

        private void barButtonItem66_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            // DisplayForms.ViewFormFloat(new TravelOrdersList());
        }

        private void barButtonItem32_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
        }
        private void barButtonItem43_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {

        }

        private void barButtonItem18_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
        }

        private void barButtonItem15_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            // DisplayForms.ViewForm(new CreateLeaveMaster());
        }

        private void barButtonItem27_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //  DisplayForms.ViewFormDialog(new CreateLeave());
        }

        private void barButtonItem26_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //DisplayForms.ViewFormDialog(new CreateHDMF());
        }

        private void barButtonItem49_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //DisplayForms.ViewFormDialog(new CompanySetup(Utilities.globalInfo.Company));
        }
        private void barButtonItem51_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            // DisplayForms.ViewFormDialog(new UserMaster());
        }

        private void barButtonItem6_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //  DisplayForms.ViewForm(new PermanentSchedule());
        }

        private void barButtonItem82_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            // DisplayForms.ViewForm(new TemporaryScheduleMasterRecord());
        }

        private void barButtonItem54_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            // DisplayForms.ViewForm(new UploadDATFile());
        }

        private void barButtonItem52_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            SendKeys.Send("{F2}");
        }

        private void barButtonItem44_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //   DisplayForms.ViewFormFloat(new CreateDeductionEntry());
        }

        private void barButtonItem81_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            var scope = _scopefactory.CreateScope();
            var emp = scope.GetRequiredService<DeductionMasterRecord>();
            DisplayForms.ViewForm(emp, scope);

        }

        private void barButtonItem46_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            // DisplayForms.ViewFormFloat(new AllowanceEntryUI());
        }

        private void barButtonItem90_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //    DisplayForms.ViewForm(new HolidayMasterRecord());
        }

        private void barButtonItem60_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //Task.Factory.StartNew(() =>
            //{
            //    new TaskRunnder(MainProgress.handler).Looper();
            //});
            //DisplayForms.ViewForm(new SalaryAdjustmentMasterRecord());
        }

        public void UpdateProgress(int i)
        {
            if (InvokeRequired)
            {
                Action<int> setva = (ii) =>
                 {
                     progressbarDisplay.EditValue = ii;
                     progressbarDisplay.Refresh();
                     progressbarDisplay.Caption = ii.ToString();
                 };
                Invoke(setva, new object[] { i });
            }
            else
            {
                progressbarDisplay.Visibility = i == 0 || i >= 100 ? DevExpress.XtraBars.BarItemVisibility.Never : DevExpress.XtraBars.BarItemVisibility.Always;
                progressbarDisplay.EditValue = i;
                progressbarDisplay.Refresh();
                progressbarDisplay.Caption = i.ToString();
            }
        }
        private void barButtonItem53_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            Close();
        }

        private void barButtonItem47_ItemClick_1(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //DisplayForms.ViewFormFloat(new BiomerticEnrollment());
        }
    }
}
