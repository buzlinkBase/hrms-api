using Hrms.Domain;

namespace Hrms.Core.Services;

public static class HolidayPaidPolicy
{
    public static bool Resolve(HolidayType holType) => holType == HolidayType.LEGAL;
}
