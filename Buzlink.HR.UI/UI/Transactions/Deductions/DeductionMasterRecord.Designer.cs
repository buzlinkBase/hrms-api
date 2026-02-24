
namespace Buzlink.HR.UI
{
    partial class DeductionMasterRecord
	{
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
			this.components = new System.ComponentModel.Container();
			DevExpress.XtraGrid.GridLevelNode gridLevelNode1 = new DevExpress.XtraGrid.GridLevelNode();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DeductionMasterRecord));
			this.gridView1 = new DevExpress.XtraGrid.Views.Grid.GridView();
			this.gridColumn14 = new DevExpress.XtraGrid.Columns.GridColumn();
			this.gridColumn15 = new DevExpress.XtraGrid.Columns.GridColumn();
			this.gridColumn1 = new DevExpress.XtraGrid.Columns.GridColumn();
			this.gridColumn2 = new DevExpress.XtraGrid.Columns.GridColumn();
			this.gridColumn3 = new DevExpress.XtraGrid.Columns.GridColumn();
			this.GridControl2 = new DevExpress.XtraGrid.GridControl();
			this.BandedGridView1 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridView();
			this.GridBand1 = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
			this.GridColumn4 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
			this.GridColumn5 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
			this.GridColumn6 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
			this.gridBand2 = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
			this.GridColumn12 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
			this.GridColumn11 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
			this.GridColumn10 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
			this.gridBand4 = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
			this.GridColumn7 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
			this.GridColumn8 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
			this.gridBand3 = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
			this.GridColumn13 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
			this.GridColumn9 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
			this.bandedGridColumn2 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
			this.bandedGridColumn1 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
			this.barManager1 = new DevExpress.XtraBars.BarManager(this.components);
			this.bar1 = new DevExpress.XtraBars.Bar();
			this.barButtonItem4 = new DevExpress.XtraBars.BarButtonItem();
			this.barButtonItem1 = new DevExpress.XtraBars.BarButtonItem();
			this.barButtonItem2 = new DevExpress.XtraBars.BarButtonItem();
			this.barButtonItem3 = new DevExpress.XtraBars.BarButtonItem();
			this.barButtonItem5 = new DevExpress.XtraBars.BarButtonItem();
			this.barButtonItem6 = new DevExpress.XtraBars.BarButtonItem();
			this.barDockControlTop = new DevExpress.XtraBars.BarDockControl();
			this.barDockControlBottom = new DevExpress.XtraBars.BarDockControl();
			this.barDockControlLeft = new DevExpress.XtraBars.BarDockControl();
			this.barDockControlRight = new DevExpress.XtraBars.BarDockControl();
			this.filterEditorControl1 = new DevExpress.DataAccess.UI.FilterEditorControl();
			this.panelControl1 = new DevExpress.XtraEditors.PanelControl();
			this.labelControl1 = new DevExpress.XtraEditors.LabelControl();
			this.dateRangeFilter1 = new Buzlink.HR.UI.DateRangeFilter();
			((System.ComponentModel.ISupportInitialize)(this.gridView1)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.GridControl2)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.BandedGridView1)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.barManager1)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.panelControl1)).BeginInit();
			this.panelControl1.SuspendLayout();
			this.SuspendLayout();
			// 
			// gridView1
			// 
			this.gridView1.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.gridColumn14,
            this.gridColumn15,
            this.gridColumn1,
            this.gridColumn2,
            this.gridColumn3});
			this.gridView1.GridControl = this.GridControl2;
			this.gridView1.Name = "gridView1";
			this.gridView1.OptionsBehavior.Editable = false;
			this.gridView1.OptionsBehavior.ReadOnly = true;
			// 
			// gridColumn14
			// 
			this.gridColumn14.Caption = "Description";
			this.gridColumn14.FieldName = "Description";
			this.gridColumn14.Name = "gridColumn14";
			this.gridColumn14.Visible = true;
			this.gridColumn14.VisibleIndex = 0;
			// 
			// gridColumn15
			// 
			this.gridColumn15.Caption = "Due Date";
			this.gridColumn15.DisplayFormat.FormatString = "MMM dd, yyyy";
			this.gridColumn15.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
			this.gridColumn15.FieldName = "Date";
			this.gridColumn15.Name = "gridColumn15";
			this.gridColumn15.Visible = true;
			this.gridColumn15.VisibleIndex = 1;
			// 
			// gridColumn1
			// 
			this.gridColumn1.Caption = "Principal";
			this.gridColumn1.DisplayFormat.FormatString = "{0:n2}";
			this.gridColumn1.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
			this.gridColumn1.FieldName = "Principal";
			this.gridColumn1.Name = "gridColumn1";
			this.gridColumn1.Visible = true;
			this.gridColumn1.VisibleIndex = 2;
			// 
			// gridColumn2
			// 
			this.gridColumn2.Caption = "Interest";
			this.gridColumn2.DisplayFormat.FormatString = "{0:n2}";
			this.gridColumn2.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
			this.gridColumn2.FieldName = "Interest";
			this.gridColumn2.Name = "gridColumn2";
			this.gridColumn2.Visible = true;
			this.gridColumn2.VisibleIndex = 3;
			// 
			// gridColumn3
			// 
			this.gridColumn3.Caption = "Amount";
			this.gridColumn3.DisplayFormat.FormatString = "{0:n2}";
			this.gridColumn3.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
			this.gridColumn3.FieldName = "Amount";
			this.gridColumn3.Name = "gridColumn3";
			this.gridColumn3.Visible = true;
			this.gridColumn3.VisibleIndex = 4;
			// 
			// GridControl2
			// 
			this.GridControl2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.GridControl2.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			gridLevelNode1.LevelTemplate = this.gridView1;
			gridLevelNode1.RelationName = "Details";
			this.GridControl2.LevelTree.Nodes.AddRange(new DevExpress.XtraGrid.GridLevelNode[] {
            gridLevelNode1});
			this.GridControl2.Location = new System.Drawing.Point(0, 82);
			this.GridControl2.MainView = this.BandedGridView1;
			this.GridControl2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.GridControl2.Name = "GridControl2";
			this.GridControl2.Size = new System.Drawing.Size(1105, 439);
			this.GridControl2.TabIndex = 10;
			this.GridControl2.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.BandedGridView1,
            this.gridView1});
			// 
			// BandedGridView1
			// 
			this.BandedGridView1.Bands.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.GridBand[] {
            this.GridBand1,
            this.gridBand2,
            this.gridBand4,
            this.gridBand3});
			this.BandedGridView1.Columns.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn[] {
            this.GridColumn13,
            this.GridColumn4,
            this.GridColumn5,
            this.GridColumn6,
            this.GridColumn12,
            this.GridColumn11,
            this.GridColumn10,
            this.GridColumn7,
            this.GridColumn8,
            this.bandedGridColumn2,
            this.bandedGridColumn1,
            this.GridColumn9});
			this.BandedGridView1.DetailHeight = 284;
			this.BandedGridView1.GridControl = this.GridControl2;
			this.BandedGridView1.Name = "BandedGridView1";
			this.BandedGridView1.OptionsBehavior.Editable = false;
			this.BandedGridView1.OptionsBehavior.ReadOnly = true;
			this.BandedGridView1.OptionsView.ColumnAutoWidth = false;
			this.BandedGridView1.RowClick += new DevExpress.XtraGrid.Views.Grid.RowClickEventHandler(this.BandedGridView1_RowClick);
			// 
			// GridBand1
			// 
			this.GridBand1.Columns.Add(this.GridColumn4);
			this.GridBand1.Columns.Add(this.GridColumn5);
			this.GridBand1.Columns.Add(this.GridColumn6);
			this.GridBand1.Name = "GridBand1";
			this.GridBand1.VisibleIndex = 0;
			this.GridBand1.Width = 343;
			// 
			// GridColumn4
			// 
			this.GridColumn4.AppearanceHeader.Options.UseTextOptions = true;
			this.GridColumn4.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
			this.GridColumn4.Caption = "Trans Date";
			this.GridColumn4.DisplayFormat.FormatString = "MMM dd, yyyy";
			this.GridColumn4.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
			this.GridColumn4.FieldName = "EncodeDate";
			this.GridColumn4.Name = "GridColumn4";
			this.GridColumn4.OptionsColumn.AllowEdit = false;
			this.GridColumn4.OptionsColumn.ReadOnly = true;
			this.GridColumn4.Visible = true;
			this.GridColumn4.Width = 85;
			// 
			// GridColumn5
			// 
			this.GridColumn5.AppearanceHeader.Options.UseTextOptions = true;
			this.GridColumn5.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
			this.GridColumn5.Caption = "Employee";
			this.GridColumn5.FieldName = "FullName";
			this.GridColumn5.Name = "GridColumn5";
			this.GridColumn5.OptionsColumn.AllowEdit = false;
			this.GridColumn5.OptionsColumn.ReadOnly = true;
			this.GridColumn5.Visible = true;
			this.GridColumn5.Width = 140;
			// 
			// GridColumn6
			// 
			this.GridColumn6.AppearanceHeader.Options.UseTextOptions = true;
			this.GridColumn6.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
			this.GridColumn6.Caption = "Deduction Name";
			this.GridColumn6.FieldName = "DeductionName";
			this.GridColumn6.Name = "GridColumn6";
			this.GridColumn6.OptionsColumn.AllowEdit = false;
			this.GridColumn6.OptionsColumn.ReadOnly = true;
			this.GridColumn6.Visible = true;
			this.GridColumn6.Width = 118;
			// 
			// gridBand2
			// 
			this.gridBand2.AppearanceHeader.Options.UseTextOptions = true;
			this.gridBand2.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
			this.gridBand2.Caption = "Amount";
			this.gridBand2.Columns.Add(this.GridColumn12);
			this.gridBand2.Columns.Add(this.GridColumn11);
			this.gridBand2.Columns.Add(this.GridColumn10);
			this.gridBand2.Name = "gridBand2";
			this.gridBand2.VisibleIndex = 1;
			this.gridBand2.Width = 354;
			// 
			// GridColumn12
			// 
			this.GridColumn12.AppearanceHeader.Options.UseTextOptions = true;
			this.GridColumn12.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
			this.GridColumn12.Caption = "Principal";
			this.GridColumn12.DisplayFormat.FormatString = "{0:n2}";
			this.GridColumn12.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
			this.GridColumn12.FieldName = "TotalPrincipal";
			this.GridColumn12.Name = "GridColumn12";
			this.GridColumn12.OptionsColumn.AllowEdit = false;
			this.GridColumn12.OptionsColumn.ReadOnly = true;
			this.GridColumn12.Visible = true;
			this.GridColumn12.Width = 118;
			// 
			// GridColumn11
			// 
			this.GridColumn11.AppearanceHeader.Options.UseTextOptions = true;
			this.GridColumn11.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
			this.GridColumn11.Caption = "Interest";
			this.GridColumn11.DisplayFormat.FormatString = "{0:n2}";
			this.GridColumn11.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
			this.GridColumn11.FieldName = "InterestRate";
			this.GridColumn11.Name = "GridColumn11";
			this.GridColumn11.OptionsColumn.AllowEdit = false;
			this.GridColumn11.OptionsColumn.ReadOnly = true;
			this.GridColumn11.Visible = true;
			this.GridColumn11.Width = 118;
			// 
			// GridColumn10
			// 
			this.GridColumn10.AppearanceHeader.Options.UseTextOptions = true;
			this.GridColumn10.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
			this.GridColumn10.Caption = "Total";
			this.GridColumn10.DisplayFormat.FormatString = "{0:n2}";
			this.GridColumn10.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
			this.GridColumn10.FieldName = "TotalAmount";
			this.GridColumn10.Name = "GridColumn10";
			this.GridColumn10.OptionsColumn.AllowEdit = false;
			this.GridColumn10.OptionsColumn.ReadOnly = true;
			this.GridColumn10.Visible = true;
			this.GridColumn10.Width = 118;
			// 
			// gridBand4
			// 
			this.gridBand4.AppearanceHeader.Options.UseTextOptions = true;
			this.gridBand4.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
			this.gridBand4.Caption = "Date";
			this.gridBand4.Columns.Add(this.GridColumn7);
			this.gridBand4.Columns.Add(this.GridColumn8);
			this.gridBand4.Name = "gridBand4";
			this.gridBand4.VisibleIndex = 2;
			this.gridBand4.Width = 236;
			// 
			// GridColumn7
			// 
			this.GridColumn7.AppearanceHeader.Options.UseTextOptions = true;
			this.GridColumn7.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
			this.GridColumn7.Caption = "Start Date";
			this.GridColumn7.FieldName = "StartDate";
			this.GridColumn7.Name = "GridColumn7";
			this.GridColumn7.OptionsColumn.AllowEdit = false;
			this.GridColumn7.OptionsColumn.ReadOnly = true;
			this.GridColumn7.Visible = true;
			this.GridColumn7.Width = 118;
			// 
			// GridColumn8
			// 
			this.GridColumn8.AppearanceHeader.Options.UseTextOptions = true;
			this.GridColumn8.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
			this.GridColumn8.Caption = "End Date";
			this.GridColumn8.FieldName = "EndDate";
			this.GridColumn8.Name = "GridColumn8";
			this.GridColumn8.OptionsColumn.AllowEdit = false;
			this.GridColumn8.OptionsColumn.ReadOnly = true;
			this.GridColumn8.Visible = true;
			this.GridColumn8.Width = 118;
			// 
			// gridBand3
			// 
			this.gridBand3.AppearanceHeader.Options.UseTextOptions = true;
			this.gridBand3.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
			this.gridBand3.Columns.Add(this.GridColumn13);
			this.gridBand3.Columns.Add(this.GridColumn9);
			this.gridBand3.Name = "gridBand3";
			this.gridBand3.VisibleIndex = 3;
			this.gridBand3.Width = 247;
			// 
			// GridColumn13
			// 
			this.GridColumn13.AppearanceHeader.Options.UseTextOptions = true;
			this.GridColumn13.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
			this.GridColumn13.Caption = "Status";
			this.GridColumn13.FieldName = "Status";
			this.GridColumn13.Name = "GridColumn13";
			this.GridColumn13.OptionsColumn.AllowEdit = false;
			this.GridColumn13.OptionsColumn.ReadOnly = true;
			this.GridColumn13.Visible = true;
			this.GridColumn13.Width = 129;
			// 
			// GridColumn9
			// 
			this.GridColumn9.AppearanceHeader.Options.UseTextOptions = true;
			this.GridColumn9.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
			this.GridColumn9.Caption = "Note";
			this.GridColumn9.FieldName = "Remarks";
			this.GridColumn9.Name = "GridColumn9";
			this.GridColumn9.OptionsColumn.AllowEdit = false;
			this.GridColumn9.OptionsColumn.ReadOnly = true;
			this.GridColumn9.Visible = true;
			this.GridColumn9.Width = 118;
			// 
			// bandedGridColumn2
			// 
			this.bandedGridColumn2.Caption = "MOP";
			this.bandedGridColumn2.FieldName = "modeOfPayment";
			this.bandedGridColumn2.Name = "bandedGridColumn2";
			this.bandedGridColumn2.OptionsColumn.AllowEdit = false;
			this.bandedGridColumn2.OptionsColumn.ReadOnly = true;
			this.bandedGridColumn2.Visible = true;
			// 
			// bandedGridColumn1
			// 
			this.bandedGridColumn1.Caption = "Terms";
			this.bandedGridColumn1.FieldName = "Terms";
			this.bandedGridColumn1.Name = "bandedGridColumn1";
			this.bandedGridColumn1.OptionsColumn.AllowEdit = false;
			this.bandedGridColumn1.OptionsColumn.ReadOnly = true;
			this.bandedGridColumn1.Visible = true;
			// 
			// barManager1
			// 
			this.barManager1.Bars.AddRange(new DevExpress.XtraBars.Bar[] {
            this.bar1});
			this.barManager1.DockControls.Add(this.barDockControlTop);
			this.barManager1.DockControls.Add(this.barDockControlBottom);
			this.barManager1.DockControls.Add(this.barDockControlLeft);
			this.barManager1.DockControls.Add(this.barDockControlRight);
			this.barManager1.Form = this;
			this.barManager1.Items.AddRange(new DevExpress.XtraBars.BarItem[] {
            this.barButtonItem1,
            this.barButtonItem2,
            this.barButtonItem3,
            this.barButtonItem4,
            this.barButtonItem5,
            this.barButtonItem6});
			this.barManager1.MaxItemId = 7;
			// 
			// bar1
			// 
			this.bar1.BarName = "Tools";
			this.bar1.DockCol = 0;
			this.bar1.DockRow = 0;
			this.bar1.DockStyle = DevExpress.XtraBars.BarDockStyle.Top;
			this.bar1.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[] {
            new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.barButtonItem4, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
            new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.barButtonItem1, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
            new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.barButtonItem2, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
            new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.barButtonItem3, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
            new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.barButtonItem5, "", true, true, true, 0, null, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
            new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.barButtonItem6, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph)});
			this.bar1.Text = "Tools";
			// 
			// barButtonItem4
			// 
			this.barButtonItem4.Caption = "Refresh";
			this.barButtonItem4.Id = 3;
			this.barButtonItem4.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("barButtonItem4.ImageOptions.Image")));
			this.barButtonItem4.ImageOptions.LargeImage = ((System.Drawing.Image)(resources.GetObject("barButtonItem4.ImageOptions.LargeImage")));
			this.barButtonItem4.Name = "barButtonItem4";
			this.barButtonItem4.ItemClick += new DevExpress.XtraBars.ItemClickEventHandler(this.barButtonItem4_ItemClick);
			// 
			// barButtonItem1
			// 
			this.barButtonItem1.Caption = "Create";
			this.barButtonItem1.Id = 0;
			this.barButtonItem1.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("barButtonItem1.ImageOptions.Image")));
			this.barButtonItem1.ImageOptions.LargeImage = ((System.Drawing.Image)(resources.GetObject("barButtonItem1.ImageOptions.LargeImage")));
			this.barButtonItem1.Name = "barButtonItem1";
			this.barButtonItem1.ItemClick += new DevExpress.XtraBars.ItemClickEventHandler(this.barButtonItem1_ItemClick);
			// 
			// barButtonItem2
			// 
			this.barButtonItem2.Caption = "Edit";
			this.barButtonItem2.Id = 1;
			this.barButtonItem2.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("barButtonItem2.ImageOptions.Image")));
			this.barButtonItem2.ImageOptions.LargeImage = ((System.Drawing.Image)(resources.GetObject("barButtonItem2.ImageOptions.LargeImage")));
			this.barButtonItem2.Name = "barButtonItem2";
			this.barButtonItem2.ItemClick += new DevExpress.XtraBars.ItemClickEventHandler(this.barButtonItem2_ItemClick);
			// 
			// barButtonItem3
			// 
			this.barButtonItem3.Caption = "Cancel";
			this.barButtonItem3.Id = 2;
			this.barButtonItem3.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("barButtonItem3.ImageOptions.Image")));
			this.barButtonItem3.ImageOptions.LargeImage = ((System.Drawing.Image)(resources.GetObject("barButtonItem3.ImageOptions.LargeImage")));
			this.barButtonItem3.Name = "barButtonItem3";
			this.barButtonItem3.ItemClick += new DevExpress.XtraBars.ItemClickEventHandler(this.barButtonItem3_ItemClick);
			// 
			// barButtonItem5
			// 
			this.barButtonItem5.Caption = "Export";
			this.barButtonItem5.Id = 4;
			this.barButtonItem5.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("barButtonItem5.ImageOptions.Image")));
			this.barButtonItem5.ImageOptions.LargeImage = ((System.Drawing.Image)(resources.GetObject("barButtonItem5.ImageOptions.LargeImage")));
			this.barButtonItem5.Name = "barButtonItem5";
			this.barButtonItem5.ItemClick += new DevExpress.XtraBars.ItemClickEventHandler(this.barButtonItem5_ItemClick);
			// 
			// barButtonItem6
			// 
			this.barButtonItem6.Caption = "Print";
			this.barButtonItem6.Id = 5;
			this.barButtonItem6.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("barButtonItem6.ImageOptions.Image")));
			this.barButtonItem6.ImageOptions.LargeImage = ((System.Drawing.Image)(resources.GetObject("barButtonItem6.ImageOptions.LargeImage")));
			this.barButtonItem6.Name = "barButtonItem6";
			this.barButtonItem6.Visibility = DevExpress.XtraBars.BarItemVisibility.Never;
			// 
			// barDockControlTop
			// 
			this.barDockControlTop.CausesValidation = false;
			this.barDockControlTop.Dock = System.Windows.Forms.DockStyle.Top;
			this.barDockControlTop.Location = new System.Drawing.Point(0, 0);
			this.barDockControlTop.Manager = this.barManager1;
			this.barDockControlTop.Size = new System.Drawing.Size(1105, 28);
			// 
			// barDockControlBottom
			// 
			this.barDockControlBottom.CausesValidation = false;
			this.barDockControlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.barDockControlBottom.Location = new System.Drawing.Point(0, 521);
			this.barDockControlBottom.Manager = this.barManager1;
			this.barDockControlBottom.Size = new System.Drawing.Size(1105, 0);
			// 
			// barDockControlLeft
			// 
			this.barDockControlLeft.CausesValidation = false;
			this.barDockControlLeft.Dock = System.Windows.Forms.DockStyle.Left;
			this.barDockControlLeft.Location = new System.Drawing.Point(0, 28);
			this.barDockControlLeft.Manager = this.barManager1;
			this.barDockControlLeft.Size = new System.Drawing.Size(0, 493);
			// 
			// barDockControlRight
			// 
			this.barDockControlRight.CausesValidation = false;
			this.barDockControlRight.Dock = System.Windows.Forms.DockStyle.Right;
			this.barDockControlRight.Location = new System.Drawing.Point(1105, 28);
			this.barDockControlRight.Manager = this.barManager1;
			this.barDockControlRight.Size = new System.Drawing.Size(0, 493);
			// 
			// filterEditorControl1
			// 
			this.filterEditorControl1.ActiveView = DevExpress.XtraFilterEditor.FilterEditorActiveView.Visual;
			this.filterEditorControl1.AppearanceEmptyValueColor = System.Drawing.Color.Empty;
			this.filterEditorControl1.AppearanceFieldNameColor = System.Drawing.Color.Empty;
			this.filterEditorControl1.AppearanceGroupOperatorColor = System.Drawing.Color.Empty;
			this.filterEditorControl1.AppearanceOperatorColor = System.Drawing.Color.Empty;
			this.filterEditorControl1.AppearanceValueColor = System.Drawing.Color.Empty;
			this.filterEditorControl1.IsModified = false;
			this.filterEditorControl1.Location = new System.Drawing.Point(32067, 32022);
			this.filterEditorControl1.Name = "filterEditorControl1";
			this.filterEditorControl1.NodeSeparatorHeight = 2;
			this.filterEditorControl1.Size = new System.Drawing.Size(200, 200);
			this.filterEditorControl1.TabIndex = 0;
			this.filterEditorControl1.Text = "filterEditorControl1";
			this.filterEditorControl1.UseMenuForOperandsAndOperators = false;
			// 
			// panelControl1
			// 
			this.panelControl1.Controls.Add(this.labelControl1);
			this.panelControl1.Controls.Add(this.dateRangeFilter1);
			this.panelControl1.Controls.Add(this.filterEditorControl1);
			this.panelControl1.Dock = System.Windows.Forms.DockStyle.Top;
			this.panelControl1.Location = new System.Drawing.Point(0, 28);
			this.panelControl1.Name = "panelControl1";
			this.panelControl1.Size = new System.Drawing.Size(1105, 54);
			this.panelControl1.TabIndex = 4;
			// 
			// labelControl1
			// 
			this.labelControl1.Location = new System.Drawing.Point(21, 22);
			this.labelControl1.Name = "labelControl1";
			this.labelControl1.Size = new System.Drawing.Size(74, 13);
			this.labelControl1.TabIndex = 2;
			this.labelControl1.Text = "Recording Date";
			// 
			// dateRangeFilter1
			// 
			this.dateRangeFilter1.FilterName = "This Month";
			this.dateRangeFilter1.FromDate = new System.DateTime(2024, 1, 1, 0, 0, 0, 0);
			this.dateRangeFilter1.HideDate = false;
			this.dateRangeFilter1.Location = new System.Drawing.Point(112, 19);
			this.dateRangeFilter1.Name = "dateRangeFilter1";
			this.dateRangeFilter1.Size = new System.Drawing.Size(375, 22);
			this.dateRangeFilter1.TabIndex = 1;
			this.dateRangeFilter1.ToDate = new System.DateTime(2024, 1, 31, 23, 59, 59, 0);
			this.dateRangeFilter1.OnFilterChange += new Buzlink.HR.UI.DateRangeFilter.OnFilterChangeHandler(this.dateRangeFilter1_OnFilterChange);
			// 
			// DeductionMasterRecord
			// 
			this.AccessibleDescription = "Deduction Entry";
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1105, 521);
			this.Controls.Add(this.GridControl2);
			this.Controls.Add(this.panelControl1);
			this.Controls.Add(this.barDockControlLeft);
			this.Controls.Add(this.barDockControlRight);
			this.Controls.Add(this.barDockControlBottom);
			this.Controls.Add(this.barDockControlTop);
			this.Name = "DeductionMasterRecord";
			this.Text = "Deduction Master Record";
			((System.ComponentModel.ISupportInitialize)(this.gridView1)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.GridControl2)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.BandedGridView1)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.barManager1)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.panelControl1)).EndInit();
			this.panelControl1.ResumeLayout(false);
			this.panelControl1.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

        }

        #endregion

        private DevExpress.XtraBars.BarManager barManager1;
        private DevExpress.XtraBars.Bar bar1;
        private DevExpress.XtraBars.BarButtonItem barButtonItem4;
        private DevExpress.XtraBars.BarButtonItem barButtonItem1;
        private DevExpress.XtraBars.BarButtonItem barButtonItem2;
        private DevExpress.XtraBars.BarButtonItem barButtonItem3;
        private DevExpress.XtraBars.BarButtonItem barButtonItem5;
        private DevExpress.XtraBars.BarButtonItem barButtonItem6;
        private DevExpress.XtraBars.BarDockControl barDockControlTop;
        private DevExpress.XtraBars.BarDockControl barDockControlBottom;
        private DevExpress.XtraBars.BarDockControl barDockControlLeft;
        private DevExpress.XtraBars.BarDockControl barDockControlRight;
		internal DevExpress.XtraGrid.GridControl GridControl2;
		internal DevExpress.XtraGrid.Views.BandedGrid.BandedGridView BandedGridView1;
		internal DevExpress.XtraGrid.Views.BandedGrid.GridBand GridBand1;
		internal DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn GridColumn4;
		internal DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn GridColumn5;
		internal DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn GridColumn6;
		internal DevExpress.XtraGrid.Views.BandedGrid.GridBand gridBand2;
		internal DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn GridColumn12;
		internal DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn GridColumn11;
		internal DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn GridColumn10;
		internal DevExpress.XtraGrid.Views.BandedGrid.GridBand gridBand4;
		internal DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn GridColumn7;
		internal DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn GridColumn8;
		internal DevExpress.XtraGrid.Views.BandedGrid.GridBand gridBand3;
		internal DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn GridColumn13;
		internal DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn GridColumn9;
		private DevExpress.XtraEditors.PanelControl panelControl1;
		private DateRangeFilter dateRangeFilter1;
		private DevExpress.DataAccess.UI.FilterEditorControl filterEditorControl1;
		private DevExpress.XtraEditors.LabelControl labelControl1;
		private DevExpress.XtraGrid.Views.Grid.GridView gridView1;
		private DevExpress.XtraGrid.Columns.GridColumn gridColumn15;
		private DevExpress.XtraGrid.Columns.GridColumn gridColumn1;
		private DevExpress.XtraGrid.Columns.GridColumn gridColumn2;
		private DevExpress.XtraGrid.Columns.GridColumn gridColumn3;
		private DevExpress.XtraGrid.Columns.GridColumn gridColumn14;
		private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn2;
		private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn1;
	}
}