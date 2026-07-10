using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.SqlServer.Types;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hrms.Domain.ValueObjects;

public record AttendanceFilter : EmployeeFilter { }
public record AttendanceFilterDate
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? EmployeeId { get; set; }
}
public record CreateAttendance
{
    public DateTime WorkTime { get; set; }
    public Guid EmployeeId { get; set; }
}

public class AttendanceModel
{
    public Guid Id { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? Name { get; set; }
    public DateTime WorkDateTime { get; set; }
    public string Batch { get; set; } = string.Empty;
    public string LogSource { get; set; } = LOGSOURCE.OTHER.ToString();
}


public class ManualAttModel
{
    public string BatchCode { get; set; }
    public DateTime Date { get; set; }
}