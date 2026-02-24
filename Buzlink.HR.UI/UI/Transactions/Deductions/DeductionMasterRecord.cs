namespace Buzlink.HR.UI
{
    public partial class DeductionMasterRecord : DevExpress.XtraEditors.XtraForm
    {
        public DeductionMasterRecord()
        {
            InitializeComponent();
        }

        private void barButtonItem4_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            ViewRecord();
        }

        void ViewRecord()
        {
            try
            {
                //           using (var command=new UCommand())
                //           {
                //var data = new DeductionRecordController(command).FindTransaction(dateRangeFilter1.FromDate, dateRangeFilter1.ToDate);
                //               GridControl2.DataSource = data;
                //               BandedGridView1.OptionsView.ColumnAutoWidth = true;
                //               BandedGridView1.BestFitColumns();
                //           }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }
        void findDetail()
        {
            try
            {
                //           var model = BandedGridView1.GetFocusedRow() as DeductionRecordMainEntity;
                //           if (model == null) return;
                //           //if (model.Details.Count > 0) return; 
                //           using (var command=new UCommand())
                //           {
                //var data = new DeductionRecordController(command).FindDetails(model.Id);
                //               model.Details =data;
                //               gridView1.OptionsView.ColumnAutoWidth = true;
                //               gridView1.BestFitColumns();
                //           }
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }

        private void dateRangeFilter1_OnFilterChange(Extensions.DateRange range)
        {
            ViewRecord();
        }

        private void barButtonItem1_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            var frm = new CreateDeductionEntry();
            frm.OnSaved += Frm_OnSaved; ;
            DisplayForms.ViewFormDialog(frm);
        }

        private void Frm_OnSaved()
        {
            ViewRecord();
            var handle = BandedGridView1.FocusedRowHandle;
            BandedGridView1.CollapseMasterRow(handle);
        }

        private void barButtonItem2_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //         var model = BandedGridView1.GetFocusedRow() as DeductionRecordMainEntity;
            //if (model==null)
            //{
            //             Msg.Inform("Select transaction first");
            //             return;
            //}
            //         var frm = new CreateDeductionEntry(model);
            //         frm.OnSaved += Frm_OnSaved; ;
            //         DisplayForms.ViewFormDialog(frm);
        }

        private void barButtonItem3_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            Cancel();
        }
        void Cancel()
        {
            try
            {
                //var model = BandedGridView1.GetFocusedRow() as DeductionRecordMainEntity;
                //if (model == null)
                //{
                //    Msg.Inform("Select transaction first");
                //    return;
                //}
                //if (!Msg.ContinueCancel()) return;
                //using (var command = new UCommand())
                //{
                //    new DeductionRecordController(command).Cancel(model);
                //    if (command.Commit()) ViewRecord();
                //}
            }
            catch (Exception ex)
            {
                Msg.Error(ex);
            }
        }




        private void gridView1_RowCellStyle(object sender, DevExpress.XtraGrid.Views.Grid.RowCellStyleEventArgs e)
        {
            try
            {
                if (e.Column.FieldName == "Status")
                {
                    var status = e.CellValue.ToString();
                    if (status == "Cancelled")
                    {
                        e.Appearance.BackColor = Color.Red;
                        e.Appearance.ForeColor = Color.White;
                    }
                    else if (status == "Approved")
                    {
                        e.Appearance.BackColor = Color.Green;
                        e.Appearance.ForeColor = Color.White;
                    }
                    else
                    {
                        e.Appearance.BackColor = Color.Salmon;
                        e.Appearance.ForeColor = Color.White;
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void barButtonItem5_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            ExcelExporter.Export(GridControl2, Text);
        }

        private void BandedGridView1_RowClick(object sender, DevExpress.XtraGrid.Views.Grid.RowClickEventArgs e)
        {
            findDetail();
        }
    }
}