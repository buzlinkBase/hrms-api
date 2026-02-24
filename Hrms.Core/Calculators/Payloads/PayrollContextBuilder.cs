namespace Hrms.Core.Calculators.Payloads;

public class PayrollContextBuilder
{
    private PayrollContext _context = new();
    public PayrollContextBuilder SetDailyRecord(DailyRecordRunModel record)
    {
        _context.DailyRecord = record;
        return this;
    }
    public PayrollContextBuilder SetWorkType(DailyRecordRunModel record)
    {
        var value = record.WorkType.RemoveSpacesBeforeCaps();
        var workType = (WorkType)Enum.Parse(typeof(WorkType), value);
        _context.WorkType = workType;
        return this;
    }

    public PayrollContextBuilder SetPayrollDate(DateOnly payrollDate)
    {
        _context.PayrollDate = payrollDate;
        return this;
    }
    public PayrollContextBuilder SetEmployee(EmployeeModelPayrollRun employee)
    {
        _context.Employee = employee;
        return this;
    }
    public PayrollContextBuilder SetPayload(CalculatorPayload payload)
    {
        _context.Payload = payload;
        return this;
    }
    public PayrollContext Build() => _context;
}
public class DeductionPayloadContextBuilder
{
    private DeductionPayloadContext _context = new();
    public DeductionPayloadContextBuilder SetPayload(CalculatorPayload payload)
    {
        _context.Payload = payload;
        return this;
    }

    public DeductionPayloadContextBuilder SetPayrollLine(PayrollSummaryLine payload)
    {
        _context.PayrollLine = payload;
        return this;
    }
    public DeductionPayloadContextBuilder SetEmployee(EmployeeModelPayrollRun payload)
    {
        _context.Employee = payload;
        return this;
    }
    public DeductionPayloadContext Build() => _context;
}
