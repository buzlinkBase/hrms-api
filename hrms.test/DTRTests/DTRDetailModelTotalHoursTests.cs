using Hrms.Domain.ValueObjects;

namespace hrms.test.DTRTests;

/// <summary>
/// DTRDetailModel.TotalHours — the per-day sum the DTR detail table's columns should add up
/// to. Regression coverage for a bug where LegalHolOTHours was added twice and
/// LegalHolNightDiffOTHours was never added at all.
/// </summary>
public class DTRDetailModelTotalHoursTests
{
    [Fact]
    public void RegularDay_SumsAllFourRegularBuckets()
    {
        var model = new DTRDetailModel
        {
            RegularNetHours = 8,
            RegularOTHours = 2,
            RegularNDHours = 1,
            RegularNDOTHours = 0.5,
        };

        model.TotalHours.Should().Be(11.5);
    }

    [Fact]
    public void LegalHolidayDay_IncludesNightDiffOT_AndDoesNotDoubleCountOT()
    {
        // Regression case for the original bug: LegalHolOTHours was summed twice and
        // LegalHolNightDiffOTHours was omitted entirely.
        var model = new DTRDetailModel
        {
            LegalHolHours = 8,
            LegalHolOTHours = 2,
            LegalHolNightDiffHours = 1,
            LegalHolNightDiffOTHours = 0.5,
        };

        model.TotalHours.Should().Be(11.5); // 8 + 2 + 1 + 0.5, each counted exactly once
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
