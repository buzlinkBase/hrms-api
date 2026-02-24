namespace Buzlink.HR.UI
{
    partial class CreateOT
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CreateOT));
			this.startdate = new DevExpress.XtraEditors.DateEdit();
			this.labelControl11 = new DevExpress.XtraEditors.LabelControl();
			this.labelControl10 = new DevExpress.XtraEditors.LabelControl();
			this.labelControl3 = new DevExpress.XtraEditors.LabelControl();
			this.labelControl2 = new DevExpress.XtraEditors.LabelControl();
			this.labelControl1 = new DevExpress.XtraEditors.LabelControl();
			this.positiontxt = new DevExpress.XtraEditors.TextEdit();
			this.departmenttxt = new DevExpress.XtraEditors.TextEdit();
			this.endDate = new DevExpress.XtraEditors.DateEdit();
			this.memoEdit1 = new DevExpress.XtraEditors.MemoEdit();
			this.labelControl5 = new DevExpress.XtraEditors.LabelControl();
			this.barManager1 = new DevExpress.XtraBars.BarManager(this.components);
			this.bar1 = new DevExpress.XtraBars.Bar();
			this.barButtonItem2 = new DevExpress.XtraBars.BarButtonItem();
			this.barButtonItem1 = new DevExpress.XtraBars.BarButtonItem();
			this.bar3 = new DevExpress.XtraBars.Bar();
			this.barDockControlTop = new DevExpress.XtraBars.BarDockControl();
			this.barDockControlBottom = new DevExpress.XtraBars.BarDockControl();
			this.barDockControlLeft = new DevExpress.XtraBars.BarDockControl();
			this.barDockControlRight = new DevExpress.XtraBars.BarDockControl();
			this.employeeName = new DevExpress.XtraEditors.SearchLookUpEdit();
			this.searchLookUpEdit1View = new DevExpress.XtraGrid.Views.Grid.GridView();
			this.gridColumn1 = new DevExpress.XtraGrid.Columns.GridColumn();
			((System.ComponentModel.ISupportInitialize)(this.startdate.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.startdate.Properties.CalendarTimeProperties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.positiontxt.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.departmenttxt.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.endDate.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.endDate.Properties.CalendarTimeProperties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.memoEdit1.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.barManager1)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.employeeName.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.searchLookUpEdit1View)).BeginInit();
			this.SuspendLayout();
			// 
			// startdate
			// 
			this.startdate.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.startdate.EditValue = new System.DateTime(2021, 9, 16, 11, 56, 28, 124);
			this.startdate.Location = new System.Drawing.Point(126, 144);
			this.startdate.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.startdate.Name = "startdate";
			this.startdate.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
			this.startdate.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
			this.startdate.Properties.DisplayFormat.FormatString = "MMM dd, yyyy";
			this.startdate.Properties.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
			this.startdate.Properties.MaskSettings.Set("mask", "MMM dd, yyyy HH:mm");
			this.startdate.Properties.UseMaskAsDisplayFormat = true;
			this.startdate.Size = new System.Drawing.Size(551, 20);
			this.startdate.TabIndex = 4;
			this.startdate.EditValueChanged += new System.EventHandler(this.startdate_EditValueChanged);
			// 
			// labelControl11
			// 
			this.labelControl11.Location = new System.Drawing.Point(43, 172);
			this.labelControl11.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.labelControl11.Name = "labelControl11";
			this.labelControl11.Size = new System.Drawing.Size(44, 13);
			this.labelControl11.TabIndex = 2;
			this.labelControl11.Text = "End Date";
			// 
			// labelControl10
			// 
			this.labelControl10.Location = new System.Drawing.Point(43, 149);
			this.labelControl10.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.labelControl10.Name = "labelControl10";
			this.labelControl10.Size = new System.Drawing.Size(50, 13);
			this.labelControl10.TabIndex = 2;
			this.labelControl10.Text = "Start Date";
			// 
			// labelControl3
			// 
			this.labelControl3.Location = new System.Drawing.Point(43, 100);
			this.labelControl3.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.labelControl3.Name = "labelControl3";
			this.labelControl3.Size = new System.Drawing.Size(37, 13);
			this.labelControl3.TabIndex = 2;
			this.labelControl3.Text = "Position";
			// 
			// labelControl2
			// 
			this.labelControl2.Location = new System.Drawing.Point(43, 77);
			this.labelControl2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.labelControl2.Name = "labelControl2";
			this.labelControl2.Size = new System.Drawing.Size(57, 13);
			this.labelControl2.TabIndex = 2;
			this.labelControl2.Text = "Department";
			// 
			// labelControl1
			// 
			this.labelControl1.Location = new System.Drawing.Point(43, 55);
			this.labelControl1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.labelControl1.Name = "labelControl1";
			this.labelControl1.Size = new System.Drawing.Size(46, 13);
			this.labelControl1.TabIndex = 2;
			this.labelControl1.Text = "Employee";
			// 
			// positiontxt
			// 
			this.positiontxt.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.positiontxt.Location = new System.Drawing.Point(126, 97);
			this.positiontxt.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.positiontxt.Name = "positiontxt";
			this.positiontxt.Properties.ReadOnly = true;
			this.positiontxt.Size = new System.Drawing.Size(551, 20);
			this.positiontxt.TabIndex = 1;
			// 
			// departmenttxt
			// 
			this.departmenttxt.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.departmenttxt.Location = new System.Drawing.Point(126, 74);
			this.departmenttxt.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.departmenttxt.Name = "departmenttxt";
			this.departmenttxt.Properties.ReadOnly = true;
			this.departmenttxt.Size = new System.Drawing.Size(551, 20);
			this.departmenttxt.TabIndex = 1;
			// 
			// endDate
			// 
			this.endDate.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.endDate.EditValue = new System.DateTime(2021, 9, 16, 11, 56, 28, 124);
			this.endDate.Location = new System.Drawing.Point(126, 168);
			this.endDate.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.endDate.Name = "endDate";
			this.endDate.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
			this.endDate.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
			this.endDate.Properties.DisplayFormat.FormatString = "MMM dd, yyyy";
			this.endDate.Properties.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
			this.endDate.Properties.MaskSettings.Set("mask", "MMM dd, yyyy HH:mm");
			this.endDate.Properties.UseMaskAsDisplayFormat = true;
			this.endDate.Size = new System.Drawing.Size(551, 20);
			this.endDate.TabIndex = 4;
			this.endDate.EditValueChanged += new System.EventHandler(this.endDate_EditValueChanged);
			// 
			// memoEdit1
			// 
			this.memoEdit1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.memoEdit1.EditValue = "";
			this.memoEdit1.Location = new System.Drawing.Point(43, 236);
			this.memoEdit1.Name = "memoEdit1";
			this.memoEdit1.Properties.NullValuePrompt = "Enter Reason here!";
			this.memoEdit1.Size = new System.Drawing.Size(634, 92);
			this.memoEdit1.TabIndex = 10;
			// 
			// labelControl5
			// 
			this.labelControl5.Location = new System.Drawing.Point(43, 218);
			this.labelControl5.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.labelControl5.Name = "labelControl5";
			this.labelControl5.Size = new System.Drawing.Size(36, 13);
			this.labelControl5.TabIndex = 2;
			this.labelControl5.Text = "Reason";
			// 
			// barManager1
			// 
			this.barManager1.Bars.AddRange(new DevExpress.XtraBars.Bar[] {
            this.bar1,
            this.bar3});
			this.barManager1.DockControls.Add(this.barDockControlTop);
			this.barManager1.DockControls.Add(this.barDockControlBottom);
			this.barManager1.DockControls.Add(this.barDockControlLeft);
			this.barManager1.DockControls.Add(this.barDockControlRight);
			this.barManager1.Form = this;
			this.barManager1.Items.AddRange(new DevExpress.XtraBars.BarItem[] {
            this.barButtonItem1,
            this.barButtonItem2});
			this.barManager1.MaxItemId = 2;
			this.barManager1.StatusBar = this.bar3;
			// 
			// bar1
			// 
			this.bar1.BarName = "Tools";
			this.bar1.DockCol = 0;
			this.bar1.DockRow = 0;
			this.bar1.DockStyle = DevExpress.XtraBars.BarDockStyle.Top;
			this.bar1.FloatLocation = new System.Drawing.Point(240, 103);
			this.bar1.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[] {
            new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.barButtonItem2, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
            new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.barButtonItem1, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph)});
			this.bar1.Text = "Tools";
			// 
			// barButtonItem2
			// 
			this.barButtonItem2.Caption = "Clear";
			this.barButtonItem2.Id = 1;
			this.barButtonItem2.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("barButtonItem2.ImageOptions.Image")));
			this.barButtonItem2.ImageOptions.LargeImage = ((System.Drawing.Image)(resources.GetObject("barButtonItem2.ImageOptions.LargeImage")));
			this.barButtonItem2.Name = "barButtonItem2";
			this.barButtonItem2.ItemClick += new DevExpress.XtraBars.ItemClickEventHandler(this.barButtonItem2_ItemClick);
			// 
			// barButtonItem1
			// 
			this.barButtonItem1.Caption = "Save";
			this.barButtonItem1.Id = 0;
			this.barButtonItem1.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("barButtonItem1.ImageOptions.Image")));
			this.barButtonItem1.ImageOptions.LargeImage = ((System.Drawing.Image)(resources.GetObject("barButtonItem1.ImageOptions.LargeImage")));
			this.barButtonItem1.Name = "barButtonItem1";
			this.barButtonItem1.ItemClick += new DevExpress.XtraBars.ItemClickEventHandler(this.barButtonItem1_ItemClick);
			// 
			// bar3
			// 
			this.bar3.BarName = "Status bar";
			this.bar3.CanDockStyle = DevExpress.XtraBars.BarCanDockStyle.Bottom;
			this.bar3.DockCol = 0;
			this.bar3.DockRow = 0;
			this.bar3.DockStyle = DevExpress.XtraBars.BarDockStyle.Bottom;
			this.bar3.OptionsBar.AllowQuickCustomization = false;
			this.bar3.OptionsBar.DrawDragBorder = false;
			this.bar3.OptionsBar.UseWholeRow = true;
			this.bar3.Text = "Status bar";
			// 
			// barDockControlTop
			// 
			this.barDockControlTop.CausesValidation = false;
			this.barDockControlTop.Dock = System.Windows.Forms.DockStyle.Top;
			this.barDockControlTop.Location = new System.Drawing.Point(0, 0);
			this.barDockControlTop.Manager = this.barManager1;
			this.barDockControlTop.Size = new System.Drawing.Size(715, 28);
			// 
			// barDockControlBottom
			// 
			this.barDockControlBottom.CausesValidation = false;
			this.barDockControlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.barDockControlBottom.Location = new System.Drawing.Point(0, 370);
			this.barDockControlBottom.Manager = this.barManager1;
			this.barDockControlBottom.Size = new System.Drawing.Size(715, 18);
			// 
			// barDockControlLeft
			// 
			this.barDockControlLeft.CausesValidation = false;
			this.barDockControlLeft.Dock = System.Windows.Forms.DockStyle.Left;
			this.barDockControlLeft.Location = new System.Drawing.Point(0, 28);
			this.barDockControlLeft.Manager = this.barManager1;
			this.barDockControlLeft.Size = new System.Drawing.Size(0, 342);
			// 
			// barDockControlRight
			// 
			this.barDockControlRight.CausesValidation = false;
			this.barDockControlRight.Dock = System.Windows.Forms.DockStyle.Right;
			this.barDockControlRight.Location = new System.Drawing.Point(715, 28);
			this.barDockControlRight.Manager = this.barManager1;
			this.barDockControlRight.Size = new System.Drawing.Size(0, 342);
			// 
			// employeeName
			// 
			this.employeeName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.employeeName.EditValue = 0;
			this.employeeName.Location = new System.Drawing.Point(126, 50);
			this.employeeName.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.employeeName.Name = "employeeName";
			this.employeeName.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
			this.employeeName.Properties.DisplayMember = "FullName";
			this.employeeName.Properties.NullText = "";
			this.employeeName.Properties.PopupView = this.searchLookUpEdit1View;
			this.employeeName.Properties.ValueMember = "Id";
			this.employeeName.Size = new System.Drawing.Size(551, 20);
			this.employeeName.TabIndex = 7;
			this.employeeName.EditValueChanged += new System.EventHandler(this.employeeName_EditValueChanged);
			// 
			// searchLookUpEdit1View
			// 
			this.searchLookUpEdit1View.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.gridColumn1});
			this.searchLookUpEdit1View.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
			this.searchLookUpEdit1View.Name = "searchLookUpEdit1View";
			this.searchLookUpEdit1View.OptionsSelection.EnableAppearanceFocusedCell = false;
			this.searchLookUpEdit1View.OptionsView.ShowGroupPanel = false;
			// 
			// gridColumn1
			// 
			this.gridColumn1.Caption = "Name";
			this.gridColumn1.FieldName = "FullName";
			this.gridColumn1.Name = "gridColumn1";
			this.gridColumn1.Visible = true;
			this.gridColumn1.VisibleIndex = 0;
			// 
			// CreateOT
			// 
			this.AccessibleDescription = "Overtime Application";
			this.AccessibleName = "";
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(715, 388);
			this.Controls.Add(this.memoEdit1);
			this.Controls.Add(this.labelControl10);
			this.Controls.Add(this.endDate);
			this.Controls.Add(this.labelControl3);
			this.Controls.Add(this.startdate);
			this.Controls.Add(this.labelControl1);
			this.Controls.Add(this.labelControl11);
			this.Controls.Add(this.labelControl5);
			this.Controls.Add(this.labelControl2);
			this.Controls.Add(this.departmenttxt);
			this.Controls.Add(this.positiontxt);
			this.Controls.Add(this.employeeName);
			this.Controls.Add(this.barDockControlLeft);
			this.Controls.Add(this.barDockControlRight);
			this.Controls.Add(this.barDockControlBottom);
			this.Controls.Add(this.barDockControlTop);
			this.Name = "CreateOT";
			this.Text = "Overtime Application";
			((System.ComponentModel.ISupportInitialize)(this.startdate.Properties.CalendarTimeProperties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.startdate.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.positiontxt.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.departmenttxt.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.endDate.Properties.CalendarTimeProperties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.endDate.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.memoEdit1.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.barManager1)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.employeeName.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.searchLookUpEdit1View)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

        }

        #endregion
        private DevExpress.XtraEditors.DateEdit startdate;
        private DevExpress.XtraEditors.LabelControl labelControl11;
        private DevExpress.XtraEditors.LabelControl labelControl10;
        private DevExpress.XtraEditors.LabelControl labelControl3;
        private DevExpress.XtraEditors.LabelControl labelControl2;
        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraEditors.TextEdit positiontxt;
        private DevExpress.XtraEditors.TextEdit departmenttxt;
        private DevExpress.XtraEditors.MemoEdit memoEdit1;
        private DevExpress.XtraEditors.DateEdit endDate;
        private DevExpress.XtraEditors.LabelControl labelControl5;
        private DevExpress.XtraBars.BarManager barManager1;
        private DevExpress.XtraBars.Bar bar1;
        private DevExpress.XtraBars.BarButtonItem barButtonItem2;
        private DevExpress.XtraBars.BarButtonItem barButtonItem1;
        private DevExpress.XtraBars.Bar bar3;
        private DevExpress.XtraBars.BarDockControl barDockControlTop;
        private DevExpress.XtraBars.BarDockControl barDockControlBottom;
        private DevExpress.XtraBars.BarDockControl barDockControlLeft;
        private DevExpress.XtraBars.BarDockControl barDockControlRight;
        private DevExpress.XtraEditors.SearchLookUpEdit employeeName;
        private DevExpress.XtraGrid.Views.Grid.GridView searchLookUpEdit1View;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn1;
	}
}