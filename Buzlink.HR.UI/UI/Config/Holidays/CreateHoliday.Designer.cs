namespace Buzlink.HR.UI
{
    partial class CreateHoliday
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CreateHoliday));
			this.isspecial = new DevExpress.XtraEditors.CheckEdit();
			this.holdate = new DevExpress.XtraEditors.DateEdit();
			this.islegal = new DevExpress.XtraEditors.CheckEdit();
			this.holname = new DevExpress.XtraEditors.TextEdit();
			this.labelControl1 = new DevExpress.XtraEditors.LabelControl();
			this.labelControl3 = new DevExpress.XtraEditors.LabelControl();
			this.labelControl4 = new DevExpress.XtraEditors.LabelControl();
			this.panelControl1 = new DevExpress.XtraEditors.PanelControl();
			this.recuring = new DevExpress.XtraEditors.CheckEdit();
			this.barManager1 = new DevExpress.XtraBars.BarManager(this.components);
			this.bar1 = new DevExpress.XtraBars.Bar();
			this.barButtonItem2 = new DevExpress.XtraBars.BarButtonItem();
			this.barButtonItem1 = new DevExpress.XtraBars.BarButtonItem();
			this.bar3 = new DevExpress.XtraBars.Bar();
			this.barDockControlTop = new DevExpress.XtraBars.BarDockControl();
			this.barDockControlBottom = new DevExpress.XtraBars.BarDockControl();
			this.barDockControlLeft = new DevExpress.XtraBars.BarDockControl();
			this.barDockControlRight = new DevExpress.XtraBars.BarDockControl();
			((System.ComponentModel.ISupportInitialize)(this.isspecial.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.holdate.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.holdate.Properties.CalendarTimeProperties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.islegal.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.holname.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.panelControl1)).BeginInit();
			this.panelControl1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.recuring.Properties)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.barManager1)).BeginInit();
			this.SuspendLayout();
			// 
			// isspecial
			// 
			this.isspecial.Location = new System.Drawing.Point(246, 64);
			this.isspecial.Name = "isspecial";
			this.isspecial.Properties.Caption = "Special";
			this.isspecial.Properties.CheckStyle = DevExpress.XtraEditors.Controls.CheckStyles.Radio;
			this.isspecial.Properties.RadioGroupIndex = 0;
			this.isspecial.Size = new System.Drawing.Size(75, 20);
			this.isspecial.TabIndex = 4;
			this.isspecial.TabStop = false;
			// 
			// holdate
			// 
			this.holdate.EditValue = new System.DateTime(2021, 9, 18, 16, 26, 3, 0);
			this.holdate.Location = new System.Drawing.Point(140, 90);
			this.holdate.Name = "holdate";
			this.holdate.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
			this.holdate.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
			this.holdate.Properties.DisplayFormat.FormatString = "MMM dd, yyyy";
			this.holdate.Properties.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
			this.holdate.Properties.EditFormat.FormatString = "MMM dd, yyyy";
			this.holdate.Properties.EditFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
			this.holdate.Properties.MaskSettings.Set("mask", "MMM dd, yyyy");
			this.holdate.Properties.UseMaskAsDisplayFormat = true;
			this.holdate.Size = new System.Drawing.Size(268, 20);
			this.holdate.TabIndex = 5;
			// 
			// islegal
			// 
			this.islegal.Location = new System.Drawing.Point(140, 64);
			this.islegal.Name = "islegal";
			this.islegal.Properties.Caption = "Legal";
			this.islegal.Properties.CheckStyle = DevExpress.XtraEditors.Controls.CheckStyles.Radio;
			this.islegal.Properties.RadioGroupIndex = 0;
			this.islegal.Size = new System.Drawing.Size(87, 20);
			this.islegal.TabIndex = 4;
			this.islegal.TabStop = false;
			// 
			// holname
			// 
			this.holname.EditValue = "";
			this.holname.Location = new System.Drawing.Point(140, 38);
			this.holname.Name = "holname";
			this.holname.Size = new System.Drawing.Size(268, 20);
			this.holname.TabIndex = 1;
			// 
			// labelControl1
			// 
			this.labelControl1.Location = new System.Drawing.Point(53, 67);
			this.labelControl1.Name = "labelControl1";
			this.labelControl1.Size = new System.Drawing.Size(24, 13);
			this.labelControl1.TabIndex = 0;
			this.labelControl1.Text = "Type";
			// 
			// labelControl3
			// 
			this.labelControl3.Location = new System.Drawing.Point(53, 43);
			this.labelControl3.Name = "labelControl3";
			this.labelControl3.Size = new System.Drawing.Size(65, 13);
			this.labelControl3.TabIndex = 0;
			this.labelControl3.Text = "Holiday Name";
			// 
			// labelControl4
			// 
			this.labelControl4.Location = new System.Drawing.Point(53, 93);
			this.labelControl4.Name = "labelControl4";
			this.labelControl4.Size = new System.Drawing.Size(23, 13);
			this.labelControl4.TabIndex = 0;
			this.labelControl4.Text = "Date";
			// 
			// panelControl1
			// 
			this.panelControl1.Controls.Add(this.recuring);
			this.panelControl1.Controls.Add(this.isspecial);
			this.panelControl1.Controls.Add(this.holdate);
			this.panelControl1.Controls.Add(this.islegal);
			this.panelControl1.Controls.Add(this.holname);
			this.panelControl1.Controls.Add(this.labelControl4);
			this.panelControl1.Controls.Add(this.labelControl1);
			this.panelControl1.Controls.Add(this.labelControl3);
			this.panelControl1.Location = new System.Drawing.Point(60, 71);
			this.panelControl1.Name = "panelControl1";
			this.panelControl1.Size = new System.Drawing.Size(473, 196);
			this.panelControl1.TabIndex = 6;
			// 
			// recuring
			// 
			this.recuring.Location = new System.Drawing.Point(140, 119);
			this.recuring.Name = "recuring";
			this.recuring.Properties.Caption = "Recuring";
			this.recuring.Size = new System.Drawing.Size(102, 18);
			this.recuring.TabIndex = 6;
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
			this.barDockControlTop.Size = new System.Drawing.Size(598, 27);
			// 
			// barDockControlBottom
			// 
			this.barDockControlBottom.CausesValidation = false;
			this.barDockControlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.barDockControlBottom.Location = new System.Drawing.Point(0, 299);
			this.barDockControlBottom.Manager = this.barManager1;
			this.barDockControlBottom.Size = new System.Drawing.Size(598, 19);
			// 
			// barDockControlLeft
			// 
			this.barDockControlLeft.CausesValidation = false;
			this.barDockControlLeft.Dock = System.Windows.Forms.DockStyle.Left;
			this.barDockControlLeft.Location = new System.Drawing.Point(0, 27);
			this.barDockControlLeft.Manager = this.barManager1;
			this.barDockControlLeft.Size = new System.Drawing.Size(0, 272);
			// 
			// barDockControlRight
			// 
			this.barDockControlRight.CausesValidation = false;
			this.barDockControlRight.Dock = System.Windows.Forms.DockStyle.Right;
			this.barDockControlRight.Location = new System.Drawing.Point(598, 27);
			this.barDockControlRight.Manager = this.barManager1;
			this.barDockControlRight.Size = new System.Drawing.Size(0, 272);
			// 
			// CreateHoliday
			// 
			this.AccessibleDescription = "Holiday Setup";
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(598, 318);
			this.Controls.Add(this.panelControl1);
			this.Controls.Add(this.barDockControlLeft);
			this.Controls.Add(this.barDockControlRight);
			this.Controls.Add(this.barDockControlBottom);
			this.Controls.Add(this.barDockControlTop);
			this.Name = "CreateHoliday";
			this.Text = "Holiday";
			((System.ComponentModel.ISupportInitialize)(this.isspecial.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.holdate.Properties.CalendarTimeProperties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.holdate.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.islegal.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.holname.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.panelControl1)).EndInit();
			this.panelControl1.ResumeLayout(false);
			this.panelControl1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.recuring.Properties)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.barManager1)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

        }

        #endregion
        private DevExpress.XtraEditors.CheckEdit isspecial;
        private DevExpress.XtraEditors.DateEdit holdate;
        private DevExpress.XtraEditors.CheckEdit islegal;
        private DevExpress.XtraEditors.TextEdit holname;
        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraEditors.LabelControl labelControl3;
        private DevExpress.XtraEditors.LabelControl labelControl4;
        private DevExpress.XtraEditors.PanelControl panelControl1;
        private DevExpress.XtraEditors.CheckEdit recuring;
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