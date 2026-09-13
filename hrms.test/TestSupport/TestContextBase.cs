namespace hrms.test.TestSupport;

/// <summary>
/// Shared builder for a DeductionPayloadContext built entirely from plain POCOs — no DB,
/// no mocking library, matching this codebase's existing (and only) test convention. Every
/// scenario axis (SalaryType, PayrollFrequency, ComputationBasis, cutoff configuration,
/// StatutoryDeductionSchedule, cross-month periods, prior contributions/gross) is set via
/// explicit helper calls so each test's Arrange step reads as exactly what it's exercising.
/// </summary>
public abstract class TestContextBase
{
    protected static Guid NewEmployeeId() => Guid.NewGuid();

    protected static DeductionPayloadContext CreateContext(
        SalaryType salaryType,
        PayrollFrequency frequency,
        decimal grossIncome,
        DateOnly fromDate,
        DateOnly toDate,
        decimal monthlyRate = 30000m,
        DateOnly? hireDate = null,
        StatutoryDeductionSchedule schedule = StatutoryDeductionSchedule.PerPayroll,
        Guid? employeeId = null)
    {
        var id = employeeId ?? NewEmployeeId();
        var employee = new EmployeeModelPayrollRun
        {
            Id = id,
            FullName = "Test Employee",
            SalaryType = salaryType,
            PayrollFrequency = frequency,
            MonthlyRate = monthlyRate,
            DailyRate = Math.Round(monthlyRate / 26m, 2),
            HireDate = hireDate ?? new DateOnly(2020, 1, 1),
            PayrollGroup = new PayrollGroupModel
            {
                PayrollFrequency = frequency,
                StatutoryDeductionSchedule = schedule,
                CutoffDays = new List<CutoffModel>(),
            },
        };

        var payrollLine = new PayrollSummaryLine
        {
            EmployeeId = id,
            PayPeriodStart = fromDate,
            PayPeriodEnd = toDate,
            GrossIncome = grossIncome,
        };

        var payload = new CalculatorPayload
        {
            FromDate = fromDate,
            ToDate = toDate,
            CompanyPolicy = new CompanyPolicyRule(),
        };

        return new DeductionPayloadContext
        {
            Employee = employee,
            Payload = payload,
            PayrollLine = payrollLine,
        };
    }

    // --- Cutoff configuration ---------------------------------------------------------

    protected static void AddCutoff(DeductionPayloadContext context, int day, bool isEndOfMonth = false, string label = "")
        => context.Employee.PayrollGroup!.CutoffDays!.Add(new CutoffModel { Day = day, IsEndOfMonth = isEndOfMonth, Label = label });

    // --- Prior-this-month gross/payrolls (for Variable Get*GrossBaseRate accumulation) -

    protected static void AddPriorPayroll(DeductionPayloadContext context, decimal priorGrossIncome)
    {
        var key = new EmployeeKey(context.Employee.Id);
        if (!context.Payload.PostedPriorPayrolls.TryGetValue(key, out var list))
        {
            list = new List<Payroll>();
            context.Payload.PostedPriorPayrolls[key] = list;
        }
        list.Add(new Payroll { EmployeeId = context.Employee.Id, GrossIncome = priorGrossIncome });
    }

    // --- One-Time Leave Payout (maternity-style lump sum) -------------------------------

    // Marks the employee as having a OneTime-payout leave (StatutoryHelper.IsOneTimePayLeave)
    // releasing within [context.Payload.FromDate, ToDate] — the statutory Get*BracketBaseRate
    // methods then bracket off the period's actual GrossIncome instead of a FIXED projection
    // or Variable accumulation, and the Table*Calculators release the full bracket amount
    // immediately instead of splitting/prorating it. releasePayrollDate defaults to the
    // context's own FromDate (i.e. "releases within this period") — pass an out-of-range date
    // to build a non-matching payout for a negative test.
    protected static void AddOneTimeLeavePayout(DeductionPayloadContext context, DateOnly? releasePayrollDate = null)
    {
        var key = new EmployeeKey(context.Employee.Id);
        if (!context.Payload.OneTimeLeavePayouts.TryGetValue(key, out var list))
        {
            list = new List<LeaveApplication>();
            context.Payload.OneTimeLeavePayouts[key] = list;
        }
        list.Add(new LeaveApplication
        {
            Leave = new Leave { Description = "Test One-Time Leave Payout" },
            PayoutMode = PayoutMode.OneTime,
            ReleasePayrollDate = releasePayrollDate ?? context.Payload.FromDate,
        });
    }

    // --- SSS ----------------------------------------------------------------------------

    protected static void SetSSSRate(DeductionPayloadContext context, ComputationBasis basis, decimal ee = 0, decimal er = 0, decimal ec = 0)
        => context.Employee.SSSRate = new CreateSSSRate { ComputationType = basis, EE = ee, ER = er, EC = ec };

