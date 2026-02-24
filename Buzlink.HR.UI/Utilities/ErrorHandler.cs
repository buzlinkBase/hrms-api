namespace Buzlink.HR.UI.Utilities
{
    internal class ErrorHandler
    {
        public static void Handle(Exception ex)
        {
            if (ex is AggregateException aggEx)
            {
                foreach (var inner in aggEx.InnerExceptions)
                {
                    Msg.Error(inner);
                }
            }
            else
            {
                Msg.Error(ex);
            }
        }
    }
}
