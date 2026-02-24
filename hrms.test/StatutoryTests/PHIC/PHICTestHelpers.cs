using Hrms.Domain;
using Hrms.Domain.ValueObjects;

namespace hrms.test;

public class PHICTestHelpers : StatutoryTestContextBase
{
    public static DeductionPayloadContext BuildContext(
      DateOnly fromDate,
      DateOnly toDate,
      SalaryType salaryType,
      PayrollFrequency payrollFrequency,
      ComputationBasis computationBasis,
      decimal monthlyRate,
      decimal dailyRate,
      decimal grossPay)
    {
        var context = CreateContext(fromDate, toDate, salaryType, payrollFrequency, computationBasis, monthlyRate, dailyRate, grossPay);
        context.Employee.PHICRate = new CreatePHICRate
        {
            ComputationType = computationBasis,
            EE = 100,
            ER = 200,
        };
        return context;
    }

    public static void SetContributions(DeductionPayloadContext context, decimal ee, decimal er, decimal ec)
    {
        var contributions = new List<PHICContributionModel>();
        if (!context.Payload.PHICContribution.TryGetValue(new EmployeeKey(context.Employee.Id), out contributions))
        {
            contributions = new List<PHICContributionModel>();
        }
        contributions.Add(new PHICContributionModel
        {
            EmployeeShare = ee,
            EmployerShare = er,
        });
    }
}