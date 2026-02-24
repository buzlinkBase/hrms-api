using Hrms.Domain;
using Hrms.Domain.ValueObjects;

namespace hrms.test;

public class SSSTestHelpers : StatutoryTestContextBase
{

    public static DeductionPayloadContext BuildContext(
      DateOnly fromDate,
      DateOnly toDate,
      SalaryType salaryType,
      PayrollFrequency payrollFrequency,
      ComputationBasis computationBasis,
      decimal monthlyRate,
      decimal dailyRate,
      decimal grossPay
  )
    {
        var context = CreateContext(fromDate, toDate, salaryType, payrollFrequency, computationBasis, monthlyRate, dailyRate, grossPay);
        context.Employee.SSSRate = new CreateSSSRate
        {
            ComputationType = computationBasis,
            EE = 100,
            ER = 200,
            EC = 10,
        };
        return context;
    }

    public static void SetContributions(DeductionPayloadContext context, decimal ee, decimal er, decimal ec)
    {
        var contributions = new List<SSSContributionModel>();
        if (!context.Payload.SSSContribution.TryGetValue(new EmployeeKey(context.Employee.Id), out contributions))
        {
            contributions = new List<SSSContributionModel>();
        }
        contributions.Add(new SSSContributionModel
        {
            EE = ee,
            ER = er,
            EC = ec
        });
    }
}