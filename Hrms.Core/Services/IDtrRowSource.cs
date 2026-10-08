namespace Hrms.Core.Services;

/// <summary>
/// Where calculated DTR day rows come from. The calculation engine lives in DTR.Core, which
/// depends on Hrms.Core, so Hrms.Core's background save reaches it through this seam; DTR.Core
/// supplies the implementation (DtrCalcRowSource).
/// </summary>
public interface IDtrRowSource
{
    Task<List<DTRDetailModel>> ComputeAsync(DTRRequestPayload payload, CancellationToken token);
}
