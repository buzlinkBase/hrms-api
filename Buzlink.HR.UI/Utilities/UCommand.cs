using BuzlinkRepository;
using DevExpress.XtraEditors;
using Hrms.Infrastructure.Data;

namespace Buzlink.HR.UI;

internal interface ICommandBehavior
{
    bool HideNotifications { get; set; }
}
internal class DefaultCommandBehavior : ICommandBehavior
{
    public bool HideNotifications { get; set; }
}

public interface IUnitOfWorkService : IUnitOfWork<HrmsContext> { }
public class UCommand : UnitOfWork<HrmsContext>, IUnitOfWorkService
{
    public UCommand(HrmsContext context) : base(context)
    {
        OnCommitChanges += UCommand_OnCommitChanges;
    }

    private void UCommand_OnCommitChanges(object? sender, CommitChangesResponse e)
    {
        XtraMessageBoxArgs args = new XtraMessageBoxArgs();
        args.Text = e.Message;
        args.Buttons = new DialogResult[] { DialogResult.OK };
        args.DefaultButtonIndex = 0;
        args.AutoCloseOptions = new AutoCloseOptions()
        {
            Delay = 5000,
            ShowTimerOnDefaultButton = true
        };

        if (e.Success)
        {
            args.MessageBeepSound = MessageBeepSound.Information;
            args.Caption = "Information";
            args.Icon = SystemIcons.Information;
            XtraMessageBox.Show(args);
        }
        else
        {
            args.MessageBeepSound = MessageBeepSound.Error;
            args.Icon = SystemIcons.Error;
            args.Caption = "Error";
            XtraMessageBox.Show(args);
        }
    }
}
