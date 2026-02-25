using System;
using System.Collections.Generic;
namespace Hrms.Domain.ValueObjects;

public class CreateRestDayDate
{
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollDate { get; set; }
}

public class RestDayDateModel
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollDate { get; set; }
}
 