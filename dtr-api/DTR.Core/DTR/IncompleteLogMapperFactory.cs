using Hrms.Domain.Entities;

namespace DTR.Core;

public interface IIncompleteLogShiftMapper
{
    ColumnarLogModel MapData(DTRProcessorPayload payload, List<Attendance> attendances, DateOnly payrollDate);
}

public static class IncompleteLogMapperFactory
{
    public static IIncompleteLogShiftMapper Create(DTRProcessorPayload payload) =>
        payload.Data.CurrentShift.ShiftType switch
        {
            TimeShiftType.FIXED => new SplitMapper(payload),//use split to show all logs instead of limitted to nearpunch
            TimeShiftType.SPLIT => new SplitMapper(payload),
            _ => throw new NotImplementedException("Unsupported shift type [IncompleteLogModel]")
        };
}


