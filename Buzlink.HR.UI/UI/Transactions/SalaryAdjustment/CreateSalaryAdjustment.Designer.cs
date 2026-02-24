namespace Buzlink.HR.UI
{
    partial class CreateSalaryAdjustment
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CreateSalaryAdjustment));
			this.xtraTabControl1 = new DevExpress.XtraTab.XtraTabControl();
			this.xtraTabPage1 = new DevExpress.XtraTab.XtraTabPage();
			this.employeeName = new DevExpress.XtraEditors.SearchLookUpEdit();
			this.searchLookUpEdit1View = new DevExpress.XtraGrid.Views.Grid.GridView();
			this.gridColumn6 = new DevExpress.XtraGrid.Columns.GridColumn();
			this.labelControl3 = new DevExpress.XtraEditors.LabelControl();
			this.labelControl2 = new DevExpress.XtraEditors.LabelControl();
			this.labelControl1 = new DevExpress.XtraEditors.LabelControl();
			this.position = new DevExpress.XtraEditors.TextEdit();
			this.department = new DevExpress.XtraEditors.TextEdit();
			this.labelControl4 = new DevExpress.XtraEditors.LabelControl();
			this.Amount = new DevExpress.XtraEditors.SpinEdit();
			this.xtraTabControl2 = new DevExpress.XtraTab.XtraTabControl();
			this.xtraTabPage3 = new DevExpress.XtraTab.XtraTabPage();
			this.remarks = new DevExpress.XtraEditors.MemoEdit();
			this.period = new DevExpress.XtraEditors.DateEdit();
			this.labelControl5 = new DevExpress.XtraEditors.LabelControl();
			this.labelControl10 = new DevExpress.XtraEditors.LabelControl();
			this.barManager1 = new DevExpress.XtraBars.BarManager(this.components);
			this.bar1 = new DevExpress.XtraBars.Bar();
			this.barButtonItem2 = new DevExpress.XtraBars.BarButtonItem();
			this.barButtonItem1 = new DevExpress.XtraBars.BarButtonItem();
			this.bar3 = new DevExpress.XtraBars.Bar();
			this.barDockControlTop = new DevExpress.XtraBars.BarDockControl();
			this.barDockControlBottom = new DevExpress.XtraBars.BarDockControl();
			this.barDockControlLeft = new DevExpress.XtraBars.BarDockControl();
			this.barDockControlRight = new DevExpress.XtraBars.BarDockControl();
			((System.ComponentModel.ISupportInitialize)(this.xtraTabControl1)).BeginInit();
			this.xtraTabControl1.SuspendLayout();
			this.xtraTabPage1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.employeeName.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.searchLookUpEdit1View)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.position.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.department.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.Amount.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.xtraTabControl2)).BeginInit();
			this.xtraTabControl2.SuspendLayout();
			this.xtraTabPage3.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.remarks.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.period.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.period.Properties.CalendarTimeProperties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.barManager1)).BeginInit();
			this.SuspendLayout();
			// 
			// xtraTabControl1
			// 
			this.xtraTabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.xtraTabControl1.Location = new System.Drawing.Point(22, 46);
			this.xtraTabControl1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.xtraTabControl1.Name = "xtraTabControl1";
			this.xtraTabControl1.SelectedTabPage = this.xtraTabPage1;
			this.xtraTabControl1.Size = new System.Drawing.Size(579, 133);
			this.xtraTabControl1.TabIndex = 0;
			this.xtraTabControl1.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] {
            this.xtraTabPage1});
			// 
			// xtraTabPage1
			// 
			this.xtraTabPage1.Controls.Add(this.employeeName);
			this.xtraTabPage1.Controls.Add(this.labelControl3);
			this.xtraTabPage1.Controls.Add(this.labelControl2);
			this.xtraTabPage1.Controls.Add(this.labelControl1);
			this.xtraTabPage1.Controls.Add(this.position);
			this.xtraTabPage1.Controls.Add(this.department);
			this.xtraTabPage1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.xtraTabPage1.Name = "xtraTabPage1";
			this.xtraTabPage1.Size = new System.Drawing.Size(577, 104);
			this.xtraTabPage1.Text = "Employee Information";
			// 
			// employeeName
			// 
			this.employeeName.EditValue = "";
			this.employeeName.Location = new System.Drawing.Point(157, 19);
			this.employeeName.Name = "employeeName";
			this.employeeName.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
			this.employeeName.Properties.DisplayMember = "FullName";
			this.employeeName.Properties.NullText = "";
			this.employeeName.Properties.PopupView = this.searchLookUpEdit1View;
			this.employeeName.Properties.ValueMember = "Id";
			this.employeeName.Size = new System.Drawing.Size(399, 20);
			this.employeeName.TabIndex = 0;
			this.employeeName.EditValueChanged += new System.EventHandler(this.employeeName_EditValueChanged_1);
			// 
			// searchLookUpEdit1View
			// 
			this.searchLookUpEdit1View.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.gridColumn6});
			this.searchLookUpEdit1View.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
			this.searchLookUpEdit1View.Name = "searchLookUpEdit1View";
			this.searchLookUpEdit1View.OptionsSelection.EnableAppearanceFocusedCell = false;
			this.searchLookUpEdit1View.OptionsView.ShowGroupPanel = false;
			// 
			// gridColumn6
			// 
			this.gridColumn6.Caption = "Full Name";
			this.gridColumn6.FieldName = "FullName";
			this.gridColumn6.Name = "gridColumn6";
			this.gridColumn6.Visible = true;
			this.gridColumn6.VisibleIndex = 0;
			// 
			// labelControl3
			// 
			this.labelControl3.Location = new System.Drawing.Point(30, 69);
			this.labelControl3.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.labelControl3.Name = "labelControl3";
			this.labelControl3.Size = new System.Drawing.Size(37, 13);
			this.labelControl3.TabIndex = 2;
			this.labelControl3.Text = "Position";
			// 
			// labelControl2
			// 
			this.labelControl2.Location = new System.Drawing.Point(30, 46);
			this.labelControl2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.labelControl2.Name = "labelControl2";
			this.labelControl2.Size = new System.Drawing.Size(57, 13);
			this.labelControl2.TabIndex = 2;
			this.labelControl2.Text = "Department";
			// 
			// labelControl1
			// 
			this.labelControl1.Location = new System.Drawing.Point(30, 20);
			this.labelControl1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.labelControl1.Name = "labelControl1";
			this.labelControl1.Size = new System.Drawing.Size(46, 13);
			this.labelControl1.TabIndex = 2;
			this.labelControl1.Text = "Employee";
			// 
			// position
			// 
			this.position.EditValue = "";
			this.position.Location = new System.Drawing.Point(157, 67);
			this.position.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.position.Name = "position";
			this.position.Properties.ReadOnly = true;
			this.position.Size = new System.Drawing.Size(399, 20);
			this.position.TabIndex = 1;
			// 
			// department
			// 
			this.department.EditValue = "";
			this.department.Location = new System.Drawing.Point(157, 44);
			this.department.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.department.Name = "department";
			this.department.Properties.ReadOnly = true;
			this.department.Size = new System.Drawing.Size(399, 20);
			this.department.TabIndex = 1;
			// 
			// labelControl4
			// 
			this.labelControl4.Location = new System.Drawing.Point(32, 67);
			this.labelControl4.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.labelControl4.Name = "labelControl4";
			this.labelControl4.Size = new System.Drawing.Size(37, 13);
			this.labelControl4.TabIndex = 2;
			this.labelControl4.Text = "Amount";
			// 
			// Amount
			// 
			this.Amount.EditValue = new decimal(new int[] {
            0,
            0,
            0,
            0});
			this.Amount.Location = new System.Drawing.Point(157, 64);
			this.Amount.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.Amount.Name = "Amount";
			this.Amount.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
			this.Amount.Properties.MaskSettings.Set("mask", "n2");
			this.Amount.Properties.UseMaskAsDisplayFormat = true;
			this.Amount.Size = new System.Drawing.Size(377, 20);
			this.Amount.TabIndex = 1;
			// 
			// xtraTabControl2
			// 
			this.xtraTabControl2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.xtraTabControl2.Location = new System.Drawing.Point(22, 196);
			this.xtraTabControl2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.xtraTabControl2.Name = "xtraTabControl2";
			this.xtraTabControl2.SelectedTabPage = this.xtraTabPage3;
			this.xtraTabControl2.Size = new System.Drawing.Size(578, 210);
			this.xtraTabControl2.TabIndex = 1;
			this.xtraTabControl2.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] {
            this.xtraTabPage3});
			// 
			// xtraTabPage3
			// 
			this.xtraTabPage3.Controls.Add(this.remarks);
			this.xtraTabPage3.Controls.Add(this.period);
			this.xtraTabPage3.Controls.Add(this.labelControl5);
			this.xtraTabPage3.Controls.Add(this.labelControl4);
			this.xtraTabPage3.Controls.Add(this.labelControl10);
			this.xtraTabPage3.Controls.Add(this.Amount);
			this.xtraTabPage3.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.xtraTabPage3.Name = "xtraTabPage3";
			this.xtraTabPage3.Size = new System.Drawing.Size(576, 181);
			this.xtraTabPage3.Text = "Payment Setup";
			// 
			// remarks
			// 
			this.remarks.Location = new System.Drawing.Point(157, 88);
			this.remarks.Name = "remarks";
			this.remarks.Size = new System.Drawing.Size(377, 54);
			this.remarks.TabIndex = 4;
			// 
			// period
			// 
			this.period.EditValue = new System.DateTime(2024, 1, 7, 12, 40, 39, 303);
			this.period.Location = new System.Drawing.Point(157, 40);
			this.period.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.period.Name = "period";
			this.period.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
			this.period.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
			this.period.Properties.DisplayFormat.FormatString = "MMM dd, yyyy";
			this.period.Properties.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
			this.period.Properties.MaskSettings.Set("mask", "MMM dd, yyyy");
			this.period.Size = new System.Drawing.Size(377, 20);
			this.period.TabIndex = 3;
			// 
			// labelControl5
			// 
			this.labelControl5.Location = new System.Drawing.Point(32, 90);
			this.labelControl5.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.labelControl5.Name = "labelControl5";
			this.labelControl5.Size = new System.Drawing.Size(41, 13);
			this.labelControl5.TabIndex = 2;
			this.labelControl5.Text = "Remarks";
			// 
			// labelControl10
			// 
			this.labelControl10.Location = new System.Drawing.Point(32, 43);
			this.labelControl10.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.labelControl10.Name = "labelControl10";
			this.labelControl10.Size = new System.Drawing.Size(75, 13);
			this.labelControl10.TabIndex = 2;
			this.labelControl10.Text = "Apply To Period";
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
			this.barDockControlTop.Size = new System.Drawing.Size(625, 28);
			// 
			// barDockControlBottom
			// 
			this.barDockControlBottom.CausesValidation = false;
			this.barDockControlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.barDockControlBottom.Location = new System.Drawing.Point(0, 432);
			this.barDockControlBottom.Manager = this.barManager1;
			this.barDockControlBottom.Size = new System.Drawing.Size(625, 18);
			// 
			// barDockControlLeft
			// 
			this.barDockControlLeft.CausesValidation = false;
			this.barDockControlLeft.Dock = System.Windows.Forms.DockStyle.Left;
			this.barDockControlLeft.Location = new System.Drawing.Point(0, 28);
			this.barDockControlLeft.Manager = this.barManager1;
			this.barDockControlLeft.Size = new System.Drawing.Size(0, 404);
			// 
			// barDockControlRight
			// 
			this.barDockControlRight.CausesValidation = false;
			this.barDockControlRight.Dock = System.Windows.Forms.DockStyle.Right;
			this.barDockControlRight.Location = new System.Drawing.Point(625, 28);
			this.barDockControlRight.Manager = this.barManager1;
			this.barDockControlRight.Size = new System.Drawing.Size(0, 404);
			// 
			// CreateSalaryAdjustment
			// 
			this.AccessibleDescription = "Salary Adjustment";
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(625, 450);
			this.Controls.Add(this.xtraTabControl2);
			this.Controls.Add(this.xtraTabControl1);
			this.Controls.Add(this.barDockControlLeft);
			this.Controls.Add(this.barDockControlRight);
			this.Controls.Add(this.barDockControlBottom);
			this.Controls.Add(this.barDockControlTop);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
			this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.Name = "CreateSalaryAdjustment";
			this.Text = "Salary Adjustment";
			((System.ComponentModel.ISupportInitialize)(this.xtraTabControl1)).EndInit();
			this.xtraTabControl1.ResumeLayout(false);
			this.xtraTabPage1.ResumeLayout(false);
			this.xtraTabPage1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.employeeName.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.searchLookUpEdit1View)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.position.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.department.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.Amount.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.xtraTabControl2)).EndInit();
			this.xtraTabControl2.ResumeLayout(false);
			this.xtraTabPage3.ResumeLayout(false);
			this.xtraTabPage3.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.remarks.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.period.Properties.CalendarTimeProperties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.period.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.barManager1)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

        }

        #endregion

        private DevExpress.XtraTab.XtraTabControl xtraTabControl1;
        private DevExpress.XtraTab.XtraTabPage xtraTabPage1;
        private DevExpress.XtraTab.XtraTabControl xtraTabControl2;
        private DevExpress.XtraTab.XtraTabPage xtraTabPage3;
        private DevExpress.XtraEditors.LabelControl labelControl3;
        private DevExpress.XtraEditors.LabelControl labelControl2;
        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraEditors.TextEdit position;
        private DevExpress.XtraEditors.TextEdit department;
        private DevExpress.XtraEditors.LabelControl labelControl4;
        private DevExpress.XtraEditors.SpinEdit Amount;
        private DevExpress.XtraEditors.DateEdit period;
        private DevExpress.XtraEditors.LabelControl labelControl10;
        private DevExpress.XtraEditors.SearchLookUpEdit employeeName;
        private DevExpress.XtraGrid.Views.Grid.GridView searchLookUpEdit1View;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn6;
        private DevExpress.XtraEditors.MemoEdit remarks;
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
	}
}