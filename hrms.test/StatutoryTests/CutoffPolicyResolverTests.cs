using FluentAssertions;
using Hrms.Core.Policies.DeductionPolicies;
using Hrms.Domain;
using Hrms.Domain.ValueObjects;
using Xunit;

namespace hrms.test;

public class CutoffPolicyResolverTests
{
    private readonly CutoffPolicyResolver _sut;

    public CutoffPolicyResolverTests()
    {
        _sut = new CutoffPolicyResolver();
    }

    private DeductionPayloadContext CreateMockContext(DateOnly from, DateOnly to,
        List<CutoffModel> configs,
        PayrollFrequency payrollFrequency)
    {
        var employee = new EmployeeModelPayrollRun
        {
            Id = Guid.Empty,
            PayrollFrequency = payrollFrequency,
            PayrollGroup = new PayrollGroupModel
            {
                PayrollFrequency = payrollFrequency,
                CutoffDays = configs
            },
        };
        var context = new DeductionPayloadContext
        {
            Employee = employee,
            Payload = new CalculatorPayload
            {
                FromDate = from,
                ToDate = to,
            }
        };
        return context;
    }

    [Fact]
    public void GetCurrentCutoff_ShouldReturnEndOfMonth_WhenIsEndOfMonthIsTrue()
    {
        // Arrange: February 2025 (Non-leap year)
        var configs = new List<CutoffModel>
        {
            new() { IsEndOfMonth = true, Label = "Final Cutoff" }
        };
        var context = CreateMockContext(
            new DateOnly(2025, 2, 15),
            new DateOnly(2025, 2, 28),
            configs, PayrollFrequency.MONTHLY);

        // Act
        var result = _sut.GetCurrentCutoff(context);

        // Assert
        result.Day.Should().Be(28);
        result.Label.Should().Be("Final Cutoff");
    }

    [Theory]
    [InlineData(10, 15)] // Current day 10, should match 15
    [InlineData(15, 15)] // Current day 15, should match 15
    [InlineData(16, 30)] // Current day 16, should match 30
    public void GetCurrentCutoff_ShouldFindNextAvailableCutoff(int currentDay, int expectedDay)
    {
        // Arrange
        var configs = new List<CutoffModel>
        {
            new() { Day = 15, Label = "First" },
            new() { Day = 30, Label = "Second" }
        };
        var context = CreateMockContext(
            new DateOnly(2025, 4, currentDay),
            new DateOnly(2025, 4, currentDay + 5),
            configs, PayrollFrequency.SEMI_MONTHLY);

        // Act
        var result = _sut.GetCurrentCutoff(context);

        // Assert
        result.Day.Should().Be(expectedDay);
    }

    [Fact]
    public void IsFirstCutoff_ShouldBeTrue_WhenDayIsBeforeFirstConfig()
    {
        // Arrange
        var configs = new List<CutoffModel>
        {
            new() { Day = 15 },
            new() { Day = 30 }
        };
        var context = CreateMockContext(
            new DateOnly(2025, 1, 5),
            new DateOnly(2025, 1, 15),
            configs, PayrollFrequency.SEMI_MONTHLY);

        // Act & Assert
        _sut.IsFirstCutoff(context).Should().BeTrue();
        _sut.IsSecondCutoff(context).Should().BeFalse();
    }

    [Fact]
    public void ResolveCutoffModels_ShouldHandleLeapYearFebruary()
    {
        // Arrange: Feb 2024 was a leap year
        var configs = new List<CutoffModel> { new() { IsEndOfMonth = true } };
        var context = CreateMockContext(
            new DateOnly(2024, 2, 1),
            new DateOnly(2024, 2, 29),
            configs, PayrollFrequency.MONTHLY);

        // Act
        var result = _sut.GetCurrentCutoff(context);

        // Assert
        result.Day.Should().Be(29);
    }

    [Fact]
    public void GetSecondCutoff_ShouldThrow_IfOnlyOneCutoffConfigured()
    {
        // Arrange
        var configs = new List<CutoffModel> { new() { Day = 30 } };
        var context = CreateMockContext(
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 1, 31),
            configs, PayrollFrequency.MONTHLY);

        // Act
        Action act = () => _sut.GetSecondCutoff(context);

