using Hrms.Domain;
using Hrms.Domain.ValueObjects;
using Xunit;

namespace hrms.test
{
    public class OvertimePolicyTests
    {

        [Fact]
        public void ShouldComputeOT_WhenRegularOTHoursSet()
        {
            var context = OvertimeTestHelpers.CreatePayrollContext(WorkType.RegularWorkDay, 1000);
            context.DailyRecord = new DailyRecordRunModel { RegularOTHours = 2 };
            var pipe = new OvertimePipeline();
            var result = pipe.Run(context);

            Assert.NotNull(result.OTInfo);
            Assert.Equal(2m, result.OTInfo.Hour);
            Assert.Equal("RegularWorkDayOT", result.OTInfo.Handler);
            Assert.True(result.OTInfo.Amount > 0);
            Assert.Equal(result.Value, result.OTInfo.Amount);
            Assert.Equal(312.5m, result.Value);
        }

        [Fact]
        public void ShouldComputeOT_WhenRestDayOTHoursSet()
        {
            var context = OvertimeTestHelpers.CreatePayrollContext(WorkType.RestDayDuty, 1000);
            context.DailyRecord = new DailyRecordRunModel { RestDayOTHours = 2 };
            var pipe = new OvertimePipeline();
            var result = pipe.Run(context);

            Assert.NotNull(result.OTInfo);
            Assert.Equal(2m, result.OTInfo.Hour);
            Assert.Equal("RestDayOT", result.OTInfo.Handler);
            Assert.True(result.OTInfo.Amount > 0);
            Assert.Equal(result.Value, result.OTInfo.Amount);
            Assert.Equal(406.25m, result.Value);
        }

        [Fact]
        public void ShouldComputeOT_WhenLegalHolOTHoursSet()
        {
            var context = OvertimeTestHelpers.CreatePayrollContext(WorkType.LegalHolidayDuty, 1000);
            context.DailyRecord = new DailyRecordRunModel { LegalHolOTHours = 2 };
            var pipe = new OvertimePipeline();
            var result = pipe.Run(context);

            Assert.NotNull(result.OTInfo);
            Assert.Equal(2m, result.OTInfo.Hour);
            Assert.Equal("LegalHolOT", result.OTInfo.Handler);
            Assert.True(result.OTInfo.Amount > 0);
            Assert.Equal(result.Value, result.OTInfo.Amount);
            Assert.Equal(625m, result.Value);
        }

        [Fact]
        public void ShouldComputeOT_WhenRestDayLegalHolidayDutySet()
        {
            var context = OvertimeTestHelpers.CreatePayrollContext(WorkType.RestDayLegalHolidayDuty, 1000);
            context.DailyRecord = new DailyRecordRunModel { LegalHolOTHours = 2 };
            var pipe = new OvertimePipeline();
            var result = pipe.Run(context);

            Assert.NotNull(result.OTInfo);
            Assert.Equal(2m, result.OTInfo.Hour);
            Assert.Equal("LegalRestOT", result.OTInfo.Handler);
            Assert.True(result.OTInfo.Amount > 0);
            Assert.Equal(result.Value, result.OTInfo.Amount);
            Assert.Equal(812.5m, result.Value);
        }

        [Fact]
        public void ShouldComputeOT_WhenSpecialHolidaySet()
        {
            var context = OvertimeTestHelpers.CreatePayrollContext(WorkType.SpecialHolidayDuty, 1000);
            context.DailyRecord = new DailyRecordRunModel { SpecialHolOTHours = 2 };
            var pipe = new OvertimePipeline();
            var result = pipe.Run(context);

            Assert.NotNull(result.OTInfo);
            Assert.Equal(2m, result.OTInfo.Hour);
            Assert.Equal("SpecialOTWorking", result.OTInfo.Handler);
            Assert.True(result.OTInfo.Amount > 0);
            Assert.Equal(result.Value, result.OTInfo.Amount);
            Assert.Equal(312.5m, result.Value);
        }

        [Fact]
        public void ShouldComputeOT_WhenSpecialHolidayDutyNWSet()
        {
            var context = OvertimeTestHelpers.CreatePayrollContext(WorkType.SpecialHolidayDuty, 1000);
            context.DailyRecord = new DailyRecordRunModel { SpecialHolOTHours = 2 };
            var pipe = new OvertimePipeline();
            var result = pipe.Run(context);

            Assert.NotNull(result.OTInfo);
            Assert.Equal(2m, result.OTInfo.Hour);
            Assert.Equal("SpecialOTNonworking", result.OTInfo.Handler);
            Assert.True(result.OTInfo.Amount > 0);
            Assert.Equal(result.Value, result.OTInfo.Amount);
            Assert.Equal(406.25m, result.Value);
        }

        [Fact]
        public void ShouldComputeOT_WhenRestDaySpecialHolidayDutyNWSet()
        {
            var context = OvertimeTestHelpers.CreatePayrollContext(WorkType.RestDaySpecialHolidayDuty, 1000);
            context.DailyRecord = new DailyRecordRunModel { SpecialHolOTHours = 2 };
            var pipe = new OvertimePipeline();
            var result = pipe.Run(context);

            Assert.NotNull(result.OTInfo);
            Assert.Equal(2m, result.OTInfo.Hour);
            Assert.Equal("SpecialRestOT", result.OTInfo.Handler);
            Assert.True(result.OTInfo.Amount > 0);
            Assert.Equal(result.Value, result.OTInfo.Amount);
            Assert.Equal(468.75m, result.Value);
        }
    }

    public class OvertimeTestHelpers
    {
        public static PayrollContext CreatePayrollContext(WorkType workType, decimal dailyRate = 1_000)
        {
            var empId = Guid.Empty;
            return new PayrollContext
            {
                WorkType = workType,
                Employee = new EmployeeModelPayrollRun
                {
                    Id = empId,
                    DailyRate = dailyRate,
                    Settings = new EmployeeSettingModel { IsEligibleForOvertime = true, IsEligibleForNightDifferential = true }
                },
                PayrollDate = DateOnly.FromDateTime(DateTime.Today),
                Payload = new CalculatorPayload
                {
                    Leaves = new Dictionary<EmployeePayDateKey, List<LeaveApplicationPyRun>>(),
                    LeaveCredits = new Dictionary<EmployeeLeaveCreditsKey, decimal>(),
                    PremiumRates = TestRateProvider.GetDefaultPremiumRates(),
                },
            };
        }
    }
}
