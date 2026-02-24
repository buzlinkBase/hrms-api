using Hrms.Domain;
using Hrms.Domain.ValueObjects;

namespace hrms.test;

public class HDMFTestHelpers : StatutoryTestContextBase
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
        context.Employee.HDMFRate = new CreateHDMFRate
        {
            ComputationType = computationBasis,
            EE = 100,
            ER = 200,
        };
        return context;
    }

    public static void SetContributions(DeductionPayloadContext context, decimal ee, decimal er, decimal ec)
    {
        var contributions = new List<HDMFContributionModel>();
        if (!context.Payload.HDMFContribution.TryGetValue(new EmployeeKey(context.Employee.Id), out contributions))
        {
            contributions = new List<HDMFContributionModel>();
        }
        contributions.Add(new HDMFContributionModel
        {
            EmployeeShare = ee,
            EmployerShare = er,
        });
    }
}