    protected static void SeedSSSTable(DeductionPayloadContext context, params SSSModel[] brackets)
        => context.Payload.SSSTableModel = brackets.ToList();

    protected static SSSModel SSSBracket(decimal from, decimal to, decimal ee, decimal er, decimal ec) =>
        new SSSModel { RangeFrom = from, RangeTo = to, EE = ee, ER = er, EC = ec };

    protected static void AddSSSContribution(DeductionPayloadContext context, decimal ee, decimal er, decimal ec)
    {
        var key = new EmployeeKey(context.Employee.Id);
        if (!context.Payload.SSSContribution.TryGetValue(key, out var list))
        {
            list = new List<SSSContributionModel>();
            context.Payload.SSSContribution[key] = list;
        }
        list.Add(new SSSContributionModel { EmployeeId = context.Employee.Id, EE = ee, ER = er, EC = ec });
    }

    // --- PHIC ---------------------------------------------------------------------------

    protected static void SetPHICRate(DeductionPayloadContext context, ComputationBasis basis, decimal ee = 0, decimal er = 0)
        => context.Employee.PHICRate = new CreatePHICRate { ComputationType = basis, EE = ee, ER = er };

    protected static void SeedPHICTable(DeductionPayloadContext context, params PHICModel[] brackets)
        => context.Payload.PHICTableModel = brackets.ToList();

    protected static PHICModel PHICBracket(decimal min, decimal max, decimal ee, decimal er) =>
        new PHICModel { MinSalaryBase = min, MaxSalaryBase = max, EmployeeShare = ee, EmployerShare = er };

    protected static void AddPHICContribution(DeductionPayloadContext context, decimal ee, decimal er)
    {
        var key = new EmployeeKey(context.Employee.Id);
        if (!context.Payload.PHICContribution.TryGetValue(key, out var list))
        {
            list = new List<PHICContributionModel>();
            context.Payload.PHICContribution[key] = list;
        }
        list.Add(new PHICContributionModel { EmployeeId = context.Employee.Id, EmployeeShare = ee, EmployerShare = er });
    }

    // --- HDMF ---------------------------------------------------------------------------

    protected static void SetHDMFRate(DeductionPayloadContext context, ComputationBasis basis, decimal ee = 0, decimal er = 0)
        => context.Employee.HDMFRate = new CreateHDMFRate { ComputationType = basis, EE = ee, ER = er };

    protected static void SeedHDMFTable(DeductionPayloadContext context, params HDMFModel[] brackets)
        => context.Payload.HDMFTableModel = brackets.ToList();

    protected static HDMFModel HDMFBracket(decimal min, decimal max, decimal ee, decimal er) =>
        new HDMFModel { MinSalaryBase = min, MaxSalaryBase = max, EmployeeShare = ee, EmployerShare = er };

    protected static void AddHDMFContribution(DeductionPayloadContext context, decimal ee, decimal er)
    {
        var key = new EmployeeKey(context.Employee.Id);
        if (!context.Payload.HDMFContribution.TryGetValue(key, out var list))
        {
            list = new List<HDMFContributionModel>();
            context.Payload.HDMFContribution[key] = list;
        }
        list.Add(new HDMFContributionModel { EmployeeId = context.Employee.Id, EmployeeShare = ee, EmployerShare = er });
    }

    // --- Client statutory capping (Setup > Client > Settings > Statutory Capping) -------

    protected static void SetClientStatutoryCap(DeductionPayloadContext context, StatutoryCapType type, decimal cap)
    {
        context.Employee.ClientId ??= Guid.NewGuid();
        context.Payload.ClientStatutoryCaps[new ClientStatutoryCapKey(context.Employee.ClientId.Value, type)] = cap;
    }

    // --- Minimum Take-Home Pay (Setup > Company Policy > Minimum Take-Home Pay) --------

    protected static void SetRequiredTakehomePercentage(DeductionPayloadContext context, decimal percentage)
        => context.Payload.CompanyPolicy.RequiredTakehomePercentage = percentage;

    // --- WTax -----------------------------------------------------------------------------

    protected static void SetTaxRate(DeductionPayloadContext context, ComputationBasis basis, decimal ee = 0)
        => context.Employee.TaxRate = new CreateTaxRate { ComputationType = basis, EE = ee };

    protected static void SeedTaxTable(DeductionPayloadContext context, params WTaxModel[] brackets)
        => context.Payload.TaxTableModel = brackets.ToList();

    // payrollType is required (no default) — WTaxHelper.GetTable now filters by the
    // employee's own PayrollFrequency, so a mislabeled bracket row simply never matches
    // rather than silently reusing whatever the default happened to be.
    protected static WTaxModel TaxBracket(decimal from, decimal to, decimal baseTaxDue, decimal addOnPercentage, string payrollType) =>
        new WTaxModel { RangeFrom = from, RangeTo = to, BaseTaxDue = baseTaxDue, AddOnPercentage = addOnPercentage, PayrollType = payrollType };
}
