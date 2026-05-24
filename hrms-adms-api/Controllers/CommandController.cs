using Asp.Versioning;
using Hrms.adms.Models.DTO;
using Hrms.adms.Services;
using Hrms.adms.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SqlServer.Types;

namespace Hrms.adms.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[Authorize]
public class CommandsController : ControllerBase
{
    private readonly CommandService _service;
    private readonly IConfiguration _configuration;

    public CommandsController(CommandService service,
        IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllPending([FromQuery] string SN)
    {
        var data = await _service.FindAllPending(SN);
        return Ok(data);
    }

    [HttpPost("sync-employees")]
    public async Task<IActionResult> Create([FromQuery] string SN, [FromBody] List<SetEmployeeCommandPayload> payload)
    {
        ISystemClockService clockService = new SystemClockService(_configuration);
        var formatter = new ZKTecoCommandFormatter(clockService);
        var devCommands = new List<DeviceCommand>();

        foreach (var item in payload)
        {
            var id = Guid.CreateVersion7();
            var syncEmpCmd = new DeviceCommandFormmaterPayload
            {
                Id = id.ToString("N"),
                Command = "SYNC_EMPLOYEES",
                CommandPayload = "",
                Parameters = new()
                {
                    {"employee_code",item.BioId},
                    {"name", item.Name },
                    {"card_no", item.Card},
                    {"privilege",item.Privilege },
                    {"password", item.Password},
                    {"group_code","1" },
                }
            };
            var resultCommand = formatter.Format(syncEmpCmd);
            var devcommand = new DeviceCommand()
            {
                Id = id,
                CommandType = syncEmpCmd.Command,
                Commands = resultCommand,
                SN = SN,
            };
            devCommands.Add(devcommand);
        }
        await _service.CreateCommand(devCommands);
        return NoContent();
    }

    [HttpPost("sync-biometric")]
    public async Task<IActionResult> SyncBio([FromQuery] string SN, [FromBody] List<SyncBioPayload> payload)
    {
        ISystemClockService clockService = new SystemClockService(_configuration);
        var formatter = new ZKTecoCommandFormatter(clockService);
        var devCommands = new List<DeviceCommand>();
        foreach (var item in payload)
        {
            var id = Guid.CreateVersion7();
            var syncEmpCmd = new DeviceCommandFormmaterPayload
            {
                Id = id.ToString("N"),
                Command = "SYNC_BIOMETRICS",
                CommandPayload = "",
                Parameters = new()
                {
                    {"pin",item.BioId},
                    {"biometric_type",  "fingerprint" },
                    {"template_index", item.Index},
                    {"template_data",item.Template },
                    {"is_valid", item.Dures},
                }
            };
            var resultCommand = formatter.Format(syncEmpCmd);
            var devcommand = new DeviceCommand()
            {
                Id = id,
                CommandType = syncEmpCmd.Command,
                Commands = resultCommand,
                SN = SN,
            };
            devCommands.Add(devcommand);
        }
        await _service.CreateCommand(devCommands);
        return NoContent();
    }

    [HttpPost("sync-face")]
    public async Task<IActionResult> SyncFace([FromQuery] string SN, [FromBody] List<SyncBioPayload> payload)
    {
        ISystemClockService clockService = new SystemClockService(_configuration);
        var formatter = new ZKTecoCommandFormatter(clockService);
        var devCommands = new List<DeviceCommand>();
        foreach (var item in payload)
        {
            var id = Guid.NewGuid();
            var syncEmpCmd = new DeviceCommandFormmaterPayload
            {
                Id = id.ToString("N"),
                Command = "SYNC_BIOMETRICS",
                CommandPayload = "",
                Parameters = new()
                {
                    {"pin",item.BioId},
                    {"biometric_type",  "face" },
                    {"template_index", "50"},
                    {"template_data",item.Template },
                    {"is_valid",  0},
                    {"face_upload_prefixed",  "false"},
                }
            };
            var resultCommand = formatter.Format(syncEmpCmd);
            var devcommand = new DeviceCommand()
            {
                Id = id,
                CommandType = syncEmpCmd.Command,
                Commands = resultCommand,
                SN = SN,
            };
            devCommands.Add(devcommand);
        }
        await _service.CreateCommand(devCommands);
        return NoContent();
    }

    [HttpPost("enroll-fp")]
    public async Task<IActionResult> EnrollFinger([FromQuery] string SN, [FromBody] EnrollFPPayload payload)
    {
        ISystemClockService clockService = new SystemClockService(_configuration);
        var formatter = new ZKTecoCommandFormatter(clockService);
        var id = Guid.CreateVersion7();
        var syncEmpCmd = new DeviceCommandFormmaterPayload
        {
            Id = id.ToString("N"),
            Command = "ENROLL_FINGERPRINT",
            CommandPayload = "",
            Parameters = new()
                {
                    {"employee_code",payload.BioId},
                    {"template_index", payload.FingerIndex},
                    {"retry",3},
                    {"overwrite",  true},
                }
        };
        var resultCommand = formatter.Format(syncEmpCmd);
        var devcommand = new DeviceCommand()
        {
            Id = id,
            CommandType = syncEmpCmd.Command,
            Commands = resultCommand,
            SN = SN,
        };
        await _service.CreateCommand(new List<DeviceCommand> { devcommand });
        return NoContent();

    }

    [HttpPost("enroll-face")]
    public async Task<IActionResult> EnrollFace([FromQuery] string SN, [FromBody] EnrollFacePayload payload)
    {
        ISystemClockService clockService = new SystemClockService(_configuration);
        var formatter = new ZKTecoCommandFormatter(clockService);
        var id = Guid.CreateVersion7();
        var syncEmpCmd = new DeviceCommandFormmaterPayload
        {
            Id = id.ToString("N"),
            Command = "ENROLL_FACE",
            CommandPayload = "",
            Parameters = new()
                {
                    {"pin",payload.BioId},
                    {"card_no", payload.CardNo},
                    {"retry",5},
                    {"type",  payload.FaceType},
                    {"overwrite",  payload.Overwrite},
                }
        };
        var resultCommand = formatter.Format(syncEmpCmd);
        var devcommand = new DeviceCommand()
        {
            Id = id,
            CommandType = syncEmpCmd.Command,
            Commands = resultCommand,
            SN = SN,
        };
        await _service.CreateCommand(new List<DeviceCommand> { devcommand });
        return NoContent();
    }

    [HttpPost("reboot")]
    public async Task<IActionResult> Reboot([FromQuery] string SN)
    {
        ISystemClockService clockService = new SystemClockService(_configuration);
        var formatter = new ZKTecoCommandFormatter(clockService);
        var id = Guid.CreateVersion7();
        var syncEmpCmd = new DeviceCommandFormmaterPayload
        {
            Id = id.ToString("N"),
            Command = "RESTART",
        };
        var resultCommand = formatter.Format(syncEmpCmd);
        var devcommand = new DeviceCommand()
        {
            Id = id,
            CommandType = syncEmpCmd.Command,
            Commands = resultCommand,
            SN = SN,
        };
        await _service.CreateCommand(new List<DeviceCommand> { devcommand });
        return NoContent();
    }

    [HttpPost("clear-logs")]
    public async Task<IActionResult> ClearLogs([FromQuery] string SN)
    {
        ISystemClockService clockService = new SystemClockService(_configuration);
        var formatter = new ZKTecoCommandFormatter(clockService);
        var id = Guid.CreateVersion7();
        var syncEmpCmd = new DeviceCommandFormmaterPayload
        {
            Id = id.ToString("N"),
            Command = "CLEAR_LOGS",
        };
        var resultCommand = formatter.Format(syncEmpCmd);
        var devcommand = new DeviceCommand()
        {
            Id = id,
            CommandType = syncEmpCmd.Command,
            Commands = resultCommand,
            SN = SN,
        };
        await _service.CreateCommand(new List<DeviceCommand> { devcommand });
        return NoContent();
    }

    [HttpPost("set-time")]
    public async Task<IActionResult> SetTime([FromQuery] string SN, [FromQuery] bool autoServerTime)
    {
        ISystemClockService clockService = new SystemClockService(_configuration);
        var formatter = new ZKTecoCommandFormatter(clockService);
        var id = Guid.CreateVersion7();
        var syncEmpCmd = new DeviceCommandFormmaterPayload
        {
            Id = id.ToString("N"),
            Command = "SET_TIME",
            Parameters = new() { { "timestamp", DateTime.UtcNow } }
        };

        if (autoServerTime)
        {
            syncEmpCmd.Parameters = new() { { "option", "AutoServerTime" }, { "value", "1" } };
        }
        var resultCommand = formatter.Format(syncEmpCmd);
        var devcommand = new DeviceCommand()
        {
            Id = id,
            CommandType = syncEmpCmd.Command,
            Commands = resultCommand,
            SN = SN,
        };
        await _service.CreateCommand(new List<DeviceCommand> { devcommand });
        return NoContent();
    }

    [HttpPost("enable-attendance")]
    public async Task<IActionResult> EnableDevice([FromQuery] string SN, [FromQuery] int enable)
    {
        ISystemClockService clockService = new SystemClockService(_configuration);
        var formatter = new ZKTecoCommandFormatter(clockService);
        var id = Guid.CreateVersion7();
        var syncEmpCmd = new DeviceCommandFormmaterPayload
        {
            Id = id.ToString("N"),
            Command = enable == 1 ? "ENABLE_ATTENDANCE" : "DISABLE_ATTENDANCE",

        };
        var resultCommand = formatter.Format(syncEmpCmd);
        var devcommand = new DeviceCommand()
        {
            Id = id,
            CommandType = syncEmpCmd.Command,
            Commands = resultCommand,
            SN = SN,
        };
        await _service.CreateCommand(new List<DeviceCommand> { devcommand });
        return NoContent();
    }

    [HttpPost("clear-admin")]
    public async Task<IActionResult> ClearAdmin([FromQuery] string SN)
    {
        ISystemClockService clockService = new SystemClockService(_configuration);
        var formatter = new ZKTecoCommandFormatter(clockService);
        var id = Guid.CreateVersion7();
        var syncEmpCmd = new DeviceCommandFormmaterPayload
        {
            Id = id.ToString("N"),
            Command = "RM_ADMIN_PRIVILEGE",
        };
        var resultCommand = formatter.Format(syncEmpCmd);
        var devcommand = new DeviceCommand()
        {
            Id = id,
            CommandType = syncEmpCmd.Command,
            Commands = resultCommand,
            SN = SN,
        };
        await _service.CreateCommand(new List<DeviceCommand> { devcommand });
        return NoContent();
    } 

    [HttpPost("pull-attendance")]
    public async Task<IActionResult> PullAtt([FromQuery] PullAttPayload data)
    {
        ISystemClockService clockService = new SystemClockService(_configuration);
        var formatter = new ZKTecoCommandFormatter(clockService);
        var id = Guid.CreateVersion7();
        var syncEmpCmd = new DeviceCommandFormmaterPayload
        {
            Id = id.ToString("N"),
            Command = "PULL_ATTENDANCE",
            Parameters = new()
                        {
                            { "start_time", data.StartDate},
                            { "end_time",  data.EndDate }
                        }
        };
        var resultCommand = formatter.Format(syncEmpCmd);
        var devcommand = new DeviceCommand()
        {
            Id = id,
            CommandType = syncEmpCmd.Command,
            Commands = resultCommand,
            SN = data.SN,
        };
        await _service.CreateCommand(new List<DeviceCommand> { devcommand });
        return NoContent();
    }


    [HttpDelete()]
    public async Task<IActionResult> Delete([FromQuery(Name = "id:Guid")] Guid Id)
    {
        _service.Delete(Id);
        _service.CommitChanges();
        return NoContent();
    }
}

public record PullAttPayload
{
    /// <summary>
    /// The unique target biometric hardware serial number string.
    /// </summary>
    /// <example>MB460-998234B</example>
    public string SN { get; set; } = string.Empty;

