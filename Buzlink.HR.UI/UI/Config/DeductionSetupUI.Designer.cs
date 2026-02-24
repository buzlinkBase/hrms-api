namespace Buzlink.HR.UI
{
    partial class DeductionSetupUI
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
            DevExpress.XtraEditors.Controls.EditorButtonImageOptions editorButtonImageOptions2 = new DevExpress.XtraEditors.Controls.EditorButtonImageOptions();
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject5 = new DevExpress.Utils.SerializableAppearanceObject();
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject6 = new DevExpress.Utils.SerializableAppearanceObject();
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject7 = new DevExpress.Utils.SerializableAppearanceObject();
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject8 = new DevExpress.Utils.SerializableAppearanceObject();
            ComponentResourceManager resources = new ComponentResourceManager(typeof(DeductionSetupUI));
            labelControl1 = new DevExpress.XtraEditors.LabelControl();
            xtraTabControl2 = new DevExpress.XtraTab.XtraTabControl();
            xtraTabPage3 = new DevExpress.XtraTab.XtraTabPage();
            gridControl1 = new DevExpress.XtraGrid.GridControl();
            gridView1 = new DevExpress.XtraGrid.Views.Grid.GridView();
            gridColumn1 = new DevExpress.XtraGrid.Columns.GridColumn();
            gridColumn3 = new DevExpress.XtraGrid.Columns.GridColumn();
            gridColumn2 = new DevExpress.XtraGrid.Columns.GridColumn();
            gridColumn5 = new DevExpress.XtraGrid.Columns.GridColumn();
            gridColumn6 = new DevExpress.XtraGrid.Columns.GridColumn();
            gridColumn4 = new DevExpress.XtraGrid.Columns.GridColumn();
            repositoryItemButtonEdit1 = new DevExpress.XtraEditors.Repository.RepositoryItemButtonEdit();
            panelControl1 = new DevExpress.XtraEditors.PanelControl();
            order = new DevExpress.XtraEditors.SpinEdit();
            category = new DevExpress.XtraEditors.LookUpEdit();
            txtDescription = new DevExpress.XtraEditors.TextEdit();
            labelControl3 = new DevExpress.XtraEditors.LabelControl();
            labelControl4 = new DevExpress.XtraEditors.LabelControl();
            toolTip1 = new ToolTip(components);
            panelControl2 = new DevExpress.XtraEditors.PanelControl();
            simpleButton1 = new DevExpress.XtraEditors.SimpleButton();
            saveCommand = new DevExpress.XtraEditors.SimpleButton();
            ((ISupportInitialize)xtraTabControl2).BeginInit();
            xtraTabControl2.SuspendLayout();
            xtraTabPage3.SuspendLayout();
            ((ISupportInitialize)gridControl1).BeginInit();
            ((ISupportInitialize)gridView1).BeginInit();
            ((ISupportInitialize)repositoryItemButtonEdit1).BeginInit();
            ((ISupportInitialize)panelControl1).BeginInit();
            panelControl1.SuspendLayout();
            ((ISupportInitialize)order.Properties).BeginInit();
            ((ISupportInitialize)category.Properties).BeginInit();
            ((ISupportInitialize)txtDescription.Properties).BeginInit();
            ((ISupportInitialize)panelControl2).BeginInit();
            panelControl2.SuspendLayout();
            SuspendLayout();
            // 
            // labelControl1
            // 
            labelControl1.Location = new Point(36, 37);
            labelControl1.Margin = new Padding(4);
            labelControl1.Name = "labelControl1";
            labelControl1.Size = new Size(91, 19);
            labelControl1.TabIndex = 0;
            labelControl1.Text = "Classification";
            // 
            // xtraTabControl2
            // 
            xtraTabControl2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            xtraTabControl2.Location = new Point(15, 16);
            xtraTabControl2.Margin = new Padding(4);
            xtraTabControl2.Name = "xtraTabControl2";
            xtraTabControl2.SelectedTabPage = xtraTabPage3;
            xtraTabControl2.Size = new Size(1006, 459);
            xtraTabControl2.TabIndex = 1;
            xtraTabControl2.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] { xtraTabPage3 });
            // 
            // xtraTabPage3
            // 
            xtraTabPage3.Controls.Add(gridControl1);
            xtraTabPage3.Margin = new Padding(4);
            xtraTabPage3.Name = "xtraTabPage3";
            xtraTabPage3.Size = new Size(1004, 418);
            xtraTabPage3.Text = "Deductions";
            // 
            // gridControl1
            // 
            gridControl1.Dock = DockStyle.Fill;
            gridControl1.EmbeddedNavigator.Margin = new Padding(4);
            gridControl1.Location = new Point(0, 0);
            gridControl1.MainView = gridView1;
            gridControl1.Margin = new Padding(4);
            gridControl1.Name = "gridControl1";
            gridControl1.RepositoryItems.AddRange(new DevExpress.XtraEditors.Repository.RepositoryItem[] { repositoryItemButtonEdit1 });
            gridControl1.Size = new Size(1004, 418);
            gridControl1.TabIndex = 0;
            toolTip1.SetToolTip(gridControl1, "Double click to update");
            gridControl1.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { gridView1 });
            // 
            // gridView1
            // 
            gridView1.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] { gridColumn1, gridColumn3, gridColumn2, gridColumn5, gridColumn6, gridColumn4 });
            gridView1.DetailHeight = 512;
            gridView1.GridControl = gridControl1;
            gridView1.Name = "gridView1";
            gridView1.OptionsDetail.EnableDetailToolTip = true;
            gridView1.OptionsEditForm.PopupEditFormWidth = 1200;
            gridView1.DoubleClick += gridView1_DoubleClick;
            // 
            // gridColumn1
            // 
            gridColumn1.AppearanceHeader.Options.UseTextOptions = true;
            gridColumn1.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            gridColumn1.Caption = "Class";
            gridColumn1.FieldName = "Category";
            gridColumn1.MinWidth = 30;
            gridColumn1.Name = "gridColumn1";
            gridColumn1.OptionsColumn.AllowEdit = false;
            gridColumn1.OptionsColumn.ReadOnly = true;
            gridColumn1.Visible = true;
            gridColumn1.VisibleIndex = 0;
            gridColumn1.Width = 220;
            // 
            // gridColumn3
            // 
            gridColumn3.Caption = "Code";
            gridColumn3.FieldName = "Code";
            gridColumn3.MinWidth = 30;
            gridColumn3.Name = "gridColumn3";
            gridColumn3.OptionsColumn.AllowEdit = false;
            gridColumn3.OptionsColumn.ReadOnly = true;
            gridColumn3.Width = 73;
            // 
            // gridColumn2
            // 
            gridColumn2.AppearanceHeader.Options.UseTextOptions = true;
            gridColumn2.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            gridColumn2.Caption = "Name";
            gridColumn2.FieldName = "Name";
            gridColumn2.MinWidth = 30;
            gridColumn2.Name = "gridColumn2";
            gridColumn2.OptionsColumn.AllowEdit = false;
            gridColumn2.OptionsColumn.ReadOnly = true;
            gridColumn2.Visible = true;
            gridColumn2.VisibleIndex = 1;
            gridColumn2.Width = 504;
            // 
            // gridColumn5
            // 
            gridColumn5.Caption = "Priority";
            gridColumn5.FieldName = "PriorityLevel";
            gridColumn5.MinWidth = 30;
            gridColumn5.Name = "gridColumn5";
            gridColumn5.OptionsColumn.AllowEdit = false;
            gridColumn5.OptionsColumn.ReadOnly = true;
            gridColumn5.Visible = true;
            gridColumn5.VisibleIndex = 2;
            gridColumn5.Width = 103;
            // 
            // gridColumn6
            // 
            gridColumn6.AppearanceHeader.Options.UseTextOptions = true;
            gridColumn6.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            gridColumn6.Caption = "Status";
            gridColumn6.FieldName = "Status";
            gridColumn6.MinWidth = 30;
            gridColumn6.Name = "gridColumn6";
            gridColumn6.OptionsColumn.AllowEdit = false;
            gridColumn6.OptionsColumn.ReadOnly = true;
            gridColumn6.Visible = true;
            gridColumn6.VisibleIndex = 3;
            gridColumn6.Width = 120;
            // 
            // gridColumn4
            // 
            gridColumn4.AppearanceHeader.Options.UseTextOptions = true;
            gridColumn4.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            gridColumn4.Caption = "x";
            gridColumn4.ColumnEdit = repositoryItemButtonEdit1;
            gridColumn4.MaxWidth = 45;
            gridColumn4.MinWidth = 30;
            gridColumn4.Name = "gridColumn4";
            gridColumn4.Visible = true;
            gridColumn4.VisibleIndex = 4;
            gridColumn4.Width = 43;
            // 
            // repositoryItemButtonEdit1
            // 
            repositoryItemButtonEdit1.AutoHeight = false;
            repositoryItemButtonEdit1.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] { new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Clear) });
            repositoryItemButtonEdit1.Name = "repositoryItemButtonEdit1";
            repositoryItemButtonEdit1.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.HideTextEditor;
            repositoryItemButtonEdit1.ButtonClick += repositoryItemButtonEdit1_ButtonClick;
            // 
            // panelControl1
            // 
            panelControl1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            panelControl1.Controls.Add(order);
            panelControl1.Controls.Add(category);
            panelControl1.Controls.Add(txtDescription);
            panelControl1.Controls.Add(labelControl3);
            panelControl1.Controls.Add(labelControl1);
            panelControl1.Controls.Add(labelControl4);
            panelControl1.Location = new Point(16, 485);
            panelControl1.Margin = new Padding(4);
            panelControl1.Name = "panelControl1";
            panelControl1.Size = new Size(1006, 136);
            panelControl1.TabIndex = 2;
            // 
            // order
            // 
            order.EditValue = new decimal(new int[] { 0, 0, 0, 0 });
            order.Location = new Point(664, 70);
            order.Margin = new Padding(4, 3, 4, 3);
            order.Name = "order";
            order.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] { new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo) });
            order.Properties.Mask.EditMask = "n0";
            order.Properties.Mask.UseMaskAsDisplayFormat = true;
            order.Size = new Size(312, 28);
            order.TabIndex = 1;
            // 
            // category
            // 
            category.Location = new Point(170, 32);
            category.Margin = new Padding(4);
            category.Name = "category";
            category.Properties.AcceptEditorTextAsNewValue = DevExpress.Utils.DefaultBoolean.True;
            category.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] { new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo), new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Plus, "+", -1, true, true, false, editorButtonImageOptions2, new DevExpress.Utils.KeyShortcut(Keys.None), serializableAppearanceObject5, serializableAppearanceObject6, serializableAppearanceObject7, serializableAppearanceObject8, "", null, null, DevExpress.Utils.ToolTipAnchor.Default) });
            category.Properties.Columns.AddRange(new DevExpress.XtraEditors.Controls.LookUpColumnInfo[] { new DevExpress.XtraEditors.Controls.LookUpColumnInfo("Name", "ClassEntity", 30, DevExpress.Utils.FormatType.None, "", true, DevExpress.Utils.HorzAlignment.Default, DevExpress.Data.ColumnSortOrder.None, DevExpress.Utils.DefaultBoolean.Default) });
            category.Properties.DisplayMember = "Name";
            category.Properties.NullText = "";
            category.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard;
            category.Size = new Size(327, 26);
            category.TabIndex = 0;
            category.ProcessNewValue += category_ProcessNewValue;
            category.ButtonClick += category_ButtonClick;
            // 
            // txtDescription
            // 
            txtDescription.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtDescription.EditValue = "";
            txtDescription.Location = new Point(170, 70);
            txtDescription.Margin = new Padding(4);
            txtDescription.Name = "txtDescription";
            txtDescription.Size = new Size(327, 26);
            txtDescription.TabIndex = 2;
            // 
            // labelControl3
            // 
            labelControl3.Location = new Point(605, 73);
            labelControl3.Margin = new Padding(4);
            labelControl3.Name = "labelControl3";
            labelControl3.Size = new Size(51, 19);
            labelControl3.TabIndex = 0;
            labelControl3.Text = "Priority";
            // 
            // labelControl4
            // 
            labelControl4.Location = new Point(36, 73);
            labelControl4.Margin = new Padding(4);
            labelControl4.Name = "labelControl4";
            labelControl4.Size = new Size(79, 19);
            labelControl4.TabIndex = 0;
            labelControl4.Text = "Description";
            // 
            // panelControl2
            // 
            panelControl2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            panelControl2.Controls.Add(simpleButton1);
            panelControl2.Controls.Add(saveCommand);
            panelControl2.Location = new Point(16, 634);
            panelControl2.Margin = new Padding(4, 3, 4, 3);
            panelControl2.Name = "panelControl2";
            panelControl2.Size = new Size(1008, 73);
            panelControl2.TabIndex = 4;
            // 
            // simpleButton1
            // 
            simpleButton1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            simpleButton1.ImageOptions.Image = Properties.Resources.deletelist_16x16;
            simpleButton1.Location = new Point(644, 19);
            simpleButton1.Margin = new Padding(4);
            simpleButton1.Name = "simpleButton1";
            simpleButton1.Size = new Size(153, 41);
            simpleButton1.TabIndex = 1;
            simpleButton1.Text = "Clear";
            simpleButton1.Click += simpleButton1_Click_1;
            // 
            // saveCommand
            // 
            saveCommand.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            saveCommand.ImageOptions.Image = (Image)resources.GetObject("saveCommand.ImageOptions.Image");
            saveCommand.Location = new Point(824, 19);
            saveCommand.Margin = new Padding(4);
            saveCommand.Name = "saveCommand";
            saveCommand.Size = new Size(153, 41);
            saveCommand.TabIndex = 0;
            saveCommand.Text = "Save";
            saveCommand.Click += saveCommand_Click;
            // 
            // DeductionSetupUI
            // 
            AutoScaleDimensions = new SizeF(9F, 19F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1044, 722);
            Controls.Add(panelControl2);
            Controls.Add(panelControl1);
            Controls.Add(xtraTabControl2);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Margin = new Padding(4);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "DeductionSetupUI";
            Text = "Deduction Setup";
            Load += DeductionSetupUI_Load;
            ((ISupportInitialize)xtraTabControl2).EndInit();
            xtraTabControl2.ResumeLayout(false);
            xtraTabPage3.ResumeLayout(false);
            ((ISupportInitialize)gridControl1).EndInit();
            ((ISupportInitialize)gridView1).EndInit();
            ((ISupportInitialize)repositoryItemButtonEdit1).EndInit();
            ((ISupportInitialize)panelControl1).EndInit();
            panelControl1.ResumeLayout(false);
            panelControl1.PerformLayout();
            ((ISupportInitialize)order.Properties).EndInit();
            ((ISupportInitialize)category.Properties).EndInit();
            ((ISupportInitialize)txtDescription.Properties).EndInit();
            ((ISupportInitialize)panelControl2).EndInit();
            panelControl2.ResumeLayout(false);
            ResumeLayout(false);

        }

        #endregion
        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraTab.XtraTabControl xtraTabControl2;
        private DevExpress.XtraTab.XtraTabPage xtraTabPage3;
        private DevExpress.XtraGrid.GridControl gridControl1;
        private DevExpress.XtraGrid.Views.Grid.GridView gridView1;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn1;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn2;
        private DevExpress.XtraEditors.Repository.RepositoryItemButtonEdit repositoryItemButtonEdit1;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn4;
        private DevExpress.XtraEditors.PanelControl panelControl1;
        private System.Windows.Forms.ToolTip toolTip1;
        private DevExpress.XtraEditors.LookUpEdit category;
        private DevExpress.XtraEditors.PanelControl panelControl2;
        private DevExpress.XtraEditors.SimpleButton simpleButton1;
        private DevExpress.XtraEditors.SimpleButton saveCommand;
        private DevExpress.XtraEditors.SpinEdit order;
        private DevExpress.XtraEditors.LabelControl labelControl3;
        private DevExpress.XtraEditors.TextEdit txtDescription;
        private DevExpress.XtraEditors.LabelControl labelControl4;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn3;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn5;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn6;
    }
}