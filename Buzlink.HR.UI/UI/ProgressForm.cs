namespace Buzlink.HR.UI
{
    public partial class ProgressForm : DevExpress.XtraEditors.XtraForm
    {
        public delegate void Progress(int progress);
        public Progress handler;
        public ProgressForm()
        {
            InitializeComponent();
            handler = setProgress;
        }

        public void SetClose(Task t)
        {
            t.ContinueWith(s => Close());
        }

        public void setProgress(int i)
        {

            if (marqueeProgressBarControl1.InvokeRequired)
                marqueeProgressBarControl1.Invoke(handler, i);
            else
                marqueeProgressBarControl1.Text = i.ToString();
        }
    }
}