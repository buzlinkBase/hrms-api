using AutoMapper;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
namespace Hrms.Core;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<PayrollSummaryLine, Payroll>().ReverseMap();
        CreateMap<CreateDepartment, Department>().ReverseMap();
        CreateMap<UpdateDepartment, Department>();
        CreateMap<Department, DepartmentModel>()
            .ForMember(x => x.BranchName, o => o.MapFrom(x => x.Branch.ShortName ?? x.Branch.Description))
            .ForMember(x => x.BranchHeadName, o => o.MapFrom(x => x.Head.FullName()))
            ;

        CreateMap<CreateSalaryAdjustment, SalaryAdjustment>();
        CreateMap<UpdateSalaryAdjustment, SalaryAdjustment>();
        CreateMap<SalaryAdjustment, SalaryAdjustmentModel>();

        CreateMap<CreateDailyRecord, DailyRecord>();
        CreateMap<UpdateDailyRecord, DailyRecord>();
        CreateMap<DailyRecord, DailyRecordModel>();//response for api
        CreateMap<DailyRecord, DailyRecordRunModel>();

        CreateMap<CreateEmployeeSetting, EmployeeSetting>();
        CreateMap<UpdateEmployeeSetting, EmployeeSetting>();
        CreateMap<EmployeeSetting, EmployeeSettingModel>();


        CreateMap<Employee, EmployeeModelPayrollRun>()
              .ForMember(x => x.FullName, o => o.MapFrom(x => x.FullName()))
              .ForMember(x => x.PayrollFrequency, o => o.MapFrom(x => x.PayrollGroup.PayrollFrequency))
              ;

        CreateMap<CreateEmployee, Employee>()
        .AfterMap((src, dest) =>
        {
            if (dest.SSSRate != null) dest.SSSRate.Employee = dest;
            if (dest.PHICRate != null) dest.PHICRate.Employee = dest;
            if (dest.HDMFRate != null) dest.HDMFRate.Employee = dest;
            if (dest.TaxRate != null) dest.TaxRate.Employee = dest;
            if (dest.Settings != null) dest.Settings.Employee = dest;
            if (dest.RestDays != null) dest.RestDays = dest.RestDays;
        });
        CreateMap<UpdateEmployee, Employee>().AfterMap((src, dest) =>
        {
            if (dest.SSSRate != null) dest.SSSRate.Employee = dest;
            if (dest.PHICRate != null) dest.PHICRate.Employee = dest;
            if (dest.HDMFRate != null) dest.HDMFRate.Employee = dest;
            if (dest.TaxRate != null) dest.TaxRate.Employee = dest;
            if (dest.Settings != null) dest.Settings.Employee = dest;
            if (dest.RestDays != null) dest.RestDays = dest.RestDays;
        });

        CreateMap<Employee, EmployeeModel>()
            .ForMember(x => x.FullName, o => o.MapFrom(x => x.FullName()))
            .ForMember(x => x.PayrollGroupName, o => o.MapFrom(x => x.PayrollGroup.Name))
            .ForMember(x => x.ClientName, o => o.MapFrom(x => x.Client.Name))
            .ForMember(x => x.PositionName, o => o.MapFrom(x => x.Position.Name))
            .ForMember(x => x.BranchName, o => o.MapFrom(x => x.Branch.Description))
            .ForMember(x => x.DepartmentName, o => o.MapFrom(x => x.Department.Name))
            .ForMember(x => x.TimeShiftName, o => o.MapFrom(x => x.TimeShift.ShiftName))
            .ForMember(x => x.AreaName, o => o.MapFrom(x => x.Area.Name))
            .ForMember(x => x.PayrollFrequency, o => o.MapFrom(x => x.PayrollGroup.PayrollFrequency))
            //.ForMember(x => x.Client, o => o.MapFrom(x => x.Client))
            //.ForMember(x => x.Department, o => o.MapFrom(x => x.Client))
            //.ForMember(x => x.Settings, o => o.MapFrom(x => x.Setting))
            ;

        CreateMap<RestDayModel, RestDay>().ReverseMap();

        CreateMap<Employee, EmployeeFullModel>()
            .ForMember(x => x.FullName, o => o.MapFrom(x => x.FullName()))
            .ForMember(x => x.PayrollGroupName, o => o.MapFrom(x => x.PayrollGroup.Name))
            .ForMember(x => x.ClientName, o => o.MapFrom(x => x.Client.Name))
            .ForMember(x => x.PositionName, o => o.MapFrom(x => x.Position.Name))
            .ForMember(x => x.BranchName, o => o.MapFrom(x => x.Branch.Description))
            .ForMember(x => x.DepartmentName, o => o.MapFrom(x => x.Department.Name))
            .ForMember(x => x.TimeShiftName, o => o.MapFrom(x => x.TimeShift.ShiftName))
            .ForMember(x => x.AreaName, o => o.MapFrom(x => x.Area.Name))
            .ForMember(x => x.PayrollFrequency, o => o.MapFrom(x => x.PayrollGroup.PayrollFrequency))
            //.ForMember(x => x.Assets, o => o.MapFrom(x => x.Assets))
            //.ForMember(x => x.Dependents, o => o.MapFrom(x => x.Dependents))
            //.ForMember(x => x.Educations, o => o.MapFrom(x => x.Educations))
            //.ForMember(x => x.Skills, o => o.MapFrom(x => x.Skills))
            //.ForMember(x => x.EmployeeRecords, o => o.MapFrom(x => x.EmployeeRecords))
            //.ForMember(x => x.Employments, o => o.MapFrom(x => x.Employments))
            //.ForMember(x => x.Settings, o => o.MapFrom(x => x.Setting))
            ;


        CreateMap<CreateSkill, Skill>();
        CreateMap<UpdateSkill, Skill>();
        CreateMap<Skill, SkillModel>();

        CreateMap<CreateEducation, Education>();
        CreateMap<UpdateEducation, Education>();
        CreateMap<Education, EducationModel>();

        CreateMap<CreateEmployeeRecord, EmployeeRecord>();
        CreateMap<UpdateEmployeeRecord, EmployeeRecord>();
        CreateMap<EmployeeRecord, EmployeeRecordModel>();

        CreateMap<CreateDependent, Dependent>();
        CreateMap<UpdateDependent, Dependent>();
        CreateMap<Dependent, DependentModel>();

        CreateMap<CreateAssignAsset, AssignAsset>();
        CreateMap<UpdateAssignAsset, AssignAsset>();
        CreateMap<AssignAsset, AssignAssetModel>().ReverseMap();

        CreateMap<CreateEmploymentHistory, EmploymentHistory>();
        CreateMap<UpdateEmploymentHistory, EmploymentHistory>();
        CreateMap<EmploymentHistory, EmploymentHistoryModel>();

        CreateMap<CreatePayrollGroup, PayrollGroup>();
        CreateMap<UpdatePayrollGroup, PayrollGroup>();
        CreateMap<PayrollGroup, PayrollGroupModel>();
        CreateMap<CutoffModel, CutoffDay>().ReverseMap();

        CreateMap<CreateArea, CostCenters>();
        CreateMap<UpdateArea, CostCenters>();
        CreateMap<CostCenters, AreaModel>();

        CreateMap<CreateLeave, Leave>();
        CreateMap<UpdateLeave, Leave>();
        CreateMap<Leave, LeaveModel>();

        CreateMap<CreateLeaveApplication, LeaveApplication>();
        CreateMap<UpdateLeaveApplication, LeaveApplication>();
        CreateMap<LeaveApplication, LeaveApplicationModel>();
        CreateMap<LeaveApplicationDetail, LeaveApplicationPyRun>()
            .ForMember(x => x.LeaveId, o => o.MapFrom(x => x.Application.LeaveId))
            .ForMember(x => x.EmployeeId, o => o.MapFrom(x => x.Application.EmployeeId))
            .ForMember(x => x.DayType, o => o.MapFrom(x => x.Application.DayType))
            .ForMember(x => x.PayType, o => o.MapFrom(x => x.Application.PayType))
            ;

        CreateMap<CreateWorkRotationPlan, WorkSchedulePlan>();
        CreateMap<UpdateWorkSchedulePlan, WorkSchedulePlan>();
        CreateMap<WorkSchedulePlan, WorkSchedulePlanModel>();

        CreateMap<CreateChangeHoliday, ChangeHoliday>();
        //CreateMap<ChangeHoliday, ChangeHolidayModel>();

        CreateMap<CreateHoliday, Holiday>();
        CreateMap<UpdateHoliday, Holiday>();
        CreateMap<Holiday, HolidayModel>();

        CreateMap<CreateOverTimeApplication, OverTimeApplication>();
        CreateMap<UpdateOvertimeApplication, OverTimeApplication>();
        CreateMap<OverTimeApplication, OvertimeApplicationModel>();

        CreateMap<CreateUnderTimeApplication, OverTimeApplication>();
        CreateMap<UpdateUnderTimeApplication, OverTimeApplication>();
        CreateMap<OverTimeApplication, OvertimeApplicationModel>();

        CreateMap<CreateOtherIncome, OtherIncome>();
        CreateMap<UpdateOtherIncome, OtherIncome>();
        CreateMap<OtherIncome, OtherIncomeModel>();

        CreateMap<CreateDeduction, Deduction>();
        CreateMap<UpdateDeduction, Deduction>();
        CreateMap<Deduction, DeductionModel>();

        CreateMap<DeductionType, CreateDeduction>();
        CreateMap<UpdateDeduction, CreateDeduction>();
        CreateMap<CreateDeduction, DeductionTypeModel>();

        CreateMap<CreateBranch, Branch>();
        CreateMap<UpdateBranch, Branch>();
        CreateMap<Branch, BranchModel>();

        CreateMap<CreatePosition, Position>();
        CreateMap<UpdateBranch, Position>();
        CreateMap<Position, PositionModel>();

        CreateMap<CreateTimeShift, TimeShift>();
        CreateMap<UpdateTimeShift, TimeShift>();
        CreateMap<TimeShift, TimeShiftModel>();

        CreateMap<CreateClient, Client>().ReverseMap();
        CreateMap<UpdateClient, Client>();
        CreateMap<Client, ClientModel>();

        CreateMap<CreateSection, Section>();
        CreateMap<UpdateSection, Section>();
        CreateMap<Section, SectionModel>()
            .ForMember(x => x.DepartmentName, o => o.MapFrom(x => x.Department.Name));

        CreateMap<CreateCompany, Company>();
        CreateMap<UpdateCompany, Company>();
        CreateMap<Company, CompanyModel>();

        CreateMap<CreateOtherIncomeType, OtherIncomeType>();
        CreateMap<UpdateOtherIncome, OtherIncomeType>();
        CreateMap<OtherIncomeType, OtherIncomeTypeModel>();

        CreateMap<CreateOtherIncomeApplication, OtherIncomeApplication>();
        CreateMap<UpdateOtherIncomeApplication, OtherIncomeApplication>();
        CreateMap<OtherIncomeApplication, OtherIncomeApplicationModel>();

        CreateMap<CreateOtherIncomeSchedule, OtherIncomeSchedules>();
        CreateMap<UpdateOtherIncomeSchedule, OtherIncomeSchedules>();

        CreateMap<CreateDeductionApplication, DeductionApplication>();
        CreateMap<UpdateDeductionApplication, DeductionApplication>();
        CreateMap<DeductionApplication, DeductionApplicationModel>();

        CreateMap<CreatePHIC, PHICTable>();
        CreateMap<UpdatePHIC, PHICTable>();
        CreateMap<PHICTable, PHICModel>();

        CreateMap<CreateHDMF, HDMFTable>();
        CreateMap<UpdateHDMF, HDMFTable>();
        CreateMap<HDMFTable, HDMFModel>();

        CreateMap<CreateWTax, TaxTable>();
        CreateMap<UpdateWax, TaxTable>();
        CreateMap<TaxTable, WTaxModel>();

        CreateMap<CreateSSS, SSSTable>();
        CreateMap<UpdateSSS, SSSTable>();
        CreateMap<SSSTable, SSSModel>();

        CreateMap<CreateRateTable, RateTable>();
        CreateMap<UpdateRateTable, RateTable>();
        CreateMap<RateTable, RateTableModel>();

        CreateMap<CreateSSSRate, SSSRate>().ReverseMap();
        CreateMap<CreatePHICRate, PHICRate>().ReverseMap();
        CreateMap<CreateHDMFRate, HDMFRate>().ReverseMap();
        CreateMap<CreateTaxRate, TaxRate>().ReverseMap();

        CreateMap<SSSContributionModel, SSSContribution>().ReverseMap();
        CreateMap<PHICContributionModel, PHICContribution>().ReverseMap();
        CreateMap<HDMFContributionModel, HDMFContribution>().ReverseMap();
    }
}