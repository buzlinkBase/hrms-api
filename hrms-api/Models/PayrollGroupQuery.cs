namespace Hrms.Api;

public class PayrollGroupQuery
{
    /// <summary>
    /// Filter by status. Default = all (“%”).
    /// </summary>
    public RecordStatus? Status { get; set; } =  RecordStatus.Any;
} 