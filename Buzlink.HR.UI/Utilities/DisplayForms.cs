namespace Buzlink.HR.UI;

public class DisplayForms
{
    public static void ViewForm(Form frm, IServiceResolver scope, Form? parent = null)
    {
        frm.MdiParent = Program.main;
        frm.WindowState = FormWindowState.Maximized;
        if (scope != null)
        {
            frm.FormClosed += (s, e) =>
            {
                frm.BeginInvoke(new Action(() =>
                {
                    scope.Dispose();
                }));
            };
        }
        frm.KeyPreview = true;
        if (parent is not null)
            frm.Show(parent);
        else
            frm.Show();

    }



    public static void ViewFormFloat(Form frm, IServiceResolver scope, Form? parent = null)
    {
        frm.StartPosition = FormStartPosition.CenterScreen;
        frm.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                frm.Close();
            }
        };
        if (scope != null)
        {
            frm.FormClosed += (s, e) =>
            {
                frm.BeginInvoke(new Action(() =>
                {
                    scope.Dispose();
                }));
            };
        }
        frm.KeyPreview = true;
        if (parent == null)
            frm.Show();
        else
            frm.Show(parent);
    }
    public static DialogResult ViewFormDialog(Form frm, IServiceResolver? scope = null, Form? parent = null)
    {
        frm.StartPosition = FormStartPosition.CenterScreen;
        frm.KeyPreview = true;
        frm.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                frm.Close();
            }
        };
        if (scope != null)
        {
            frm.FormClosed += (s, e) =>
            {
                frm.BeginInvoke(new Action(() =>
                {
                    scope.Dispose();
                }));
            };
        }
        if (parent == null) parent = Program.main;
        return frm.ShowDialog(parent);
    }
}
