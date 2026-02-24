namespace DTR.Core;

public class DTRCalcService
{
    private readonly CurrentRangeDTRPayloadService _payloadRangeService;
    private CancellationToken? Token;
    public DTRCalcService(CurrentRangeDTRPayloadService payloadRangeService)
    {
        _payloadRangeService = payloadRangeService;
    }
    public async Task<ObjectCollection<T>> GetDTRInfoAsync<T>(
        DTRRequestPayload payload,
        ProcessorType processorType,
        CancellationToken token,
        IncludeNullResponse ignoreNull = IncludeNullResponse.Include,
        bool processOnlyPairedAtt = true,
        bool removedoublePunch = true) where T : class, new()
    {
        var processor = DTRProcessorFactory.Create<T>(processorType);
        var canProcess = true;
        var context = await _payloadRangeService.SetPayload(canProcess, payload, removedoublePunch);
        Token = token;
        context.DtrService = this;
        var calculator = new DailyRecordCompute(context);
        return calculator.ProcessDailyRecords(processor, payload.FromDate, payload.ToDate, token, ignoreNull, processOnlyPairedAtt);
    }
    public CancellationToken GetToken => Token ?? CancellationToken.None;

}
