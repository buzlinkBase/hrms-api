using DevExpress.XtraGrid.Views.Grid;
using System.Text;

namespace Buzlink.HR.UI
{
    public static class UtilExtensions
    {
        public static string ToMysqlDateTime(this DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd H:mm:ss");
        }

        public static string ToMysqlDate(this DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd");
        }
        public static string RSQ(this string str)
        {
            return str.ReplaceAll("'", "\\'");
        }

        public static string ReplaceAll(this string original, string toBeReplaced, string newValue)
        {
            StringBuilder builder = new StringBuilder(original);
            builder.Replace(toBeReplaced, newValue);
            return builder.ToString();
        }

        public static void EndEdit(this GridView view)
        {
            view.CloseEditor();
            view.UpdateCurrentRow();
        }
    }
}
