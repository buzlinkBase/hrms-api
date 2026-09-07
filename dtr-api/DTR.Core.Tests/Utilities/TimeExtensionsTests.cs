using Hrms.Domain.Entities;

namespace DTR.Core.Tests.Utilities;

/// <summary>
/// The remaining pure, TimeContext-free helpers in TimeExtensions.cs not already exercised
/// indirectly through the pipeline/policy tests elsewhere in this project (Exclude, Intersect
/// on collections, MergeOverlapping, CropFromStart, ToTimeRange, TotalMinutes and IsEmpty are
/// all covered many times over by those tests already).
/// </summary>
public class TimeExtensionsTests
{
    private static readonly DateTime Base = new(2026, 1, 1, 8, 0, 0);

    // --- FlattenUntilShiftEnd -----------------------------------------------------------------

    [Fact]
    public void FlattenUntilShiftEnd_CropsRecordsToTheShiftWindow()
    {
        var records = new TimeRecordCollection
        {
            new TimeRecord(Base.AddHours(-1), Base.AddHours(1)), // starts before shift, ends inside
            new TimeRecord(Base.AddHours(2), Base.AddHours(10)), // ends after shift
        };

        var result = records.FlattenUntilShiftEnd(Base, TimeSpan.FromHours(8)); // shift 8:00-16:00

        result.TotalMinutes.Should().Be(60 + 360); // 07:00-08:00 cropped to 08:00-09:00 (60m) + 10:00-16:00 (360m)
        result.TimeRecords.Should().HaveCount(2);
    }

