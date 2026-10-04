using Hrms.Domain.ValueObjects;

namespace hrms.test.DTRTests;

/// <summary>
/// DTRDetailModel.TotalHours — worked hours = Hrs + OT of every day type. DTR hours are
/// natural (Hrs/OT already include their night portion), so ND/ND-OT are never added.
/// </summary>
public class DTRDetailModelTotalHoursTests
{
    [Fact]
    public void RegularDay_SumsHrsAndOt_IgnoringNightBuckets()
    {
        var model = new DTRDetailModel
        {
            RegularNetHours = 8,
            RegularOTHours = 2,
            RegularNDHours = 1,
            RegularNDOTHours = 0.5,
        };

        model.TotalHours.Should().Be(10);
    }

    [Fact]
    public void LegalHolidayDay_CountsOtOnce_AndNoNightBuckets()
    {
        var model = new DTRDetailModel
        {
            LegalHolHours = 8,
            LegalHolOTHours = 2,
            LegalHolNightDiffHours = 1,
            LegalHolNightDiffOTHours = 0.5,
        };

        model.TotalHours.Should().Be(10); // 8 + 2; the night hours are already inside Hrs/OT
    }

    [Fact]
    public void MixedDay_SumsAcrossEveryWorkTypeGroup()
    {
        var model = new DTRDetailModel
        {
            RegularNetHours = 8,
            RestDayHours = 4,
            SpecialHolHours = 2,
            RestLegalDayHours = 1,
            RestSpecialDayHours = 1,
            DoubleLegalHours = 3,
            RestDoubleLegalHours = 2,
            SpecialWorkDayHours = 1,
        };

        model.TotalHours.Should().Be(22);
    }

    [Fact]
    public void ExcludesOfficialBusinessAndLeaveHours()
    {
        // TotalHours is "hours actually worked" — OB and leave time are deliberately not
        // worked hours, so they must not inflate the total.
        var model = new DTRDetailModel
        {
            RegularNetHours = 8,
            OBHours = 4,
            PaidLeaveHours = 8,
            UnpaidLeaveHours = 8,
        };

        model.TotalHours.Should().Be(8);
    }

    [Fact]
    public void NoHoursSet_TotalIsZero()
    {
        new DTRDetailModel().TotalHours.Should().Be(0);
    }
}
