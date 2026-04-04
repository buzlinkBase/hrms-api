namespace Hrms.Core.Services;

public interface IFileParser
{
    Task<List<CreateAttendancePayload>> Parse(Stream stream);
}
