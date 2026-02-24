namespace Buzlink.HR.UI
{
    partial class CreateLeave
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
            components = new Container();
            ComponentResourceManager resources = new ComponentResourceManager(typeof(CreateLeave));
            labelControl1 = new DevExpress.XtraEditors.LabelControl();
            labelControl2 = new DevExpress.XtraEditors.LabelControl();
            description = new DevExpress.XtraEditors.TextEdit();
            panelControl1 = new DevExpress.XtraEditors.PanelControl();
            statustoggle = new DevExpress.XtraEditors.ToggleSwitch();
            barManager1 = new DevExpress.XtraBars.BarManager(components);
            bar1 = new DevExpress.XtraBars.Bar();
            barButtonItem2 = new DevExpress.XtraBars.BarButtonItem();
            barButtonItem1 = new DevExpress.XtraBars.BarButtonItem();
            bar3 = new DevExpress.XtraBars.Bar();
            barDockControlTop = new DevExpress.XtraBars.BarDockControl();
            barDockControlBottom = new DevExpress.XtraBars.BarDockControl();
            barDockControlLeft = new DevExpress.XtraBars.BarDockControl();
            barDockControlRight = new DevExpress.XtraBars.BarDockControl();
            IsWithPay = new DevExpress.XtraEditors.CheckEdit();
            credits = new DevExpress.XtraEditors.SpinEdit();
            remarks = new DevExpress.XtraEditors.MemoEdit();
            labelControl3 = new DevExpress.XtraEditors.LabelControl();
            toolTip1 = new ToolTip(components);
            ((ISupportInitialize)description.Properties).BeginInit();
            ((ISupportInitialize)panelControl1).BeginInit();
            panelControl1.SuspendLayout();
            ((ISupportInitialize)statustoggle.Properties).BeginInit();
            ((ISupportInitialize)barManager1).BeginInit();
            ((ISupportInitialize)IsWithPay.Properties).BeginInit();
            ((ISupportInitialize)credits.Properties).BeginInit();
            ((ISupportInitialize)remarks.Properties).BeginInit();
            SuspendLayout();
            // 
            // labelControl1
            // 
            labelControl1.Location = new Point(39, 66);
            labelControl1.Margin = new Padding(4);
            labelControl1.Name = "labelControl1";
            labelControl1.Size = new Size(41, 19);
            labelControl1.TabIndex = 0;
            labelControl1.Text = "Name";
            // 
            // labelControl2
            // 
            labelControl2.Location = new Point(39, 102);
            labelControl2.Margin = new Padding(4);
            labelControl2.Name = "labelControl2";
            labelControl2.Size = new Size(61, 19);
            labelControl2.TabIndex = 0;
            labelControl2.Text = "Remarks";
            // 
            // description
            // 
            description.Location = new Point(164, 61);
            description.Margin = new Padding(4);
            description.Name = "description";
            description.Size = new Size(596, 26);
            description.TabIndex = 1;
            // 
            // panelControl1
            // 
            panelControl1.Controls.Add(statustoggle);
            panelControl1.Controls.Add(IsWithPay);
            panelControl1.Controls.Add(credits);
            panelControl1.Controls.Add(remarks);
            panelControl1.Controls.Add(description);
            panelControl1.Controls.Add(labelControl1);
            panelControl1.Controls.Add(labelControl3);
            panelControl1.Controls.Add(labelControl2);
            panelControl1.Location = new Point(96, 102);
            panelControl1.Margin = new Padding(4);
            panelControl1.Name = "panelControl1";
            panelControl1.Size = new Size(798, 364);
            panelControl1.TabIndex = 2;
            // 
            // statustoggle
            // 
            statustoggle.EditValue = true;
            statustoggle.Location = new Point(164, 254);
            statustoggle.Margin = new Padding(4);
            statustoggle.MenuManager = barManager1;
            statustoggle.Name = "statustoggle";
            statustoggle.Properties.OffText = "Deactivated";
            statustoggle.Properties.OnText = "Active";
            statustoggle.Size = new Size(306, 26);
            statustoggle.TabIndex = 6;
            // 
            // barManager1
            // 
            barManager1.Bars.AddRange(new DevExpress.XtraBars.Bar[] { bar1, bar3 });
            barManager1.DockControls.Add(barDockControlTop);
            barManager1.DockControls.Add(barDockControlBottom);
            barManager1.DockControls.Add(barDockControlLeft);
            barManager1.DockControls.Add(barDockControlRight);
            barManager1.Form = this;
            barManager1.Items.AddRange(new DevExpress.XtraBars.BarItem[] { barButtonItem1, barButtonItem2 });
            barManager1.MaxItemId = 2;
            barManager1.StatusBar = bar3;
            // 
            // bar1
            // 
            bar1.BarName = "Tools";
            bar1.DockCol = 0;
            bar1.DockRow = 0;
            bar1.DockStyle = DevExpress.XtraBars.BarDockStyle.Top;
            bar1.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[] { new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, barButtonItem2, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph), new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, barButtonItem1, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph) });
            bar1.Text = "Tools";
            // 
            // barButtonItem2
            // 
            barButtonItem2.Caption = "Clear";
            barButtonItem2.Id = 1;
            barButtonItem2.ImageOptions.Image = (Image)resources.GetObject("barButtonItem2.ImageOptions.Image");
            barButtonItem2.ImageOptions.LargeImage = (Image)resources.GetObject("barButtonItem2.ImageOptions.LargeImage");
            barButtonItem2.Name = "barButtonItem2";
            barButtonItem2.ItemClick += barButtonItem2_ItemClick;
            // 
            // barButtonItem1
            // 
            barButtonItem1.Caption = "Save";
            barButtonItem1.Id = 0;
            barButtonItem1.ImageOptions.Image = (Image)resources.GetObject("barButtonItem1.ImageOptions.Image");
            barButtonItem1.ImageOptions.LargeImage = (Image)resources.GetObject("barButtonItem1.ImageOptions.LargeImage");
            barButtonItem1.Name = "barButtonItem1";
            barButtonItem1.ItemClick += barButtonItem1_ItemClick;
            // 
            // bar3
            // 
            bar3.BarName = "Status bar";
            bar3.CanDockStyle = DevExpress.XtraBars.BarCanDockStyle.Bottom;
            bar3.DockCol = 0;
            bar3.DockRow = 0;
            bar3.DockStyle = DevExpress.XtraBars.BarDockStyle.Bottom;
            bar3.OptionsBar.AllowQuickCustomization = false;
            bar3.OptionsBar.DrawDragBorder = false;
            bar3.OptionsBar.UseWholeRow = true;
            bar3.Text = "Status bar";
            // 
            // barDockControlTop
            // 
            barDockControlTop.CausesValidation = false;
            barDockControlTop.Dock = DockStyle.Top;
            barDockControlTop.Location = new Point(0, 0);
            barDockControlTop.Manager = barManager1;
            barDockControlTop.Margin = new Padding(4);
            barDockControlTop.Size = new Size(993, 40);
            // 
            // barDockControlBottom
            // 
            barDockControlBottom.CausesValidation = false;
            barDockControlBottom.Dock = DockStyle.Bottom;
            barDockControlBottom.Location = new Point(0, 537);
            barDockControlBottom.Manager = barManager1;
            barDockControlBottom.Margin = new Padding(4);
            barDockControlBottom.Size = new Size(993, 18);
            // 
            // barDockControlLeft
            // 
            barDockControlLeft.CausesValidation = false;
            barDockControlLeft.Dock = DockStyle.Left;
            barDockControlLeft.Location = new Point(0, 40);
            barDockControlLeft.Manager = barManager1;
            barDockControlLeft.Margin = new Padding(4);
            barDockControlLeft.Size = new Size(0, 497);
            // 
            // barDockControlRight
            // 
            barDockControlRight.CausesValidation = false;
            barDockControlRight.Dock = DockStyle.Right;
            barDockControlRight.Location = new Point(993, 40);
            barDockControlRight.Manager = barManager1;
            barDockControlRight.Margin = new Padding(4);
            barDockControlRight.Size = new Size(0, 497);
            // 
            // IsWithPay
            // 
            IsWithPay.EditValue = true;
            IsWithPay.Location = new Point(164, 213);
            IsWithPay.Margin = new Padding(4);
            IsWithPay.Name = "IsWithPay";
            IsWithPay.Properties.Caption = "With Pay";
            IsWithPay.Properties.CheckBoxOptions.Style = DevExpress.XtraEditors.Controls.CheckBoxStyle.SvgToggle1;
            IsWithPay.Size = new Size(306, 31);
            IsWithPay.TabIndex = 4;
            // 
            // credits
            // 
            credits.EditValue = new decimal(new int[] { 0, 0, 0, 0 });
            credits.Location = new Point(164, 175);
            credits.Margin = new Padding(4);
            credits.Name = "credits";
            credits.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] { new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo) });
            credits.Size = new Size(306, 28);
            credits.TabIndex = 3;
            // 
            // remarks
            // 
            remarks.Location = new Point(164, 99);
            remarks.Margin = new Padding(4);
            remarks.Name = "remarks";
            remarks.Size = new Size(596, 67);
            remarks.TabIndex = 2;
            // 
            // labelControl3
            // 
            labelControl3.Location = new Point(39, 180);
            labelControl3.Margin = new Padding(4);
            labelControl3.Name = "labelControl3";
            labelControl3.Size = new Size(100, 19);
            labelControl3.TabIndex = 0;
            labelControl3.Text = "Credits (Days)";
            // 
            // CreateLeave
            // 
            AccessibleDescription = "Leave Setup";
            AutoScaleDimensions = new SizeF(9F, 19F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(993, 555);
            Controls.Add(panelControl1);
            Controls.Add(barDockControlLeft);
            Controls.Add(barDockControlRight);
            Controls.Add(barDockControlBottom);
            Controls.Add(barDockControlTop);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Margin = new Padding(4);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "CreateLeave";
            Text = "Leave Setup";
            ((ISupportInitialize)description.Properties).EndInit();
            ((ISupportInitialize)panelControl1).EndInit();
            panelControl1.ResumeLayout(false);
            panelControl1.PerformLayout();
            ((ISupportInitialize)statustoggle.Properties).EndInit();
            ((ISupportInitialize)barManager1).EndInit();
            ((ISupportInitialize)IsWithPay.Properties).EndInit();
            ((ISupportInitialize)credits.Properties).EndInit();
            ((ISupportInitialize)remarks.Properties).EndInit();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion
        private DevExpress.XtraEditors.TextEdit description;
        private DevExpress.XtraEditors.LabelControl labelControl2;
        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraEditors.PanelControl panelControl1;
        private System.Windows.Forms.ToolTip toolTip1;
        private DevExpress.XtraEditors.MemoEdit remarks;
        private DevExpress.XtraEditors.SpinEdit credits;
        private DevExpress.XtraEditors.LabelControl labelControl3;
        private DevExpress.XtraEditors.CheckEdit IsWithPay;
		private DevExpress.XtraBars.BarManager barManager1;
		private DevExpress.XtraBars.Bar bar1;
		private DevExpress.XtraBars.BarButtonItem barButtonItem2;
		private DevExpress.XtraBars.BarButtonItem barButtonItem1;
		private DevExpress.XtraBars.Bar bar3;
		private DevExpress.XtraBars.BarDockControl barDockControlTop;
		private DevExpress.XtraBars.BarDockControl barDockControlBottom;
		private DevExpress.XtraBars.BarDockControl barDockControlLeft;
		private DevExpress.XtraBars.BarDockControl barDockControlRight;
		private DevExpress.XtraEditors.ToggleSwitch statustoggle;
	}
}