        // Assert
        act.Should().Throw<CutoffMismatchException>()
           .WithMessage("Monthly payroll frequency does not support a second cutoff.");
    }

    [Fact]
    public void IsSecondCutoff_ShouldReturnTrue_WhenDayIsAfterFirstCutoff()
    {
        // Arrange: Cutoffs 15 and 30. Today is 16.
        var configs = new List<CutoffModel>
        {
            new() { Day = 15 },
            new() { Day = 30 }
        };
        var context = CreateMockContext(
            new DateOnly(2025, 1, 16),
            new DateOnly(2025, 1, 30),
            configs, PayrollFrequency.SEMI_MONTHLY);

        // Act
        var result = _sut.IsSecondCutoff(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ResolveCutoff_ShouldUseToDate_WhenPeriodCrossesMonth()
    {
        // Scenario: Period is Jan 26 to Feb 10 (Cross-month)
        var configs = new List<CutoffModel>
        {
            new() { IsEndOfMonth = true, Label = "Month End" }
        };

        var context = CreateMockContext(
            new DateOnly(2025, 1, 26),
            new DateOnly(2025, 2, 10),
            configs, PayrollFrequency.MONTHLY);

        // Act
        var result = _sut.GetCurrentCutoff(context);

        // Assert: 2025 is not a leap year, so Feb EndOfMonth should be 28.
        result.Day.Should().Be(28);
        result.Label.Should().Be("Month End");
    }

    [Fact]
    public void ResolveCutoff_ShouldHandleLeapYear_WhenCrossingIntoFebruary()
    {
        // Scenario: Jan 26, 2024 to Feb 5, 2024 (2024 was a Leap Year)
        var configs = new List<CutoffModel>
        {
            new() { IsEndOfMonth = true, Label = "Month End" }
        };

        var context = CreateMockContext(
            new DateOnly(2024, 1, 26),
            new DateOnly(2024, 2, 5),
            configs, PayrollFrequency.MONTHLY);

        // Act
        var result = _sut.GetCurrentCutoff(context);

        // Assert
        result.Day.Should().Be(29);
    }

    [Fact]
    public void IsFirstCutoff_ShouldHandleCrossMonthTransition()
    {
        // Scenario: A very late January start that is technically the "First Cutoff" of the Feb cycle
        var configs = new List<CutoffModel>
        {
            new() { Day = 15 },
            new() { IsEndOfMonth = true }
        };

        var context = CreateMockContext(
            new DateOnly(2025, 1, 28),
            new DateOnly(2025, 2, 12),
            configs, PayrollFrequency.SEMI_MONTHLY);

        // Act & Assert
        // Since 28 is > 15, it falls into the second cutoff bracket of the reference month cycle.
        _sut.IsSecondCutoff(context).Should().BeTrue();
    }

    [Fact]
    public void GetCurrentCutoff_TwoFixedDates_CrossMonth_ShouldReturnLastCutoff()
    {
        // Arrange: Fixed cutoffs on the 5th and 20th
        var configs = new List<CutoffModel>
        {
            new() { Day = 5, IsEndOfMonth = false, Label = "First Cutoff" },
            new() { Day = 20, IsEndOfMonth = false, Label = "Second Cutoff" }
        };

        // Period: Jan 21 to Feb 5 (Crosses month). Reference date becomes Feb 5.
        var context = CreateMockContext(
            new DateOnly(2025, 1, 21),
            new DateOnly(2025, 2, 5),
            configs, PayrollFrequency.SEMI_MONTHLY);

        // Act
        var result = _sut.GetCurrentCutoff(context);

        // Assert: 
        // Reference day is 5. Match <= 5 is Day 5 (First Cutoff).
        result.Day.Should().Be(5);
        result.Label.Should().Be("First Cutoff");
    }

    [Theory]
    [InlineData(15, 15, "First Cutoff")]  // Exact match first
    [InlineData(16, 30, "Second Cutoff")] // One day after first
    [InlineData(31, 30, "Second Cutoff")] // Fallback to last
    public void GetCurrentCutoff_SemiMonthly_ShouldResolveCorrectBracket(int day, int expectedDay, string expectedLabel)
    {
        // Arrange
        var configs = new List<CutoffModel>
        {
            new() { Day = 15, Label = "First Cutoff" },
            new() { Day = 30, Label = "Second Cutoff" }
        };

        var context = CreateMockContext(
            new DateOnly(2025, 1, day),
            new DateOnly(2025, 1, day),
            configs,
            PayrollFrequency.SEMI_MONTHLY);

        // Act
        var result = _sut.GetCurrentCutoff(context);

        // Assert
        result.Day.Should().Be(expectedDay);
        result.Label.Should().Be(expectedLabel);
    }

    [Fact]
    public void IsSecondCutoff_TwoFixedDates_CrossMonth_ShouldBeTrue()
    {
        // Arrange: Fixed cutoffs on the 5th and 20th
        var configs = new List<CutoffModel>
        {
            new() { Day = 5, IsEndOfMonth = false },
            new() { Day = 20, IsEndOfMonth = false }
        };

        var context = CreateMockContext(
            new DateOnly(2025, 1, 21),
            new DateOnly(2025, 2, 5),
            configs, PayrollFrequency.SEMI_MONTHLY);

        // Act & Assert
        // Start date is 21. 21 > 5. Identified as second cutoff.
        _sut.IsFirstCutoff(context).Should().BeFalse();
        _sut.IsSecondCutoff(context).Should().BeTrue();
    }

    [Fact]
    public void GetCurrentCutoff_OneFixedDate_CrossMonth_ShouldReturnThatDate()
    {
        // Arrange: Only one cutoff per month on the 10th
        var configs = new List<CutoffModel>
        {
            new() { Day = 10, IsEndOfMonth = false, Label = "Monthly" }
        };

        // Period: Jan 11 to Feb 10
        var context = CreateMockContext(
            new DateOnly(2025, 1, 11),
            new DateOnly(2025, 2, 10),
            configs, PayrollFrequency.MONTHLY);

        // Act
        var result = _sut.GetCurrentCutoff(context);

        // Assert: Monthly frequency always returns the first/only cutoff.
        result.Day.Should().Be(10);
        _sut.IsFirstCutoff(context).Should().BeTrue();
    }

    [Fact]
    public void IsFirstCutoff_DailyFrequency_ShouldReturnTrue()
    {
        // Arrange
        var configs = new List<CutoffModel> { new() { Day = 1 } };
        var context = CreateMockContext(
            new DateOnly(2025, 1, 10),
            new DateOnly(2025, 1, 10),
            configs,
            PayrollFrequency.DAILY);

        // Act & Assert
        _sut.IsFirstCutoff(context).Should().BeTrue();
    }

    [Fact]
    public void IsLastCutoff_WeeklyWithFiveWeeks_ShouldOnlyBeTrueOnLastWeek()
    {
        // Arrange: 5 Weekly cutoffs configured
        var configs = new List<CutoffModel>
    {
        new() { Day = 7 }, new() { Day = 14 }, new() { Day = 21 },
        new() { Day = 28 }, new() { Day = 31, IsEndOfMonth = true }
    };

        // Case A: Week 3 (Day 21)
        var contextMid = CreateMockContext(
            new DateOnly(2025, 1, 21), new DateOnly(2025, 1, 21),
            configs, PayrollFrequency.WEEKLY);

        // Case B: Week 5 (Day 31)
        var contextLast = CreateMockContext(
            new DateOnly(2025, 1, 31), new DateOnly(2025, 1, 31),
            configs, PayrollFrequency.WEEKLY);

        // Act & Assert
        _sut.IsLastCutoff(contextMid).Should().BeFalse("Day 21 is not the last week");
        _sut.IsLastCutoff(contextLast).Should().BeTrue("Day 31 is the last week");
    }

    [Fact]
    public void IsLastCutoff_SemiMonthly_ShouldBeTrueOnSecondCutoff()
    {
        // Arrange: 15th and 30th
        var configs = new List<CutoffModel> { new() { Day = 15 }, new() { Day = 30 } };
        var context = CreateMockContext(
            new DateOnly(2025, 1, 20), new DateOnly(2025, 1, 20),
            configs, PayrollFrequency.SEMI_MONTHLY);

        // Act
        var result = _sut.IsLastCutoff(context);

        // Assert
        result.Should().BeTrue("In semi-monthly, any day after the first cutoff is the last cutoff");
    }
}