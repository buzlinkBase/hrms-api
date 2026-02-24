using Buzlink.Extensions;
using DateRange = Buzlink.Extensions.DateRange;

namespace Buzlink.HR.UI
{
    [DefaultEvent("OnFilterChange")]
    public partial class DateRangeFilter : DevExpress.XtraEditors.XtraUserControl
    {
        public delegate void OnFilterChangeHandler(DateRange range);
        public event OnFilterChangeHandler OnFilterChange;
        private bool IsLoaded;
        public DateRangeFilter()
        {
            InitializeComponent();
            set();
            IsLoaded = true;
        }
        private void set()
        {
            dateEdit1.DateTime = DateTime.Today;
            dateEdit2.DateTime = DateTime.Today;
            comboBoxEdit1.Text = FilterName;
            filterData(FilterName);
        }
        private DateTime fromDate;
        public DateTime FromDate
        {
            get { return fromDate; }
            set
            {
                fromDate = value;
                dateEdit1.DateTime = value;
            }
        }
        private DateTime _toDate;
        private bool _hideDate;
        public bool HideDate
        {
            get { return _hideDate; }
            set
            {
                _hideDate = value;
                dateEdit1.Visible = comboBoxEdit1.Text == "Range" ? true : !value;
                dateEdit2.Visible = comboBoxEdit1.Text == "Range" ? true : !value;
                FilterComponent_Resize(this, EventArgs.Empty);
            }
        }
        private string _filterName = "Today";
        public string FilterName
        {
            get { return _filterName; }
            set
            {
                _filterName = value;
                comboBoxEdit1.Text = value;
            }
        }
        public DateTime ToDate
        {
            get { return _toDate; }
            set
            {
                _toDate = value;
                dateEdit2.DateTime = value;
            }
        }
        private void comboBoxEdit1_SelectedIndexChanged(object sender, EventArgs e)
        {
            FilterName = comboBoxEdit1.Text;
            filterData(FilterName);
        }
        private void filterData(string filter)
        {
            var range = new DateRange();
            dateEdit1.Enabled = comboBoxEdit1.Text == "Range";
            dateEdit2.Enabled = comboBoxEdit1.Text == "Range";
            dateEdit1.Visible = filter == "Range" ? true : !HideDate;
            dateEdit2.Visible = filter == "Range" ? true : !HideDate;
            switch (filter)
            {
                case "Today":
                    range = FilterRangeDate.Today(DateTime.Today);
                    dateEdit1.DateTime = range.Start;
                    dateEdit2.DateTime = range.End;
                    break;
                case "Yesterday":
                    range = FilterRangeDate.YesterDay(DateTime.Today);
                    dateEdit1.DateTime = range.Start;
                    dateEdit2.DateTime = range.End;
                    break;
                case "This Week":
                    range = FilterRangeDate.ThisWeek(DateTime.Today);
                    dateEdit1.DateTime = range.Start;
                    dateEdit2.DateTime = range.End;
                    break;
                case "Last Week":
                    range = FilterRangeDate.LastWeek(DateTime.Today);
                    dateEdit1.DateTime = range.Start;
                    dateEdit2.DateTime = range.End;
                    break;
                case "This Month":
                    range = FilterRangeDate.ThisMonth(DateTime.Today);
                    dateEdit1.DateTime = range.Start;
                    dateEdit2.DateTime = range.End;
                    break;
                case "Last Month":
                    range = FilterRangeDate.LastMonth(DateTime.Today);
                    dateEdit1.DateTime = range.Start;
                    dateEdit2.DateTime = range.End;
                    break;
                case "This Year":
                    range = FilterRangeDate.ThisYear(DateTime.Today);
                    dateEdit1.DateTime = range.Start;
                    dateEdit2.DateTime = range.End;
                    break;
                case "Last Year":
                    range = FilterRangeDate.LastYear(DateTime.Today);
                    dateEdit1.DateTime = range.Start;
                    dateEdit2.DateTime = range.End;
                    break;
                case "Range":
                    range = FilterRangeDate.Today(DateTime.Today);
                    dateEdit1.DateTime = range.Start;
                    dateEdit2.DateTime = range.End;
                    break;
                default:
                    comboBoxEdit1.Text = "Today";
                    FilterName = "Today";
                    range = FilterRangeDate.Today(DateTime.Today);
                    dateEdit1.DateTime = range.Start;
                    dateEdit2.DateTime = range.End;
                    break;
            }

            FromDate = dateEdit1.DateTime;
            ToDate = dateEdit2.DateTime;
            if (IsLoaded) OnFilterChange?.Invoke(range);

        }
        public void SetFilter(DateFilterName filterName)
        {
            comboBoxEdit1.Text = filterName.Value;
            filterData(filterName.Value);
        }
        private void FilterComponent_Load(object sender, EventArgs e)
        {
            set();
        }
        private void FilterComponent_Resize(object sender, EventArgs e)
        {
            Height = comboBoxEdit1.Height + 2;
            if (HideDate) Width = comboBoxEdit1.Width + 2;
            else Width = comboBoxEdit1.Width + dateEdit1.Width + dateEdit2.Width + 6;
        }
    }
}
