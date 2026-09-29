namespace Hrms.Domain.Entities;

// Marks a record as belonging to one employee, so the confidential-employee query filter hides it
// together with that employee (see hrms.Infrastructure ConfidentialityFilters). Opt-in on purpose:
// not every EmployeeId column is the record's subject -- a workflow's named approver or an
// approval action's actor must stay visible even when that person is confidential.
public interface IEmployeeOwned
{
    Guid EmployeeId { get; }
}

// Same, for records whose owner column is nullable (201-file children, raw attendance logs).
public interface IOptionalEmployeeOwned
{
    Guid? EmployeeId { get; }
}

// An application an approval instance points at (ApprovalInstance.ApplicationId). An approver
// assigned to a confidential employee's request may see that one record -- see
// IConfidentialAccess.ApprovalVisibleRecordIds.
public interface IApprovalSubject
{
    Guid Id { get; }
}

// Which confidential-employee permission a record falls under (tenantstore catalog row
// "Confidential Records"): Access sees everything; Operations and Entries each see their own class;
// Compensation (salary and income figures) is Access-only. An employee-owned record with neither
// marker below is Operations.
public enum ConfidentialDataClass
{
    Profile,
    Operations,
    Entries,
    Compensation,
}

// Money entries a user with "Confidential Records:Entries" may process without seeing pay:
// cash bond, allowances / other income, deductions and loans.
public interface IEntryRecord;

// Salary and income figures -- payroll, contributions, statutory rates, funds that follow pay.
// Only "Confidential Records:Access" sees these for a confidential employee.
public interface ICompensationRecord;

public static class ConfidentialDataClasses
{
    public static ConfidentialDataClass Of(Type entityType) =>
        entityType == typeof(EmployeeEntities.Employee) ? ConfidentialDataClass.Profile
        : typeof(ICompensationRecord).IsAssignableFrom(entityType) ? ConfidentialDataClass.Compensation
        : typeof(IEntryRecord).IsAssignableFrom(entityType) ? ConfidentialDataClass.Entries
        : ConfidentialDataClass.Operations;
}
