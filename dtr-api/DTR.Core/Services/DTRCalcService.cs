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
        var context = await _payloadRangeService.SetPayload(canProcess, payload, removedoublePunch);
        Token = token;
        Payload = payload;
        context.DtrService = this;
        var calculator = new DailyRecordCompute(context);
        return calculator.ProcessDailyRecords(processor, payload.FromDate, payload.ToDate, token, ignoreNull, processOnlyPairedAtt);
    }
    public CancellationToken GetToken => Token ?? CancellationToken.None;
    public DTRRequestPayload GetPayload(DateOnly date)
    {
        if (Payload == null)
        {
            return new DTRRequestPayload
            {
                BranchId = Guid.Empty,
                ClientId = Guid.Empty,
                DepartmentId = Guid.Empty,
                EmployeeId = Guid.Empty,
                OperationAreaId = Guid.Empty,
                PayrollGroupId = Guid.Empty,
                FromDate = date,
                ToDate = date
            };
        }
        Payload.FromDate = date;
        Payload.ToDate = date;
        return Payload;
    }

}
