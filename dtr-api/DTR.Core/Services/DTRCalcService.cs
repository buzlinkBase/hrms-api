namespace DTR.Core;

public class DTRCalcService
{
    private readonly CurrentRangeDTRPayloadService _payloadRangeService;
    private readonly IServiceProvider _serviceProvider;
    private CancellationToken? Token;
    private DTRRequestPayload? Payload;
    public DTRCalcService(CurrentRangeDTRPayloadService payloadRangeService, IServiceProvider serviceProvider)
    {
        _payloadRangeService = payloadRangeService;
        _serviceProvider = serviceProvider;
    }
    public async Task<ObjectCollection<T>> GetDTRInfoAsync<T>(
        DTRRequestPayload payload,
        ProcessorType processorType,
        CancellationToken token,
        IncludeNullResponse ignoreNull = IncludeNullResponse.Include,
        bool processOnlyPairedAtt = true,
        bool removedoublePunch = true) where T : class, new()
    {
        var processor = DTRProcessorFactory.Create<T>(processorType, _serviceProvider);
        var canProcess = true;
        // The token reaches the payload queries, so an abandoned request stops loading data.
        var context = await _payloadRangeService.SetPayload(canProcess, payload, removedoublePunch, token);
        Token = token;
        Payload = payload;
        context.DtrService = this;
        var calculator = new DailyRecordCompute(context);
        return calculator.ProcessDailyRecords(processor, payload.FromDate, payload.ToDate, token, ignoreNull, processOnlyPairedAtt);
    }
    public CancellationToken GetToken => Token ?? CancellationToken.None;

    /// <summary>
    /// A fresh one-employee, one-day request for re-computing a single adjacent day (holiday
    /// eligibility lookups). Previously this mutated and reused the outer request's payload, so
    /// each lookup reloaded and computed the day for every employee the outer request covered
    /// (a whole company, for a payroll run) and then read whichever employee came first.
    /// </summary>
    public static DTRRequestPayload GetEmployeeDayPayload(Guid employeeId, DateOnly date) => new()
    {
        EmployeeId = employeeId,
        FromDate = date,
        ToDate = date
    };

}
