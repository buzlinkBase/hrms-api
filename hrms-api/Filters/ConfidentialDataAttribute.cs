using Hrms.Domain.Entities;

namespace Hrms.Api.Filters;

/// <summary>
/// Declares which class of confidential-employee data an endpoint works on. A partial clearance only
/// counts when it covers that class: ConfidentialAccessMiddleware narrows the caller's grants to it
/// (<see cref="Hrms.Core.Services.Security.ConfidentialGrants.NarrowTo"/>), so the employees an
/// operation lists, computes or generates for are exactly the ones the caller is cleared for there.
/// E.g. a DTR run by an Entries-only user leaves confidential employees out instead of generating
/// absences for them from attendance it can't see. Controller level; an action-level one wins.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ConfidentialDataAttribute(ConfidentialDataClass dataClass) : Attribute
{
    public ConfidentialDataClass DataClass { get; } = dataClass;
}

/// <summary>DTR, attendance, schedules, applications -- needs "Confidential Records:Operations".</summary>
public sealed class OperationsDataAttribute() : ConfidentialDataAttribute(ConfidentialDataClass.Operations);

/// <summary>Cash bond, allowances / other income, loans -- needs "Confidential Records:Entries".</summary>
public sealed class EntriesDataAttribute() : ConfidentialDataAttribute(ConfidentialDataClass.Entries);

/// <summary>
/// Generates or reveals salary and income figures (payroll runs, payslips, pay-based reports): only
/// full clearance ("Confidential Records:Access") covers confidential employees here.
/// </summary>
public sealed class CompensationDataAttribute() : ConfidentialDataAttribute(ConfidentialDataClass.Compensation);
