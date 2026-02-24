namespace Buzlink.HR.UI;

public class ExceptionFormatter
{
    public static void Format(Exception ex)
    {
        Msg.Error(ex);
    }
}