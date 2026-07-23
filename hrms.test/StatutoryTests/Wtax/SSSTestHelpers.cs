using Hrms.Domain;
using Hrms.Domain.ValueObjects;

namespace hrms.test;

public class WTaxTestHelpers : StatutoryTestContextBase
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
        context.Employee.TaxRate = new CreateTaxRate
        {
            ComputationType = computationBasis,
            EE = 100
        };
        return context;
    }

    public static void SetContributions(DeductionPayloadContext context, decimal taxDue)
    {
        var contributions = new List<WTaxContributionModel>();
        if (!context.Payload.TaxContribution.TryGetValue(new EmployeeKey(context.Employee.Id), out contributions))
        {
            contributions = new List<WTaxContributionModel>();
        }
        contributions.Add(new WTaxContributionModel
        {
            TaxDue = taxDue
        });
    }
}