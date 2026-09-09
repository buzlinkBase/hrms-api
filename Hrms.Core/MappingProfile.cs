using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;

namespace Hrms.Core;

public class MappingProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        TypeAdapterConfig<NetTopologySuite.Geometries.Polygon, NetTopologySuite.Geometries.Polygon>.NewConfig()
            .MapWith(src => src);

        config.NewConfig<DateTime, DateOnly>()
            .MapWith(src => DateOnly.FromDateTime(src));

        // Handle nullable DateTimes as well
        config.NewConfig<DateTime?, DateOnly?>()
            .MapWith(src => src.HasValue ? DateOnly.FromDateTime(src.Value) : null);

        config.NewConfig<DateOnly, DateTime>()
            .MapWith(src => src.ToDateTime(TimeOnly.MinValue));

        config.NewConfig<DateOnly?, DateTime?>()
            .MapWith(src => src.HasValue ? src.Value.ToDateTime(TimeOnly.MinValue) : null);

        config.NewConfig<PayrollSummaryLine, Payroll>();
        config.NewConfig<Payroll, PayrollSummaryLine>();
        config.NewConfig<CreateDepartment, Department>().TwoWays();
        config.NewConfig<UpdateDepartment, Department>();

        config.NewConfig<CreateSalaryAdjustment, SalaryAdjustment>();
        config.NewConfig<UpdateSalaryAdjustment, SalaryAdjustment>();
        config.NewConfig<SalaryAdjustment, SalaryAdjustmentModel>();

        config.NewConfig<DTRDetailModel, DailyRecord>().TwoWays();
        config.NewConfig<DailyRecord, DailyRecordRunModel>();//for payroll pipeline

        config.NewConfig<CreateEmployeeSetting, EmployeeSetting>();
        config.NewConfig<UpdateEmployeeSetting, EmployeeSetting>();
        config.NewConfig<EmployeeSetting, EmployeeSettingModel>();

        // Employee Mappings with Custom Logic
        config.NewConfig<Employee, EmployeeModelPayrollRun>()
            .Map(dest => dest.FullName, src => src.FullName())
            .Map(dest => dest.PayrollFrequency, src => src.PayrollGroup.PayrollFrequency);

        // Handling AfterMapping for Create/Update Employee
        config.NewConfig<CreateEmployee, Employee>()
            .AfterMapping((src, dest) => ApplyEmployeeReferenceFixes(dest));

        config.NewConfig<UpdateEmployee, Employee>()
            .Map(
                dest => dest.HireDate,
                src => src.HireDate.HasValue && src.HireDate.Value != DateOnly.MinValue
                    ? src.HireDate.Value
                    : DateOnly.FromDateTime(DateTime.UtcNow)
            )
            .AfterMapping((src, dest) => ApplyEmployeeReferenceFixes(dest));

        // Complex Employee to Model Mappings
        config.NewConfig<Employee, EmployeeModel>()
            .Map(dest => dest.FullName, src => src.FullName())
            .Map(dest => dest.PayrollGroupName, src => src.PayrollGroup == null ? "" : src.PayrollGroup.Name)
            .Map(dest => dest.ClientName, src => src.Client == null ? "" : src.Client.Name)
            .Map(dest => dest.PositionName, src => src.Position == null ? "" : src.Position.Name)
            .Map(dest => dest.BranchName, src => src.Branch == null ? "" : src.Branch.Name)
            .Map(dest => dest.DepartmentName, src => src.Department == null ? "" : src.Department.Name)
            .Map(dest => dest.TimeShiftName, src => src.TimeShift == null ? "" : src.TimeShift.ShiftName)
            .Map(dest => dest.AreaName, src => src.Area == null ? "" : src.Area.Name)
            .Map(dest => dest.PayrollFrequency, src => src.PayrollGroup == null ? PayrollFrequency.SEMI_MONTHLY : src.PayrollGroup.PayrollFrequency);

        config.NewConfig<Employee, EmployeeFullModel>()
             .Map(dest => dest.FullName, src => src.LastName + ", " + src.FirstName + " " + src.MiddleName + " " + src.Suffix)
            .Map(dest => dest.PayrollGroupName, src => src.PayrollGroup == null ? "" : src.PayrollGroup.Name)
            .Map(dest => dest.ClientName, src => src.Client == null ? "" : src.Client.Name)
            .Map(dest => dest.PositionName, src => src.Position == null ? "" : src.Position.Name)
            .Map(dest => dest.BranchName, src => src.Branch == null ? "" : src.Branch.Name)
            .Map(dest => dest.DepartmentName, src => src.Department == null ? "" : src.Department.Name)
            .Map(dest => dest.TimeShiftName, src => src.TimeShift == null ? "" : src.TimeShift.ShiftName)
            .Map(dest => dest.AreaName, src => src.Area == null ? "" : src.Area.Name)
            .Map(dest => dest.PayrollFrequency, src => src.PayrollGroup == null ? PayrollFrequency.SEMI_MONTHLY : src.PayrollGroup.PayrollFrequency);

        config.NewConfig<RestDayModel, RestDay>().TwoWays();

        config.NewConfig<Employee, EmployeeFilterResponseModel>()
            .Map(dest => dest.Name, src => src.LastName + ", " + src.FirstName + " " + src.MiddleName + " " + src.Suffix)
            .Map(dest => dest.PayrollGroupName, src => src.PayrollGroup.Name)
            .Map(dest => dest.ClientName, src => src.Client.Name)
            .Map(dest => dest.BranchName, src => src.Branch.Name)
            .Map(dest => dest.DepartmentName, src => src.Department.Name)
            .Map(dest => dest.AreaName, src => src.Area.Name);

        // Skill, Education, Records
        config.NewConfig<CreateSkill, Skill>();
        config.NewConfig<UpdateSkill, Skill>();
        config.NewConfig<Skill, SkillModel>();

        config.NewConfig<CreateEducation, Education>();
        config.NewConfig<UpdateEducation, Education>();
        config.NewConfig<Education, EducationModel>();

        config.NewConfig<CreateEmployeeRecord, EmployeeRecord>();
        config.NewConfig<UpdateEmployeeRecord, EmployeeRecord>();
        config.NewConfig<EmployeeRecord, EmployeeRecordModel>();

        config.NewConfig<CreateDependent, Dependent>();
        config.NewConfig<UpdateDependent, Dependent>();
        config.NewConfig<Dependent, DependentModel>();

        config.NewConfig<CreatePriorEmployerTaxRecord, PriorEmployerTaxRecord>();
        config.NewConfig<UpdatePriorEmployerTaxRecord, PriorEmployerTaxRecord>();
        config.NewConfig<PriorEmployerTaxRecord, PriorEmployerTaxRecordModel>();

        config.NewConfig<CreatePayrollOpeningBalance, PayrollOpeningBalance>();
        config.NewConfig<UpdatePayrollOpeningBalance, PayrollOpeningBalance>();
        config.NewConfig<PayrollOpeningBalance, PayrollOpeningBalanceModel>();

        config.NewConfig<CreateAssignAsset, AssignAsset>();
        config.NewConfig<UpdateAssignAsset, AssignAsset>();
        config.NewConfig<AssignAsset, AssignAssetModel>().TwoWays();

        config.NewConfig<CreateEmploymentHistory, EmploymentHistory>();
        config.NewConfig<UpdateEmploymentHistory, EmploymentHistory>();
        config.NewConfig<EmploymentHistory, EmploymentHistoryModel>();

        // Payroll & Leave
        config.NewConfig<CreatePayrollGroup, PayrollGroup>();
        config.NewConfig<UpdatePayrollGroup, PayrollGroup>();
        config.NewConfig<PayrollGroup, PayrollGroupModel>();
        config.NewConfig<CutoffModel, CutoffDay>().TwoWays();

        config.NewConfig<CreateLeave, Leave>();
        config.NewConfig<UpdateLeave, Leave>();
        config.NewConfig<Leave, LeaveModel>();

        config.NewConfig<CreateLeaveApplication, LeaveApplication>();
        config.NewConfig<UpdateLeaveApplication, LeaveApplication>();
        config.NewConfig<LeaveApplication, LeaveApplicationModel>();

        // Schedules, Holidays, OT
        config.NewConfig<CreateWorkRotationPlan, WorkSchedulePlan>();
        config.NewConfig<UpdateWorkSchedulePlan, WorkSchedulePlan>();
        config.NewConfig<WorkSchedulePlan, WorkSchedulePlanModel>();

        config.NewConfig<CreateChangeHoliday, ChangeHoliday>();
        config.NewConfig<CreateHoliday, Holiday>();
        config.NewConfig<UpdateHoliday, Holiday>();
        config.NewConfig<Holiday, HolidayModel>();

        config.NewConfig<CreateMinimumWageRate, MinimumWageRate>();
        config.NewConfig<UpdateMinimumWageRate, MinimumWageRate>();
        config.NewConfig<MinimumWageRate, MinimumWageRateModel>();

        config.NewConfig<CreateOverTimeApplication, OverTimeApplication>();
        config.NewConfig<UpdateOvertimeApplication, OverTimeApplication>();
        config.NewConfig<OverTimeApplication, OvertimeApplicationModel>();

        config.NewConfig<CreateTravelOrderApplication, TravelOrderApplication>();
        config.NewConfig<UpdateTravelOrderApplication, TravelOrderApplication>();
        config.NewConfig<TravelOrderApplication, TravelOrderApplicationModel>();

        config.NewConfig<CreatePassSlipApplication, PassSlipApplication>();
        config.NewConfig<UpdatePassSlipApplication, PassSlipApplication>();
        config.NewConfig<PassSlipApplication, PassSlipApplicationModel>()
            .Map(dest => dest.EmployeeName, src => src.Employee != null
                ? (src.Employee.LastName ?? "") + ", " + (src.Employee.FirstName ?? "") + " " + (src.Employee.MiddleName ?? "")
                : null);

        config.NewConfig<CreateUnderTimeApplication, UnderTimeApplication>();
        config.NewConfig<UpdateUnderTimeApplication, UnderTimeApplication>();
        config.NewConfig<UnderTimeApplication, UnderTimeApplicationModel>();

        // Income & Deductions
        config.NewConfig<CreateOtherIncome, OtherIncome>();
        config.NewConfig<UpdateOtherIncome, OtherIncome>();
        config.NewConfig<OtherIncome, OtherIncomeModel>();

        config.NewConfig<CreateDeduction, Deduction>();
        config.NewConfig<UpdateDeduction, Deduction>();
        config.NewConfig<Deduction, DeductionModel>();

        config.NewConfig<DeductionType, CreateDeduction>();
        config.NewConfig<UpdateDeduction, CreateDeduction>();
        config.NewConfig<CreateDeduction, DeductionTypeModel>();

        config.NewConfig<CreateBranch, Branch>()
            .Map(dest => dest.Boundary, src => src.Boundary);
        config.NewConfig<UpdateBranch, Branch>()
            .Map(dest => dest.Boundary, src => src.Boundary);
        config.NewConfig<Branch, BranchModel>()
            .Map(dest => dest.Boundary, src => src.Boundary)
            .TwoWays();

        config.NewConfig<CreateCostCenter, CostCenters>()
            .Map(dest => dest.Boundary, src => src.Boundary);
        config.NewConfig<UpdateCostCenter, CostCenters>()
            .Map(dest => dest.Boundary, src => src.Boundary);
        config.NewConfig<CostCenters, CostCenterModel>()
            .Map(dest => dest.Boundary, src => src.Boundary)
            .Map(dest => dest.BranchName, src => src.Branch != null ? src.Branch.Name : null)
            .Map(dest => dest.BranchCode, src => src.Branch != null ? src.Branch.Code : null)
            .TwoWays();

        config.NewConfig<CreatePosition, Position>();
        config.NewConfig<UpdatePosition, Position>();
        config.NewConfig<Position, PositionModel>();

        config.NewConfig<CreateTimeShift, TimeShift>();
        config.NewConfig<UpdateTimeShift, TimeShift>();
        config.NewConfig<TimeShift, TimeShiftModel>();

        config.NewConfig<CreateClient, Client>().TwoWays();
        config.NewConfig<UpdateClient, Client>();
        config.NewConfig<Client, ClientModel>();

        config.NewConfig<CreateSection, Section>();
        config.NewConfig<UpdateSection, Section>();
        config.NewConfig<Section, SectionModel>()
            .Map(dest => dest.DepartmentName, src => src.Department == null ? "" : src.Department.Name);

        config.NewConfig<CreateCompany, Company>();
        config.NewConfig<UpdateCompany, Company>();
        config.NewConfig<Company, CompanyModel>();

        config.NewConfig<CreateOtherIncomeType, OtherIncomeType>();
        config.NewConfig<UpdateOtherIncome, OtherIncomeType>();
        config.NewConfig<OtherIncomeType, OtherIncomeTypeModel>();

        config.NewConfig<CreateOtherIncomeApplication, OtherIncomeApplication>();
        config.NewConfig<UpdateOtherIncomeApplication, OtherIncomeApplication>();
        config.NewConfig<OtherIncomeApplication, OtherIncomeApplicationModel>();

        config.NewConfig<CreateOtherIncomeSchedule, OtherIncomeSchedules>();
        config.NewConfig<UpdateOtherIncomeSchedule, OtherIncomeSchedules>();

        config.NewConfig<CreateDeductionApplication, DeductionApplication>();
        config.NewConfig<UpdateDeductionApplication, DeductionApplication>();
        config.NewConfig<DeductionApplication, DeductionApplicationModel>();

        // Tables & Rates
        config.NewConfig<CreatePHIC, PHICTable>();
        config.NewConfig<UpdatePHIC, PHICTable>();
        config.NewConfig<PHICTable, PHICModel>();

        config.NewConfig<CreateHDMF, HDMFTable>();
        config.NewConfig<UpdateHDMF, HDMFTable>();
        config.NewConfig<HDMFTable, HDMFModel>();

        config.NewConfig<CreateWTax, TaxTable>();
        config.NewConfig<UpdateWax, TaxTable>();
        config.NewConfig<TaxTable, WTaxModel>();

        config.NewConfig<CreateSSS, SSSTable>();
        config.NewConfig<UpdateSSS, SSSTable>();
        config.NewConfig<SSSTable, SSSModel>();

        config.NewConfig<CreateAnnualTax, AnnualTaxTable>();
        config.NewConfig<UpdateAnnualTax, AnnualTaxTable>();
        config.NewConfig<AnnualTaxTable, AnnualTaxModel>();

        config.NewConfig<CreateRateTable, RateTable>();
        config.NewConfig<UpdateRateTable, RateTable>();
        config.NewConfig<RateTable, RateTableModel>();

        config.NewConfig<ClientRateTable, ClientRateTableModel>();

        config.NewConfig<CreateClientBillingInfo, ClientBillingInfo>();
        config.NewConfig<ClientBillingInfo, ClientBillingInfoModel>();

        config.NewConfig<CreateSSSRate, SSSRate>().TwoWays();
        config.NewConfig<CreatePHICRate, PHICRate>().TwoWays();
        config.NewConfig<CreateHDMFRate, HDMFRate>().TwoWays();
        config.NewConfig<CreateTaxRate, TaxRate>().TwoWays();

        config.NewConfig<SSSContributionModel, SSSContribution>().TwoWays();
        config.NewConfig<PHICContributionModel, PHICContribution>().TwoWays();
        config.NewConfig<HDMFContributionModel, HDMFContribution>().TwoWays();

        config.NewConfig<CreateRestDayDate, RestDayDate>();
        config.NewConfig<RestDayDate, RestDayDateModel>();

        config.NewConfig<Permission, PermissionModel>();
        config.NewConfig<Role, RoleModel>()
            .Map(dest => dest.Permissions, src => src.RolePermissions.Select(rp => rp.Permission));
    }

    /// <summary>
    /// Helper to handle the circular references and back-pointers originally in AfterMap
    /// </summary>
    private void ApplyEmployeeReferenceFixes(Employee dest)
    {
        if (dest.SSSRate != null) dest.SSSRate.Employee = dest;
        if (dest.PHICRate != null) dest.PHICRate.Employee = dest;
        if (dest.HDMFRate != null) dest.HDMFRate.Employee = dest;
        if (dest.TaxRate != null) dest.TaxRate.Employee = dest;
        if (dest.Settings != null) dest.Settings.Employee = dest;
    }
}