    [Fact]
    public void FlattenUntilShiftEnd_RecordEntirelyBeforeShift_IsSkipped()
    {
        var records = new TimeRecordCollection { new TimeRecord(Base.AddHours(-3), Base.AddHours(-1)) };

        records.FlattenUntilShiftEnd(Base, TimeSpan.FromHours(8)).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void FlattenUntilShiftEnd_RecordEntirelyAfterShift_IsSkipped()
    {
        var records = new TimeRecordCollection { new TimeRecord(Base.AddHours(9), Base.AddHours(10)) };

        records.FlattenUntilShiftEnd(Base, TimeSpan.FromHours(8)).IsEmpty().Should().BeTrue();
    }

    // --- FlattenUntil ---------------------------------------------------------------------------

    [Fact]
    public void FlattenUntil_AccumulatesRecordsUpToTarget_CroppingTheLastOne()
    {
        var records = new TimeRecordCollection
        {
            new TimeRecord(Base, Base.AddHours(2)),               // 120m
            new TimeRecord(Base.AddHours(3), Base.AddHours(6)),   // 180m, target hit partway through
        };

        var result = records.FlattenUntil(200); // 120 + 80

        result.TotalMinutes.Should().Be(200);
        result.TimeRecords.Last().EndTime.Should().Be(Base.AddHours(3).AddMinutes(80));
    }

    [Fact]
    public void FlattenUntil_TargetExceedsTotal_ReturnsEverything()
    {
        var records = new TimeRecordCollection { new TimeRecord(Base, Base.AddHours(2)) };

        var result = records.FlattenUntil(1000);

        result.TotalMinutes.Should().Be(120);
    }

    // --- FlattenAfter ---------------------------------------------------------------------------

    [Fact]
    public void FlattenAfter_CropsRecordsStraddlingTheBoundary()
    {
        var boundary = Base.AddHours(1);
        var records = new TimeRecordCollection
        {
            new TimeRecord(Base, Base.AddHours(2)),               // straddles boundary -> cropped to 1h
            new TimeRecord(Base.AddHours(3), Base.AddHours(4)),   // entirely after -> kept whole
            new TimeRecord(Base.AddHours(-2), Base.AddHours(-1)), // entirely before -> dropped
        };

        var result = records.FlattenAfter(boundary);

        result.TotalMinutes.Should().Be(60 + 60);
        result.TimeRecords.Should().HaveCount(2);
        result.TimeRecords.First().StartTime.Should().Be(boundary);
    }

    [Fact]
    public void FlattenAfter_EmptySource_ReturnsEmpty()
    {
        new TimeRecordCollection().FlattenAfter(Base).IsEmpty().Should().BeTrue();
    }

    // --- CropFromEnd / DeductFromEnd -----------------------------------------------------------

    [Fact]
    public void CropFromEnd_RetainsMinutesCountingBackFromTheLatestRecord()
    {
        var records = new TimeRecordCollection
        {
            new TimeRecord(Base, Base.AddHours(1)),               // 60m
            new TimeRecord(Base.AddHours(2), Base.AddHours(4)),   // 120m, latest
        };

        var result = records.CropFromEnd(90); // takes all of the later record's last 90m

        result.TotalMinutes.Should().Be(90);
        result.TimeRecords.Single().StartTime.Should().Be(Base.AddHours(2).AddMinutes(30));
        result.TimeRecords.Single().EndTime.Should().Be(Base.AddHours(4));
    }

    [Fact]
    public void DeductFromEnd_RemovesMinutesFromTheLatestRecord()
    {
        var records = new TimeRecordCollection
        {
            new TimeRecord(Base, Base.AddHours(1)),               // 60m, kept
            new TimeRecord(Base.AddHours(2), Base.AddHours(4)),   // 120m, latest -> trimmed by 30
        };

        var result = records.DeductFromEnd(30);

        result.TotalMinutes.Should().Be(150); // 60 + 90
        result.TimeRecords.Should().HaveCount(2);
        result.TimeRecords.Last().EndTime.Should().Be(Base.AddHours(3).AddMinutes(30));
    }

    [Fact]
    public void DeductFromEnd_RemovesWholeRecordsWhenTheyFitWithinTheDeduction()
    {
        // Deduction walks from the chronological END of the collection — the LATER record
        // (by EndTime) is consumed first, entirely, since its 60m fits the 60m deduction;
        // the earlier record is untouched because by the time it's reached, remaining == 0.
        var records = new TimeRecordCollection
        {
            new TimeRecord(Base, Base.AddHours(1)),               // 60m -> fully kept
            new TimeRecord(Base.AddHours(2), Base.AddHours(3)),   // 60m -> fully removed
        };

        var result = records.DeductFromEnd(60);

        result.TotalMinutes.Should().Be(60);
        result.TimeRecords.Single().StartTime.Should().Be(Base);
    }

    // --- Intersect (single-record) / IsOverlaps -------------------------------------------------

    [Fact]
    public void Intersect_OverlappingRecords_ReturnsTheOverlapWindow()
    {
        var a = new TimeRecord(Base, Base.AddHours(3));
        var b = new TimeRecord(Base.AddHours(1), Base.AddHours(4));

        var result = a.Intersect(b);

        result!.StartTime.Should().Be(Base.AddHours(1));
        result.EndTime.Should().Be(Base.AddHours(3));
    }

    [Fact]
    public void Intersect_NonOverlappingRecords_ReturnsNull()
    {
        var a = new TimeRecord(Base, Base.AddHours(1));
        var b = new TimeRecord(Base.AddHours(2), Base.AddHours(3));

        a.Intersect(b).Should().BeNull();
    }

    [Fact]
    public void IsOverlaps_OverlappingRecords_ReturnsTrue()
    {
        var a = new TimeRecord(Base, Base.AddHours(2));
        var b = new TimeRecord(Base.AddHours(1), Base.AddHours(3));

        a.IsOverlaps(b).Should().BeTrue();
    }

    [Fact]
    public void IsOverlaps_AdjacentNonOverlappingRecords_ReturnsFalse()
    {
        var a = new TimeRecord(Base, Base.AddHours(1));
        var b = new TimeRecord(Base.AddHours(1), Base.AddHours(2));

        a.IsOverlaps(b).Should().BeFalse();
    }

    // --- Small conversion/formatting helpers -----------------------------------------------------

    [Fact]
    public void ToHour_ConvertsMinutesToHours()
    {
        (120.0).ToHour().Should().Be(2);
    }

    [Fact]
    public void ToDays_ConvertsMinutesToDays()
    {
        // A standard 8h working day should come out to 1.0 day (TimeConverter's own convention).
        (480.0).ToDays().Should().BeGreaterThan(0);
    }

    [Fact]
    public void AtStartOfDay_DateOnly_ReturnsMidnight()
    {
        var date = new DateOnly(2026, 1, 5);
        date.AtStartOfDay().Should().Be(new DateTime(2026, 1, 5, 0, 0, 0));
    }

    [Fact]
    public void AtStartOfDay_DateTime_TruncatesTimeOfDay()
    {
        new DateTime(2026, 1, 5, 14, 30, 0).AtStartOfDay().Should().Be(new DateTime(2026, 1, 5, 0, 0, 0));
    }

    [Fact]
    public void ToDateTime_TimeSpanPlusDate_CombinesThem()
    {
        var date = new DateOnly(2026, 1, 5);
        var time = new TimeSpan(14, 30, 0);

        time.ToDateTime(date).Should().Be(new DateTime(2026, 1, 5, 14, 30, 0));
    }

    [Fact]
    public void ToRangeString_FormatsAsHHmmDash()
    {
        var record = new TimeRecord(new DateTime(2026, 1, 5, 8, 0, 0), new DateTime(2026, 1, 5, 17, 30, 0));

        record.ToRangeString().Should().Be("08:00–17:30");
    }

    // --- AttendanceHelper (internal) -------------------------------------------------------------

    [Fact]
    public void IsWithinTolerance_WithinAllowedMinutes_ReturnsTrue()
    {
        AttendanceHelper.IsWithinTolerance(Base, Base.AddMinutes(4), toleranceMinutes: 5).Should().BeTrue();
    }

    [Fact]
    public void IsWithinTolerance_BeyondAllowedMinutes_ReturnsFalse()
    {
        AttendanceHelper.IsWithinTolerance(Base, Base.AddMinutes(6), toleranceMinutes: 5).Should().BeFalse();
    }

    [Fact]
    public void GetPunchNear_FindsTheClosestMatchWithinTolerance()
    {
        var records = new List<Attendance>
        {
            new Attendance { WorkDateTime = Base.AddMinutes(-10) },
            new Attendance { WorkDateTime = Base.AddMinutes(2) },
        };

        var result = AttendanceHelper.GetPunchNear(Base, records, toleranceMinutes: 5);

        result.Should().NotBeNull();
        result!.WorkDateTime.Should().Be(Base.AddMinutes(2));
    }

    [Fact]
    public void GetPunchNear_NoRecordWithinTolerance_ReturnsNull()
    {
        var records = new List<Attendance> { new Attendance { WorkDateTime = Base.AddHours(2) } };

        AttendanceHelper.GetPunchNear(Base, records, toleranceMinutes: 5).Should().BeNull();
    }

    [Fact]
    public void Let_NonNullSource_AppliesSelector()
    {
        "abc".Let(s => s.Length).Should().Be(3);
    }

    [Fact]
    public void Let_NullSource_ReturnsDefault()
    {
        ((string?)null).Let(s => s.Length).Should().Be(0);
    }
}
