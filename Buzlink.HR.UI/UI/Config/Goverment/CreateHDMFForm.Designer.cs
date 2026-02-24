
namespace Buzlink.HR.UI.UI.Config.Goverment
{
    partial class CreateHDMFForm
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
            ComponentResourceManager resources = new ComponentResourceManager(typeof(CreateHDMFForm));
            DevExpress.XtraEditors.Controls.EditorButtonImageOptions editorButtonImageOptions2 = new DevExpress.XtraEditors.Controls.EditorButtonImageOptions();
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject5 = new DevExpress.Utils.SerializableAppearanceObject();
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject6 = new DevExpress.Utils.SerializableAppearanceObject();
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject7 = new DevExpress.Utils.SerializableAppearanceObject();
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject8 = new DevExpress.Utils.SerializableAppearanceObject();
            barManager1 = new DevExpress.XtraBars.BarManager(components);
            bar1 = new DevExpress.XtraBars.Bar();
            barButtonItem1 = new DevExpress.XtraBars.BarButtonItem();
            barButtonItem2 = new DevExpress.XtraBars.BarButtonItem();
            bar2 = new DevExpress.XtraBars.Bar();
            bar3 = new DevExpress.XtraBars.Bar();
            barDockControlTop = new DevExpress.XtraBars.BarDockControl();
            barDockControlBottom = new DevExpress.XtraBars.BarDockControl();
            barDockControlLeft = new DevExpress.XtraBars.BarDockControl();
            barDockControlRight = new DevExpress.XtraBars.BarDockControl();
            gridControl1 = new DevExpress.XtraGrid.GridControl();
            advBandedGridView1 = new DevExpress.XtraGrid.Views.BandedGrid.AdvBandedGridView();
            gridBand1 = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
            gridColumn1 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            gridColumn2 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            gridBand2 = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
            gridColumn3 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            gridColumn4 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            gridBand3 = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
            bandedGridColumn1 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            gridBand4 = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
            bandedGridColumn2 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            repox = new DevExpress.XtraEditors.Repository.RepositoryItemButtonEdit();
            bandedGridColumn3 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            ((ISupportInitialize)barManager1).BeginInit();
            ((ISupportInitialize)gridControl1).BeginInit();
            ((ISupportInitialize)advBandedGridView1).BeginInit();
            ((ISupportInitialize)repox).BeginInit();
            SuspendLayout();
            // 
            // barManager1
            // 
            barManager1.Bars.AddRange(new DevExpress.XtraBars.Bar[] { bar1, bar2, bar3 });
            barManager1.DockControls.Add(barDockControlTop);
            barManager1.DockControls.Add(barDockControlBottom);
            barManager1.DockControls.Add(barDockControlLeft);
            barManager1.DockControls.Add(barDockControlRight);
            barManager1.Form = this;
            barManager1.Items.AddRange(new DevExpress.XtraBars.BarItem[] { barButtonItem1, barButtonItem2 });
            barManager1.MainMenu = bar2;
            barManager1.MaxItemId = 2;
            barManager1.StatusBar = bar3;
            // 
            // bar1
            // 
            bar1.BarName = "Tools";
            bar1.DockCol = 0;
            bar1.DockRow = 1;
            bar1.DockStyle = DevExpress.XtraBars.BarDockStyle.Top;
            bar1.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[] { new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, barButtonItem1, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph), new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, barButtonItem2, "", true, true, true, 0, null, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph) });
            bar1.Text = "Tools";
            // 
            // barButtonItem1
            // 
            barButtonItem1.Caption = "Save";
            barButtonItem1.Id = 0;
            barButtonItem1.ImageOptions.SvgImage = (DevExpress.Utils.Svg.SvgImage)resources.GetObject("barButtonItem1.ImageOptions.SvgImage");
            barButtonItem1.Name = "barButtonItem1";
            barButtonItem1.ItemClick += barButtonItem1_ItemClick;
            // 
            // barButtonItem2
            // 
            barButtonItem2.Caption = "Export";
            barButtonItem2.Id = 1;
            barButtonItem2.ImageOptions.Image = Properties.Resources.exporttoxlsx_16x16;
            barButtonItem2.ImageOptions.LargeImage = Properties.Resources.exporttoxlsx_32x32;
            barButtonItem2.Name = "barButtonItem2";
            barButtonItem2.ItemClick += barButtonItem2_ItemClick;
            // 
            // bar2
            // 
            bar2.BarName = "Main menu";
            bar2.DockCol = 0;
            bar2.DockRow = 0;
            bar2.DockStyle = DevExpress.XtraBars.BarDockStyle.Top;
            bar2.OptionsBar.MultiLine = true;
            bar2.OptionsBar.UseWholeRow = true;
            bar2.Text = "Main menu";
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
            barDockControlTop.Margin = new Padding(3, 4, 3, 4);
            barDockControlTop.Size = new Size(1449, 60);
            // 
            // barDockControlBottom
            // 
            barDockControlBottom.CausesValidation = false;
            barDockControlBottom.Dock = DockStyle.Bottom;
            barDockControlBottom.Location = new Point(0, 665);
            barDockControlBottom.Manager = barManager1;
            barDockControlBottom.Margin = new Padding(3, 4, 3, 4);
            barDockControlBottom.Size = new Size(1449, 18);
            // 
            // barDockControlLeft
            // 
            barDockControlLeft.CausesValidation = false;
            barDockControlLeft.Dock = DockStyle.Left;
            barDockControlLeft.Location = new Point(0, 60);
            barDockControlLeft.Manager = barManager1;
            barDockControlLeft.Margin = new Padding(3, 4, 3, 4);
            barDockControlLeft.Size = new Size(0, 605);
            // 
            // barDockControlRight
            // 
            barDockControlRight.CausesValidation = false;
            barDockControlRight.Dock = DockStyle.Right;
            barDockControlRight.Location = new Point(1449, 60);
            barDockControlRight.Manager = barManager1;
            barDockControlRight.Margin = new Padding(3, 4, 3, 4);
            barDockControlRight.Size = new Size(0, 605);
            // 
            // gridControl1
            // 
            gridControl1.Dock = DockStyle.Fill;
            gridControl1.EmbeddedNavigator.Margin = new Padding(4);
            gridControl1.Location = new Point(0, 60);
            gridControl1.MainView = advBandedGridView1;
            gridControl1.Margin = new Padding(4);
            gridControl1.MenuManager = barManager1;
            gridControl1.Name = "gridControl1";
            gridControl1.RepositoryItems.AddRange(new DevExpress.XtraEditors.Repository.RepositoryItem[] { repox });
            gridControl1.Size = new Size(1449, 605);
            gridControl1.TabIndex = 4;
            gridControl1.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { advBandedGridView1 });
            gridControl1.Click += gridControl1_Click;
            // 
            // advBandedGridView1
            // 
            advBandedGridView1.Bands.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.GridBand[] { gridBand1, gridBand2, gridBand3, gridBand4 });
            advBandedGridView1.Columns.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn[] { bandedGridColumn3, gridColumn1, gridColumn2, gridColumn3, gridColumn4, bandedGridColumn1, bandedGridColumn2 });
            advBandedGridView1.DetailHeight = 512;
            advBandedGridView1.GridControl = gridControl1;
            advBandedGridView1.Name = "advBandedGridView1";
            advBandedGridView1.OptionsEditForm.PopupEditFormWidth = 1200;
            advBandedGridView1.OptionsView.NewItemRowPosition = DevExpress.XtraGrid.Views.Grid.NewItemRowPosition.Bottom;
            advBandedGridView1.OptionsView.ShowGroupPanel = false;
            // 
            // gridBand1
            // 
            gridBand1.AppearanceHeader.Options.UseTextOptions = true;
            gridBand1.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            gridBand1.Caption = "Mothly Salary";
            gridBand1.Columns.Add(gridColumn1);
            gridBand1.Columns.Add(gridColumn2);
            gridBand1.MinWidth = 15;
            gridBand1.Name = "gridBand1";
            gridBand1.VisibleIndex = 0;
            gridBand1.Width = 426;
            // 
            // gridColumn1
            // 
            gridColumn1.AppearanceHeader.Options.UseTextOptions = true;
            gridColumn1.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            gridColumn1.Caption = "From";
            gridColumn1.FieldName = "RangeFrom";
            gridColumn1.MinWidth = 30;
            gridColumn1.Name = "gridColumn1";
            gridColumn1.Visible = true;
            gridColumn1.Width = 245;
            // 
            // gridColumn2
            // 
            gridColumn2.AppearanceHeader.Options.UseTextOptions = true;
            gridColumn2.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            gridColumn2.Caption = "To";
            gridColumn2.FieldName = "RangeTo";
            gridColumn2.MinWidth = 30;
            gridColumn2.Name = "gridColumn2";
            gridColumn2.Visible = true;
            gridColumn2.Width = 181;
            // 
            // gridBand2
            // 
            gridBand2.AppearanceHeader.Options.UseTextOptions = true;
            gridBand2.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            gridBand2.Caption = "Rate";
            gridBand2.Columns.Add(gridColumn3);
            gridBand2.Columns.Add(gridColumn4);
            gridBand2.MinWidth = 15;
            gridBand2.Name = "gridBand2";
            gridBand2.VisibleIndex = 1;
            gridBand2.Width = 557;
            // 
            // gridColumn3
            // 
            gridColumn3.AppearanceHeader.Options.UseTextOptions = true;
            gridColumn3.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            gridColumn3.Caption = "Employee";
            gridColumn3.DisplayFormat.FormatString = "{0:p1}";
            gridColumn3.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            gridColumn3.FieldName = "EERate";
            gridColumn3.MinWidth = 30;
            gridColumn3.Name = "gridColumn3";
            gridColumn3.Visible = true;
            gridColumn3.Width = 257;
            // 
            // gridColumn4
            // 
            gridColumn4.AppearanceHeader.Options.UseTextOptions = true;
            gridColumn4.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            gridColumn4.Caption = "Employer";
            gridColumn4.DisplayFormat.FormatString = "{0:p1}";
            gridColumn4.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            gridColumn4.FieldName = "ERRate";
            gridColumn4.MinWidth = 30;
            gridColumn4.Name = "gridColumn4";
            gridColumn4.Visible = true;
            gridColumn4.Width = 300;
            // 
            // gridBand3
            // 
            gridBand3.AppearanceHeader.Options.UseTextOptions = true;
            gridBand3.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            gridBand3.Caption = "Total";
            gridBand3.Columns.Add(bandedGridColumn1);
            gridBand3.MinWidth = 15;
            gridBand3.Name = "gridBand3";
            gridBand3.VisibleIndex = 2;
            gridBand3.Width = 174;
            // 
            // bandedGridColumn1
            // 
            bandedGridColumn1.AppearanceHeader.Options.UseTextOptions = true;
            bandedGridColumn1.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            bandedGridColumn1.Caption = "Total";
            bandedGridColumn1.DisplayFormat.FormatString = "{0:p1}";
            bandedGridColumn1.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            bandedGridColumn1.FieldName = "Total";
            bandedGridColumn1.MinWidth = 30;
            bandedGridColumn1.Name = "bandedGridColumn1";
            bandedGridColumn1.OptionsColumn.AllowEdit = false;
            bandedGridColumn1.OptionsColumn.ReadOnly = true;
            bandedGridColumn1.Visible = true;
            bandedGridColumn1.Width = 174;
            // 
            // gridBand4
            // 
            gridBand4.Columns.Add(bandedGridColumn2);
            gridBand4.MinWidth = 15;
            gridBand4.Name = "gridBand4";
            gridBand4.VisibleIndex = 3;
            gridBand4.Width = 62;
            // 
            // bandedGridColumn2
            // 
            bandedGridColumn2.AppearanceHeader.Options.UseTextOptions = true;
            bandedGridColumn2.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            bandedGridColumn2.Caption = "x";
            bandedGridColumn2.ColumnEdit = repox;
            bandedGridColumn2.MinWidth = 30;
            bandedGridColumn2.Name = "bandedGridColumn2";
            bandedGridColumn2.Visible = true;
            bandedGridColumn2.Width = 62;
            // 
            // repox
            // 
            repox.AutoHeight = false;
            repox.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] { new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Clear, "x", -1, true, true, false, editorButtonImageOptions2, new DevExpress.Utils.KeyShortcut(Keys.None), serializableAppearanceObject5, serializableAppearanceObject6, serializableAppearanceObject7, serializableAppearanceObject8, "", null, null, DevExpress.Utils.ToolTipAnchor.Default) });
            repox.Name = "repox";
            repox.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.HideTextEditor;
            // 
            // bandedGridColumn3
            // 
            bandedGridColumn3.Caption = "Effectivity";
            bandedGridColumn3.FieldName = "Effectivity";
            bandedGridColumn3.MinWidth = 30;
            bandedGridColumn3.Name = "bandedGridColumn3";
            bandedGridColumn3.Visible = true;
            bandedGridColumn3.Width = 112;
            // 
            // CreateHDMF
            // 
            AutoScaleDimensions = new SizeF(9F, 19F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1449, 683);
            Controls.Add(gridControl1);
            Controls.Add(barDockControlLeft);
            Controls.Add(barDockControlRight);
            Controls.Add(barDockControlBottom);
            Controls.Add(barDockControlTop);
            Margin = new Padding(4);
            Name = "CreateHDMF";
            Text = "HDMF Contributation Schedule";
            Load += CreateHDMF_Load;
            ((ISupportInitialize)barManager1).EndInit();
            ((ISupportInitialize)gridControl1).EndInit();
            ((ISupportInitialize)advBandedGridView1).EndInit();
            ((ISupportInitialize)repox).EndInit();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private DevExpress.XtraBars.BarManager barManager1;
        private DevExpress.XtraBars.Bar bar1;
        private DevExpress.XtraBars.BarButtonItem barButtonItem1;
        private DevExpress.XtraBars.BarButtonItem barButtonItem2;
        private DevExpress.XtraBars.Bar bar2;
        private DevExpress.XtraBars.Bar bar3;
        private DevExpress.XtraBars.BarDockControl barDockControlTop;
        private DevExpress.XtraBars.BarDockControl barDockControlBottom;
        private DevExpress.XtraBars.BarDockControl barDockControlLeft;
        private DevExpress.XtraBars.BarDockControl barDockControlRight;
        private DevExpress.XtraGrid.GridControl gridControl1;
        private DevExpress.XtraGrid.Views.BandedGrid.AdvBandedGridView advBandedGridView1;
        private DevExpress.XtraGrid.Views.BandedGrid.GridBand gridBand1;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn gridColumn1;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn gridColumn2;
        private DevExpress.XtraGrid.Views.BandedGrid.GridBand gridBand2;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn gridColumn3;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn gridColumn4;
        private DevExpress.XtraGrid.Views.BandedGrid.GridBand gridBand3;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn1;
        private DevExpress.XtraGrid.Views.BandedGrid.GridBand gridBand4;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn2;
        private DevExpress.XtraEditors.Repository.RepositoryItemButtonEdit repox;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn3;
    }
}