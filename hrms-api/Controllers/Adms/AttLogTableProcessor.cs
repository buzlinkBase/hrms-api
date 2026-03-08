using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace Hrms.Api.Controllers.Adms;

public interface ICDataProcessor
{
    Task ProcessAsync(string SN, string data, CancellationToken token = default);
}

public class UserInforTableProcessor : ICDataProcessor
{
    public async Task ProcessAsync(string SN, string data, CancellationToken token = default)
    {
        var lines = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // 1. Split the line into tab-separated parts
            var fields = line.Split('\t');
            var userDict = new Dictionary<string, string>();

            // 2. Parse Key=Value pairs
            foreach (var field in fields)
            {
                var kv = field.Split('=', 2); // Split only on the first '='
                if (kv.Length == 2)
                {
                    userDict[kv[0].Trim()] = kv[1].Trim();
                }
            }

            // 3. Map to your entity
            var user = new UserRegistration
            {
                UserPin = userDict.GetValueOrDefault("USER PIN", "0"),
                Name = userDict.GetValueOrDefault("Name", "Unknown"),
                Priority = int.TryParse(userDict.GetValueOrDefault("Pri"), out int p) ? p : 0,
                Password = userDict.GetValueOrDefault("Passwd", ""),
                Card = userDict.GetValueOrDefault("Card", "")
            };

            //_logger.LogInformation("Parsed User: {Name} (PIN: {Pin})", user.Name, user.UserPin);
            // await _userService.SaveOrUpdateUser(user);
        }
    }
}

public class AttLogTableProcessor : AttendanceService, ICDataProcessor
{
    private readonly EmployeeService _employeeService;

    public AttLogTableProcessor(IUnitOfWorkService uow,
        EmployeeService employeeService) : base(uow)
    {
        _employeeService = employeeService;
    }
    public async Task ProcessAsync(string SN, string data, CancellationToken token = default)
    {
        var atts = new List<Attendance>();
        var employees = await _employeeService
            .GetQueryable()
            .Where(x => x.BioId != 0)
            .Select(x => new { x.Id, x.BioId })
            .GroupBy(x => x.BioId)
            .ToDictionaryAsync(x => x.Key, x => x.Select(x => x.Id).First(), token)
        ;
        var lines = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var fields = line.Split('\t');
            if (fields.Length >= 2)
            {
                var bioId = int.TryParse(fields[0], out int id) ? id : 0;
                if (!employees.TryGetValue(bioId, out Guid empId))
                {
                    //bioId is not registered in the system
                    continue;
                }
                var attendance = new Attendance()
                {
                    BioId = bioId,
                    WorkDateTime = DateTime.TryParse(fields[1], out DateTime dt) ? dt : DateTime.Now,
                    EmployeeId = empId
                };
                atts.Add(attendance);
                // await _service.Create(attendance);
            }
        }

        if (atts.Count>0)
        {
            await AddRangeAsync(atts, token);
            await CommitChangesAsync(token);
        }
    }
}

public class DefaultTableProcessor : ICDataProcessor
{
    private readonly ILogger<DefaultTableProcessor> _logger;

    public DefaultTableProcessor(ILogger<DefaultTableProcessor> logger)
    {
        _logger = logger;
    }
    public async Task ProcessAsync(string SN, string data, CancellationToken token = default)
    {
        _logger.LogInformation("Not manage ICDataProcessor" + data + SN);
        await Task.CompletedTask;
    }
}