    /// <summary>
    /// The boundary starting date threshold for log collection filter matching.
    /// </summary>
    /// <example>2026-05-01T00:00:00</example>
    public DateTime StartDate { get; set; } = DateTime.Now;
    /// <summary>
    /// The boundary ending date threshold for log collection filter matching.
    /// </summary>
    /// <example>2026-05-24T23:59:59</example>
    public DateTime EndDate { get; set; } = DateTime.Now;
}
public record EnrollFPPayload
{
    public int BioId { get; set; }
    public int FingerIndex { get; set; }
}
public record EnrollFacePayload
{
    public int BioId { get; set; }
    public string CardNo { get; set; } = string.Empty;
    public int FaceType { get; set; } = 2;
    public bool Overwrite { get; set; } = true;
}
//sample commands
//class Program
//{
//    static void Main(string[] args)
//    {
//        // 1. Initialize dependencies
//        ISystemClockService clockService = new ClockNormalize();
//        var formatter = new ZKTecoCommandFormatter(clockService);

//        Console.WriteLine("================ ZKTECO COMMAND GENERATION ================\n");

//        // --- TEST CASE 1: RESTART / REBOOT ---
//        var rebootCmd = new DeviceCommandPayload { Id = 101, Command = "REBOOT" };
//        PrintResult("REBOOT", formatter.Format(rebootCmd));

//        // --- TEST CASE 2: CLEAR_LOGS ---
//        var clearLogsCmd = new DeviceCommandPayload { Id = 102, Command = "CLEAR_LOGS" };
//        PrintResult("CLEAR_LOGS", formatter.Format(clearLogsCmd));

//        // --- TEST CASE 3: SET_TIME (With Timestamp) ---
//        var setTimeCmd = new DeviceCommandPayload
//        {
//            Id = 103,
//            Command = "SET_TIME",
//            Parameters = new() { { "timestamp", DateTime.Now } }
//        };
//        PrintResult("SET_TIME (Timestamp)", formatter.Format(setTimeCmd));

//        // --- TEST CASE 4: SET_TIME (With Option Override) ---
//        var setTimeOptCmd = new DeviceCommandPayload
//        {
//            Id = 104,
//            Command = "SET_TIME",
//            Parameters = new() { { "option", "AutoServerTime" }, { "value", "1" } }
//        };
//        PrintResult("SET_TIME (Option)", formatter.Format(setTimeOptCmd));

//        // --- TEST CASE 5: SET_TIMEZONE ---
//        var setTzCmd = new DeviceCommandPayload
//        {
//            Id = 105,
//            Command = "SET_TIMEZONE",
//            Parameters = new() { { "option", "TimeZone" }, { "value", "420" } } // GMT+7 Example
//        };
//        PrintResult("SET_TIMEZONE", formatter.Format(setTzCmd));

//        // --- TEST CASE 6: ENABLE_ATTENDANCE ---
//        var enableAttCmd = new DeviceCommandPayload { Id = 106, Command = "ENABLE_ATTENDANCE" };
//        PrintResult("ENABLE_ATTENDANCE", formatter.Format(enableAttCmd));

//        // --- TEST CASE 7: DISABLE_ATTENDANCE ---
//        var disableAttCmd = new DeviceCommandPayload { Id = 107, Command = "DISABLE_ATTENDANCE" };
//        PrintResult("DISABLE_ATTENDANCE", formatter.Format(disableAttCmd));

//        // --- TEST CASE 8: SET_ATTENDANCE_MODE ---
//        var attModeCmd = new DeviceCommandPayload
//        {
//            Id = 108,
//            Command = "SET_ATTENDANCE_MODE",
//            Parameters = new() { { "option", "AttFmt" }, { "value", "1" } }
//        };
//        PrintResult("SET_ATTENDANCE_MODE", formatter.Format(attModeCmd));

//        // --- TEST CASE 9: SYNC_EMPLOYEES (Add/Update User) ---
//        var syncEmpCmd = new DeviceCommandPayload
//        {
//            Id = 109,
//            Command = "SYNC_EMPLOYEES",
//            Parameters = new()
//            {
//                { "employee_code", "9988" }, // Falling back to employee_code
//                { "name", "John Doe" },
//                { "card_no", "12345678" },
//                { "privilege", 0 },
//                { "password", "4321" },
//                { "group_code", "1" }
//            }
//        };
//        PrintResult("SYNC_EMPLOYEES (Update)", formatter.Format(syncEmpCmd));

//        // --- TEST CASE 10: SYNC_EMPLOYEES (Query Single User) ---
//        var queryEmpCmd = new DeviceCommandPayload
//        {
//            Id = 110,
//            Command = "SYNC_EMPLOYEES",
//            Parameters = new() { { "action", "query" }, { "pin", "9988" } }
//        };
//        PrintResult("SYNC_EMPLOYEES (Query)", formatter.Format(queryEmpCmd));

//        // --- TEST CASE 11: SYNC_BIOMETRICS (Fingerprint with spaced template) ---
//        // Mimicking base64 template with spaces. Regex.Replace will strip these out.
//        string fakeFpBase64 = "U09NRSBGSU5HRVJQUklOVCBEQVRBIEhFUkU=";
//        var syncFpCmd = new DeviceCommandPayload
//        {
//            Id = 111,
//            Command = "SYNC_BIOMETRICS",
//            Parameters = new()
//            {
//                { "pin", "1002" },
//                { "biometric_type", "fingerprint" },
//                { "template_index", "2" },
//                { "template_data", fakeFpBase64 },
//                { "is_valid", "true" }
//            }
//        };
//        PrintResult("SYNC_BIOMETRICS (Fingerprint)", formatter.Format(syncFpCmd));

//        // --- TEST CASE 12: SYNC_BIOMETRICS (Face Template) ---
//        string fakeFaceBase64 = "RkFDRSBURU1QTEFURSBXSVRIU1BBQ0VTIElOIEJBU0U2NA==";
//        var syncFaceCmd = new DeviceCommandPayload
//        {
//            Id = 112,
//            Command = "SYNC_BIOMETRICS",
//            Parameters = new()
//            {
//                { "pin", "1002" },
//                { "biometric_type", "face" },
//                { "template_index", "50" },
//                { "template_data", fakeFaceBase64 },
//                { "face_upload_prefixed", false }
//            }
//        };
//        PrintResult("SYNC_BIOMETRICS (Face)", formatter.Format(syncFaceCmd));

//        // --- TEST CASE 13: SYNC_BIOMETRICS (User Photo) ---
//        var syncPhotoCmd = new DeviceCommandPayload
//        {
//            Id = 113,
//            Command = "SYNC_BIOMETRICS",
//            Parameters = new()
//            {
//                { "pin", "1002" },
//                { "biometric_type", "photo" },
//                { "file_name", "1002.jpg" },
//                { "template_data", "IMAGE_RAW_OR_BASE64_STREAM" }
//            }
//        };
//        PrintResult("SYNC_BIOMETRICS (Photo)", formatter.Format(syncPhotoCmd));

//        // --- TEST CASE 14: ENROLL_FINGERPRINT ---
//        var enrollFpCmd = new DeviceCommandPayload
//        {
//            Id = 114,
//            Command = "ENROLL_FINGERPRINT",
//            Parameters = new()
//            {
//                { "pin", "1005" },
//                { "fid", 1 }, // Finger ID (Index 0-9)
//                { "retry", 3 },
//                { "overwrite", true }
//            }
//        };
//        PrintResult("ENROLL_FINGERPRINT", formatter.Format(enrollFpCmd));

//        // --- TEST CASE 15: ENROLL_FACE ---
//        var enrollFaceCmd = new DeviceCommandPayload
//        {
//            Id = 115,
//            Command = "ENROLL_FACE",
//            Parameters = new()
//            {
//                { "pin", "1005" },
//                { "card_no", "87654321" },
//                { "retry", 5 },
//                { "type", 2 }
//            }
//        };
//        PrintResult("ENROLL_FACE", formatter.Format(enrollFaceCmd));

//        // --- TEST CASE 16: PULL_EMPLOYEES (All or Single) ---
//        var pullEmpCmd = new DeviceCommandPayload
//        {
//            Id = 116,
//            Command = "PULL_EMPLOYEES",
//            Parameters = new() { { "pin", "1005" } }
//        };
//        PrintResult("PULL_EMPLOYEES", formatter.Format(pullEmpCmd));

//        // --- TEST CASE 17: PULL_ATTENDANCE (Filtered Logs) ---
//        var pullAttCmd = new DeviceCommandPayload
//        {
//            Id = 117,
//            Command = "PULL_ATTENDANCE",
//            Parameters = new()
//            {
//                { "start_time", "2026-05-19 00:00:00" },
//                { "end_time", "2026-05-19 23:59:59" }
//            }
//        };
//        PrintResult("PULL_ATTENDANCE", formatter.Format(pullAttCmd));

//        // --- TEST CASE 18: RM_ADMIN_PRIVILEGE ---
//        var rmAdminCmd = new DeviceCommandPayload { Id = 118, Command = "RM_ADMIN_PRIVILEGE" };
//        PrintResult("RM_ADMIN_PRIVILEGE", formatter.Format(rmAdminCmd));

//        // --- TEST CASE 19: CommandPayload Direct Bypass ---
//        var rawPayloadCmd = new DeviceCommandPayload
//        {
//            Id = 119,
//            CommandPayload = "CUSTOM:RAW:STRING:NO:ID:REQUIRED"
//        };
//        PrintResult("CommandPayload (Direct)", formatter.Format(rawPayloadCmd));

//        // --- TEST CASE 20: Fallback Raw Command ---
//        var fallbackCmd = new DeviceCommandPayload { Id = 120, Command = "CUSTOM_UNKNOWN_OP" };
//        PrintResult("Fallback Command", formatter.Format(fallbackCmd));
//    }

//    private static void PrintResult(string caseName, string output)
//    {
//        Console.ForegroundColor = ConsoleColor.Cyan;
//        Console.Write($"{caseName,-30}: ");
//        Console.ForegroundColor = ConsoleColor.White;
//        Console.WriteLine(output);
//    }
//}

