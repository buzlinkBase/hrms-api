namespace Buzlink.HR.UI
{
    public partial class  SSSTableSetupUI : DevExpress.XtraEditors.XtraForm
    {
        private bool loaded = false;
        public SSSTableSetupUI()
        {
            InitializeComponent();
            clear();
            loaded = true;
        }
        private void repositoryItemButtonEdit2_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
        }
        private void repositoryItemButtonEdit1_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            //if (e.Button.Caption=="x")
            //{
            //    bandedGridView1.DeleteSelectedRows();
            //}
            //else //+
            //{
            //    try
            //    {
            //        var lst = (BindingList<GovSSS>)gridControl1.DataSource;
            //        var index = bandedGridView1.FocusedRowHandle;
            //        lst.Insert( index ,new GovSSS());
            //    }
            //    catch (Exception ex)
            //    {
            //    }
            //}
        }

        private void gridView1_DoubleClick(object sender, EventArgs e)
        {
        }
        void Save()
        {
            //try
            //{
            //    //var handle = Progress.ShowProgressPanel(this);
            //    bandedGridView1.CloseEditor();
            //    bandedGridView1.UpdateCurrentRow();
            //    if (!Msg.ContinueSave()) return;
            //    var models = gridControl1.DataSource as BindingList<GovSSS>;
            //    using (var c=new UCommand())
            //    {
            //        new SSSController(c).AddOrUpdate(models.ToList());
            //        c.Commit();
            //    }
            //}
            //catch (Exception ex)
            //{
            //}
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            Save();
        }
        void clear()
        {
            ListReload();
        }
        void ListReload()
        {
            //using (var c=new UCommand())
            //{
            //    var res = c.repository.Where<GovSSS>("", "", "");
            //    List<GovSSS> lst = new List<GovSSS>();
            //    foreach (var x in res)
            //    {
            //        lst.Add(x);
            //        erRate.Value = (decimal)x.ERRate;
            //        eeRate.Value = (decimal)x.EERate;
            //    }
            //    gridControl1.DataSource = new BindingList<GovSSS>(lst);
            //    bandedGridView1.BestFitColumns();
            //    bandedGridView1.OptionsView.ColumnAutoWidth = true;
            //}
        }

        private void simpleButton1_Click_1(object sender, EventArgs e)
        {
            clear();
        }

        private void saveCommand_Click(object sender, EventArgs e)
        {
            Save();
        }
         
        private void SSSTableSetupUI_Load(object sender, EventArgs e)
        {
        }

        private void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            Save();
        }

        void RecomputeTable()
        {
            //var lst = gridControl1.DataSource as BindingList<GovSSS>;
            //foreach (GovSSS item in lst)
            //{
            //    item.EERate =(double)eeRate.Value/100;
            //    item.ERRate =(double)erRate.Value/100;
            //}
            //gridControl1.RefreshDataSource();
        }
        private void erRate_EditValueChanged(object sender, EventArgs e)
        {
            RecomputeTable();
        }

        private void eeRate_EditValueChanged(object sender, EventArgs e)
        {
            RecomputeTable();
        }

        private void bandedGridView1_InitNewRow(object sender, DevExpress.XtraGrid.Views.Grid.InitNewRowEventArgs e)
        {
            //try
            //{
            //    var item = bandedGridView1.GetRow(e.RowHandle) as GovSSS;
            //    item.EERate = (double)eeRate.Value / 100;
            //    item.ERRate = (double)erRate.Value / 100;

            //}
            //catch (Exception ex)
            //{
            //}
        }

        private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            gridControl1.Export(this.Text);
        }
    }
}