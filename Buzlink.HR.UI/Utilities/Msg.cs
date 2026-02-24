namespace Buzlink.HR.UI;

public class Msg
{
    public static void Inform(string message)
    {
        MessageBox.Show(message, "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    public static void Critical(string msg)
    {
        MessageBox.Show(msg, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public static void Error(Exception ex)
    {
        if (ex is TaskCanceledException || ex is OperationCanceledException) return;
        MessageBox.Show(ex.Message, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    public static bool ContinueSave()
    {
        return YesNo("Are you sure you want to continue saving this data?");
    }
    public static bool ContinueDownload()
    {
        return YesNo("Are you sure you want to continue downloading this data?");
    }

    public static bool ContinueUpdate()
    {
        return YesNo("Are you sure you want to continue update this data?");
    }

    public static bool MessageWhat(string msg)
    {
        return YesNo(msg + "?");
    }
    public static bool ContinueCancel()
    {
        return YesNo("Are you sure to cancel this record?");
    }
    public static bool ContinueActivating()
    {
        return YesNo("Are you sure to activate this record?");
    }

    public static bool ContinueDelete()
    {
        return YesNo("Are you sure to delete this data?");
    }

    public static bool ContinueLock()
    {
        return YesNo("Are you sure you want to lock this DTR?");
    }
    public static bool ContinueUnLock()
    {
        return YesNo("Are you sure you want to unlock this DTR?");
    }
    public static bool ContinueApprove()
    {
        return YesNo("Are you sure you want to approve this transaction?");
    }
    public static bool MessageBoxContinueUnApprove()
    {
        return YesNo("Are you sure you want to un-approve this transaction?");
    }

    public static bool YesNo(string msg)
    {
        DialogResult dialogResult = MessageBox.Show(msg, "Permission", MessageBoxButtons.YesNo);
        if (dialogResult == DialogResult.Yes)
            return true;
        return false;
    }

    public static void Cancelled()
    {
        MessageBox.Show("Successfully Cancelled", "Exception", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    public static void Deleted()
    {
        MessageBox.Show("Successfully deleted", "Exception", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    public static void Approved()
    {
        MessageBox.Show("Successfully approved", "Exception", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    public static void Saved()
    {
        MessageBox.Show("Transaction completed successfully.", "Exception", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}