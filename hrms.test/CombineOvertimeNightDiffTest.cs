using Hrms.Core.Pipelines.DTRPipelines;
using Hrms.Domain;
using Hrms.Domain.ValueObjects;
using Xunit;

namespace hrms.test
{
    public class CombineOvertimeNightDiffTest
    {
        [Fact]
        public void ShouldComputeNDOT_WhenRegularWorkDay()
        {
            var context = CombinedTestHelpers.CreateCombinedPayrollContext();
            context.DailyRecord = new DailyRecordRunModel
            {
                RegularOTHours = 2,
                RegularNDOTHours = 2
            };
            context.WorkType = WorkType.RegularWorkDay;
            var pipeline = new CombinedOTAndNightDiffPipeline();
            var result = pipeline.Run(context);
            Assert.Equal(337.5m, result.Value);
        }
    }

    public static class CombinedTestHelpers
    {
        public static PayrollContext CreateCombinedPayrollContext()
        {
            var empId = Guid.NewGuid();

            return new PayrollContext
            {
                Employee = new EmployeeModelPayrollRun
                {
                    Id = empId,
                    DailyRate = 1_000,
                    Settings = new EmployeeSettingModel { IsEligibleForNightDifferential = true, IsEligibleForOvertime = true }
                },
                PayrollDate = DateOnly.FromDateTime(DateTime.Today),
                Payload = new CalculatorPayload
                {
                    Leaves = new Dictionary<EmployeePayDateKey, List<LeaveApplicationPyRun>>(),
                    LeaveCredits = new Dictionary<EmployeeLeaveCreditsKey, decimal>(),
                    PremiumRates = TestRateProvider.GetDefaultPremiumRates(),
                }
            };
        }
    }
}
