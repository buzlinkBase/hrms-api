using Hrms.Domain;
using Hrms.Domain.ValueObjects;
using Xunit;

namespace hrms.test
{
    public class NightDiffPolicyTests
    {

        [Fact]
        public void ShouldComputeOT_WhenRegularOTHoursSet()
        {

            var context = NightDiffTestHelpers.CreatePayrollContext(WorkType.RegularWorkDay);
            context.DailyRecord = new DailyRecordRunModel { RegularNDHours = 2 };
            var pipe = new NightDiffPipeline();
            var result = pipe.Run(context);
            Assert.NotNull(result.NightDiffInfo);
            Assert.Equal(2m, result.NightDiffInfo.Hour);
            Assert.True(result.NightDiffInfo.Amount > 0);
            Assert.Equal(result.Value, result.NightDiffInfo.Amount);
            Assert.Equal(25m, result.Value);

        }

        [Fact]
        public void ShouldComputeOT_WhenRegularNDOTHoursSet()
        {
            var context = NightDiffTestHelpers.CreatePayrollContext(WorkType.RegularWorkDay);
            context.DailyRecord = new DailyRecordRunModel { RegularNDOTHours = 2 };
            var pipe = new NightDiffPipeline();
            var result = pipe.Run(context);
            Assert.NotNull(result.NightDiffInfo);
            Assert.Equal(2m, result.NightDiffInfo.Hour);
            Assert.True(result.NightDiffInfo.Amount > 0);
            Assert.Equal(result.Value, result.NightDiffInfo.Amount);
            Assert.Equal(25m, result.Value);
        }

        [Fact]
        public void ShouldComputeOT_WhenRestDayDutySet()
        {

            var context = NightDiffTestHelpers.CreatePayrollContext(WorkType.RestDayDuty, 800);
            context.DailyRecord = new DailyRecordRunModel { RestDayNDHours = 2 };
            var pipe = new NightDiffPipeline();
            var result = pipe.Run(context);
            Assert.NotNull(result.NightDiffInfo);
            Assert.Equal(2m, result.NightDiffInfo.Hour);
            Assert.True(result.NightDiffInfo.Amount > 0);
            Assert.Equal(result.Value, result.NightDiffInfo.Amount);
            Assert.Equal(20m, result.Value);
        }

        [Fact]
        public void ShouldComputeOT_WhenRestDayNDOTHoursSet()
        {

            var context = NightDiffTestHelpers.CreatePayrollContext(WorkType.RestDayDuty, 800);
            context.DailyRecord = new DailyRecordRunModel { RestDayNDOTHours = 1 };
            var pipe = new NightDiffPipeline();

            var result = pipe.Run(context);
            Assert.NotNull(result.NightDiffInfo);
            Assert.Equal(1m, result.NightDiffInfo.Hour);
            Assert.True(result.NightDiffInfo.Amount > 0);
            Assert.Equal(result.Value, result.NightDiffInfo.Amount);
            Assert.Equal(10m, result.Value);

        }

        [Fact]
        public void ShouldComputeND_WhenRegularNDHoursSet()
        {
            var context = NightDiffTestHelpers.CreatePayrollContext(WorkType.RegularWorkDay, 1000);
            context.DailyRecord = new DailyRecordRunModel { RegularNDHours = 2 };
            var pipe = new NightDiffPipeline();
            var result = pipe.Run(context);

            Assert.NotNull(result.NightDiffInfo);
            Assert.Equal(2m, result.NightDiffInfo.Hour);
            Assert.True(result.NightDiffInfo.Amount > 0);
            Assert.Equal(result.Value, result.NightDiffInfo.Amount);

            // 2 × 125 × 0.10 = 25
            Assert.Equal(25m, result.Value);
        }

        [Fact]
        public void ShouldComputeND_WhenRegularNDOTHoursSet()
        {
            var context = NightDiffTestHelpers.CreatePayrollContext(WorkType.RegularWorkDay, 1000);
            context.DailyRecord = new DailyRecordRunModel { RegularNDOTHours = 2 };
            var pipe = new NightDiffPipeline();
            var result = pipe.Run(context);

            Assert.NotNull(result.NightDiffInfo);
            Assert.Equal(2m, result.NightDiffInfo.Hour);
            Assert.True(result.NightDiffInfo.Amount > 0);
            Assert.Equal(result.Value, result.NightDiffInfo.Amount);

            // ND premium only, even if OT: 2 × 125 × 0.10 = 25
            Assert.Equal(25m, result.Value);
        }

        [Fact]
        public void ShouldComputeND_WhenRestDayNDHoursSet()
        {
            var context = NightDiffTestHelpers.CreatePayrollContext(WorkType.RestDayDuty, 1000);
            context.DailyRecord = new DailyRecordRunModel { RestDayNDHours = 1 };
            var pipe = new NightDiffPipeline();
            var result = pipe.Run(context);

            Assert.NotNull(result.NightDiffInfo);
            Assert.Equal(1m, result.NightDiffInfo.Hour);
            Assert.True(result.NightDiffInfo.Amount > 0);
            Assert.Equal(result.Value, result.NightDiffInfo.Amount);

            // 1 × 125 × 0.10 = 12.5
            Assert.Equal(12.5m, result.Value);
        }

        [Fact]
        public void ShouldComputeND_WhenRestDayNDOTHoursSet()
        {
            var context = NightDiffTestHelpers.CreatePayrollContext(WorkType.RestDayDuty, 1000);
            context.DailyRecord = new DailyRecordRunModel { RestDayNDOTHours = 1 };
            var pipe = new NightDiffPipeline();
            var result = pipe.Run(context);

            Assert.NotNull(result.NightDiffInfo);
            Assert.Equal(1m, result.NightDiffInfo.Hour);
            Assert.True(result.NightDiffInfo.Amount > 0);
            Assert.Equal(result.Value, result.NightDiffInfo.Amount);

            // ND premium only, even if OT: 1 × 125 × 0.10 = 12.5
            Assert.Equal(12.5m, result.Value);
        }
    }

    public class NightDiffTestHelpers : OvertimeTestHelpers
    {

    }
}
