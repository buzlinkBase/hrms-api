namespace DTR.Core.Services;

/// <summary>
/// The DTR engine as an <see cref="IDtrRowSource"/> -- the same calculation GET dailyrecords/dtr-detail
/// runs, so a draft saved in the background holds exactly what the review screen would show.
/// </summary>
public sealed class DtrCalcRowSource : IDtrRowSource
{
    private readonly DTRCalcService _calc;

    public DtrCalcRowSource(DTRCalcService calc)
    {
        _calc = calc;
    }

    public async Task<List<DTRDetailModel>> ComputeAsync(DTRRequestPayload payload, CancellationToken token)
    {
        var result = await _calc.GetDTRInfoAsync<DTRDetailModel>(
            payload, ProcessorType.DTRDetail, token, IncludeNullResponse.Include);
        return result.ToList();
    }
}
