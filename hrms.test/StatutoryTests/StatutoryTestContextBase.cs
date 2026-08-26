using Hrms.Domain;
using Hrms.Domain.ValueObjects;

namespace hrms.test;

public class StatutoryTestContextBase
{
    public static DeductionPayloadContext CreateContext(
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
        var payrollLine = new PayrollSummaryLine
        {
            BasicSalary = monthlyRate,
            GrossIncome = grossPay,
            PayrollDate = toDate,
            BasicSalaryItems = new List<DTRPayModel>
            {
                new DTRPayModel {
                    BasicPay = monthlyRate ,
                    TimeBaseGross = grossPay,
                },
            }
        };
        var employee = new EmployeeModelPayrollRun
        {
            Id = Guid.Empty,
            SalaryType = salaryType,
            PayrollFrequency = payrollFrequency,
            MonthlyRate = monthlyRate,
            DailyRate = dailyRate,
            PayrollGroup = new PayrollGroupModel
            {
                PayrollFrequency = payrollFrequency,
                CutoffDays = new List<CutoffModel>()
            },
        };
        var context = new DeductionPayloadContext
        {
            Employee = employee,
            PayrollLine = payrollLine,
            Payload = new CalculatorPayload
            {
                FromDate = fromDate,
                ToDate = toDate,
                CompanyPolicy = new CompanyPolicyRule(),
                HDMFContribution = new Dictionary<EmployeeKey, List<HDMFContributionModel>>()
                {
                    { new EmployeeKey(employee.Id), new List<HDMFContributionModel>() }
                },
                PHICContribution = new Dictionary<EmployeeKey, List<PHICContributionModel>>()
                {
                    { new EmployeeKey(employee.Id), new List<PHICContributionModel>() }
                },
                SSSContribution = new Dictionary<EmployeeKey, List<SSSContributionModel>>
                {
                    { new EmployeeKey(employee.Id), new List<SSSContributionModel>() }
                },
                Incomes = new Dictionary<EmployeeKey, List<OtherIncomeInfo>>(),
                TaxTableModel = new List<WTaxModel>
                {
                    new WTaxModel
                    {
                        RangeFrom=0,
                        RangeTo=25000,
                        BaseTaxDue=0,
                        AddOnPercentage=0,
                    },
                    new WTaxModel
                    {
                        RangeFrom=25001,
                        RangeTo=40_000,
                        BaseTaxDue=1000,
                        AddOnPercentage=5,
                    },
                },
                SSSTableModel = new List<SSSModel>
                {
                    new SSSModel
                    {
                        RangeFrom = 0,
                        RangeTo = 9_999,
                        MSC=8000,
                        EE = 100,
                        ER = 200,
                        EC = 10,
                        EffectiveDate = fromDate
                    },
                    new SSSModel
                    {
                        RangeFrom = 10_000,
                        RangeTo = 20_000,
                        MSC=15000,
                        EE = 500,
                        ER = 600,
                        EC = 20,
                        EffectiveDate = fromDate
                    },
                    new SSSModel
                    {
                        RangeFrom = 20_001,
                        RangeTo = 99_000,
                        MSC=50000,
                        EE = 1000,
                        ER = 2000,
                        EC = 50,
                        EffectiveDate = fromDate
                    }
                },
                PHICTableModel = new List<PHICModel>
                {
                    new PHICModel
                    {
                        MinSalaryBase = 0,
                        MaxSalaryBase = 9_999,
                        EmployeeShare = 100,
                        EmployerShare = 200,
                        EffectiveDate = fromDate
                    },
                    new PHICModel
                    {
                        MinSalaryBase = 10_000,
                        MaxSalaryBase = 20_000,
                        EmployeeShare = 500,
                        EmployerShare = 600,
                        EffectiveDate = fromDate
                    },
                    new PHICModel
                    {
                        MinSalaryBase = 20_001,
                        MaxSalaryBase = 99_000,
                        EmployeeShare = 1000,
                        EmployerShare = 2000,
                        EffectiveDate = fromDate
                    }
                },
                HDMFTableModel = new List<HDMFModel>
                {
                    new HDMFModel
                    {
                        MinSalaryBase = 0,
                        MaxSalaryBase = 9_999,
                        EmployeeShare = 100,
                        EmployerShare = 200,
                        EffectiveDate = fromDate
                    },
                    new HDMFModel
                    {
                        MinSalaryBase = 10_000,
                        MaxSalaryBase = 20_000,
                        EmployeeShare = 500,
                        EmployerShare = 600,
                        EffectiveDate = fromDate
                    },
                    new HDMFModel
                    {
                        MinSalaryBase = 20_001,
                        MaxSalaryBase = 99_000,
                        EmployeeShare = 1000,
                        EmployerShare = 2000,
                        EffectiveDate = fromDate
                    }
                }

            }
        };
        return context;
    }

    public static void SetOtherIncome(DeductionPayloadContext context, decimal totalOtherIncome)
    {
        context.PayrollLine.TotalOtherIncome = totalOtherIncome;
        context.PayrollLine.GrossIncome += totalOtherIncome;
    }
    public static void SetCutoff(DeductionPayloadContext context, int day = 31, bool endOfMonth = false)
    {
        if (context.Employee.PayrollGroup == null) return;
        context.Employee.PayrollGroup.CutoffDays.Add(new CutoffModel
        {
            Day = day,
            IsEndOfMonth = endOfMonth,
        });
    }
}
