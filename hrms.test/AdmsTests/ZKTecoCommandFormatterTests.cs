using Hrms.adms.Models.DTO;
using Hrms.adms.Utilities;

namespace hrms.test.AdmsTests;

/// <summary>
/// Fixed-output stand-in for ISystemClockService — matches this codebase's existing
/// (no mocking library) test convention since the interface is small enough to fake directly.
/// </summary>
public class FakeSystemClockService : ISystemClockService
{
    public string NormalizedValue { get; set; } = "2026-01-01 08:00:00";
    public DateTimeOffset Now() => new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    public string FormatForDevice() => NormalizedValue;
    public string NormalizeForDevice(object? value) => NormalizedValue;
    public string TimezoneName() => "UTC";
    public string DeviceTimezoneOption() => "0";
}

public class ZKTecoCommandFormatterTests
{
    private readonly FakeSystemClockService _clock = new();
    private readonly ZKTecoCommandFormatter _formatter;

    public ZKTecoCommandFormatterTests()
    {
        _formatter = new ZKTecoCommandFormatter(_clock);
    }

    private static DeviceCommandFormmaterPayload Payload(
        string command,
        Dictionary<string, object?>? parameters = null,
        string id = "1",
        string? commandPayload = null) =>
        new()
        {
            Id = id,
            Command = command,
            CommandPayload = commandPayload,
            Parameters = parameters,
        };

    // --- Simple literal commands ---------------------------------------------------------

    [Theory]
    [InlineData("RESTART")]
    [InlineData("REBOOT")]
    public void Format_RestartOrReboot_ReturnsRebootCommand(string command)
    {
        _formatter.Format(Payload(command)).Should().Be("C:1:REBOOT");
    }

    [Fact]
    public void Format_ClearLogs_ReturnsClearLogCommand()
    {
        _formatter.Format(Payload("CLEAR_LOGS")).Should().Be("C:1:CLEAR LOG");
    }

    [Fact]
    public void Format_EnableAttendance_ReturnsRealtimeOnCommand()
    {
        _formatter.Format(Payload("ENABLE_ATTENDANCE")).Should().Be("C:1:SET OPTION Realtime=1");
    }

    [Fact]
    public void Format_DisableAttendance_ReturnsRealtimeOffCommand()
    {
        _formatter.Format(Payload("DISABLE_ATTENDANCE")).Should().Be("C:1:SET OPTION Realtime=0");
    }

    [Fact]
    public void Format_RemoveAdminPrivilege_ReturnsClearAdminCommand()
    {
        _formatter.Format(Payload("RM_ADMIN_PRIVILEGE")).Should().Be("C:1:CLEAR ADMIN");
    }

    // --- SET_TIME / UPDATE_TIME -------------------------------------------------------------

    [Fact]
    public void Format_SetTime_WithExplicitOptionAndValue_UsesThemVerbatim()
    {
        var payload = Payload("SET_TIME", new Dictionary<string, object?>
        {
            ["option"] = "DateTime",
            ["value"] = "2026-01-01 10:00:00",
        });

        _formatter.Format(payload).Should().Be("C:1:SET OPTION DateTime=2026-01-01 10:00:00");
    }

    [Fact]
    public void Format_UpdateTime_WithoutExplicitOption_NormalizesTimestampViaClock()
    {
        _clock.NormalizedValue = "2026-03-15 09:30:00";
        var payload = Payload("UPDATE_TIME", new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-03-15T09:30:00Z",
        });

        _formatter.Format(payload).Should().Be("C:1:SET OPTION DateTime=2026-03-15 09:30:00");
    }

    // --- SET_TIMEZONE / SET_ATTENDANCE_MODE (FormatOptionCommand) --------------------------

    [Fact]
    public void Format_SetTimezone_WithoutParameters_DefaultsToRealtimeOn()
    {
        _formatter.Format(Payload("SET_TIMEZONE")).Should().Be("C:1:SET OPTION Realtime=1");
    }

    [Fact]
    public void Format_SetAttendanceMode_SanitizesEmbeddedWhitespace()
    {
        var payload = Payload("SET_ATTENDANCE_MODE", new Dictionary<string, object?>
        {
            ["option"] = "GMT+8\n",
            ["value"] = "1\t",
        });

        _formatter.Format(payload).Should().Be("C:1:SET OPTION GMT+8=1");
    }

    // --- SYNC_EMPLOYEES ----------------------------------------------------------------------

    [Fact]
    public void Format_SyncEmployees_QueryAction_ReturnsQueryUserInfo()
    {
        var payload = Payload("SYNC_EMPLOYEES", new Dictionary<string, object?>
        {
            ["action"] = "query",
            ["pin"] = "1001",
        });

        _formatter.Format(payload).Should().Be("C:1:DATA QUERY USERINFO PIN=1001");
    }

    [Fact]
    public void Format_SyncEmployees_QueryAction_FallsBackToEmployeeCodeWhenPinMissing()
    {
        var payload = Payload("SYNC_EMPLOYEES", new Dictionary<string, object?>
        {
            ["action"] = "query",
            ["employee_code"] = "E001",
        });

        _formatter.Format(payload).Should().Be("C:1:DATA QUERY USERINFO PIN=E001");
    }

    [Fact]
    public void Format_SyncEmployees_AddUser_ClampsPrivilegeAndOmitsMissingOptionalFields()
    {
        var payload = Payload("SYNC_EMPLOYEES", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["name"] = "John Doe",
            ["privilege"] = 99,
        });

        _formatter.Format(payload).Should().Be(
            "C:1:DATA UPDATE USERINFO PIN=1001\tName=John Doe\tPri=14\tPasswd=\tenable=true\tCard=");
    }

    [Fact]
    public void Format_SyncEmployees_AddUser_NegativePrivilegeClampsToZero()
    {
        var payload = Payload("SYNC_EMPLOYEES", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["name"] = "Jane Doe",
            ["privilege"] = -5,
        });

        _formatter.Format(payload).Should().Contain("Pri=0");
    }

    [Fact]
    public void Format_SyncEmployees_AddUser_IncludesOptionalFieldsWhenPresent()
    {
        var payload = Payload("SYNC_EMPLOYEES", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["name"] = "John Doe",
            ["card_no"] = "CARD1",
            ["password"] = "secret",
            ["group_code"] = "G1",
            ["timezone_code"] = "TZ1",
            ["verification_mode"] = "15",
            ["vice_card_no"] = "VC1",
            ["valid_from"] = "2026-01-01",
            ["valid_until"] = "2026-12-31",
        });

        var result = _formatter.Format(payload);

        result.Should().StartWith("C:1:DATA UPDATE USERINFO PIN=1001\tName=John Doe\tPri=0\tPasswd=secret\tenable=true\tCard=CARD1");
        result.Should().Contain("Grp=G1");
        result.Should().Contain("TZ=TZ1");
        result.Should().Contain("Verify=15");
        result.Should().Contain("ViceCard=VC1");
        result.Should().Contain("StartDatetime=2026-01-01");
        result.Should().Contain("EndDatetime=2026-12-31");
    }

    // --- PULL_EMPLOYEES / PULL_ATTENDANCE -----------------------------------------------------

    [Fact]
    public void Format_PullEmployees_WithPin_QueriesSpecificUser()
    {
        var payload = Payload("PULL_EMPLOYEES", new Dictionary<string, object?> { ["pin"] = "1001" });

        _formatter.Format(payload).Should().Be("C:1:DATA QUERY USERINFO PIN=1001");
    }

    [Fact]
    public void Format_PullEmployees_WithoutPin_QueriesAllUsers()
    {
        _formatter.Format(Payload("PULL_EMPLOYEES")).Should().Be("C:1:DATA QUERY USERINFO");
    }

    [Fact]
    public void Format_PullAttendance_WithBothTimes_IncludesRange()
    {
        var payload = Payload("PULL_ATTENDANCE", new Dictionary<string, object?>
        {
            ["start_time"] = "2026-01-01 00:00:00",
            ["end_time"] = "2026-01-02 00:00:00",
        });

        _formatter.Format(payload).Should().Be(
            "C:1:DATA QUERY ATTLOG StartTime=2026-01-01 00:00:00 EndTime=2026-01-02 00:00:00");
    }

    [Fact]
    public void Format_PullAttendance_WithoutTimes_HasNoRange()
    {
        _formatter.Format(Payload("PULL_ATTENDANCE")).Should().Be("C:1:DATA QUERY ATTLOG");
    }

    // --- DELETE_USER / DELETE_BIOMETRICS ------------------------------------------------------

    [Fact]
    public void Format_DeleteUser_WithPin_ReturnsDeleteCommand()
    {
        var payload = Payload("DELETE_USER", new Dictionary<string, object?> { ["pin"] = "1001" });

        _formatter.Format(payload).Should().Be("C:1:DATA DELETE USERINFO PIN=1001");
    }

    [Fact]
    public void Format_DeleteUser_WithoutPin_Throws()
    {
        var act = () => _formatter.Format(Payload("DELETE_USER"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Format_DeleteBiometrics_WithAllIndex_OmitsFingerId()
    {
        var payload = Payload("DELETE_BIOMETRICS", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["template_index"] = "all",
        });

        _formatter.Format(payload).Should().Be("C:1:DATA DELETE templatev10 PIN=1001");
    }

    [Fact]
    public void Format_DeleteBiometrics_WithSpecificIndex_IncludesFingerId()
    {
        var payload = Payload("DELETE_BIOMETRICS", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["template_index"] = "3",
        });

        _formatter.Format(payload).Should().Be("C:1:DATA DELETE templatev10 PIN=1001\tFingerID=3");
    }

    [Fact]
    public void Format_DeleteBiometrics_WithoutPin_Throws()
    {
        var act = () => _formatter.Format(Payload("DELETE_BIOMETRICS"));

        act.Should().Throw<ArgumentException>();
    }

    // --- SYNC_BIOMETRICS -----------------------------------------------------------------------

    [Fact]
    public void Format_SyncBiometrics_WithoutPin_Throws()
    {
        var payload = Payload("SYNC_BIOMETRICS", new Dictionary<string, object?> { ["biometric_type"] = "fingerprint" });

        var act = () => _formatter.Format(payload);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Format_SyncBiometrics_UnsupportedType_Throws()
    {
        var payload = Payload("SYNC_BIOMETRICS", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["biometric_type"] = "iris",
        });

        var act = () => _formatter.Format(payload);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Format_SyncBiometrics_Fingerprint_MissingTemplateData_Throws()
    {
        var payload = Payload("SYNC_BIOMETRICS", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["biometric_type"] = "fingerprint",
        });

        var act = () => _formatter.Format(payload);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Format_SyncBiometrics_Fingerprint_Basic_UsesTemplateLengthAsSize()
    {
        var payload = Payload("SYNC_BIOMETRICS", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["biometric_type"] = "fingerprint",
            ["template_index"] = "2",
            ["template_data"] = "AAEC",
        });

        _formatter.Format(payload).Should().Be("C:1:DATA UPDATE FINGERTMP PIN=1001\tFID=2\tSize=4\tTMP=AAEC");
    }

    [Fact]
    public void Format_SyncBiometrics_Fingerprint_ExplicitDataSize_OverridesComputedSize()
    {
        var payload = Payload("SYNC_BIOMETRICS", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["biometric_type"] = "fingerprint",
            ["template_data"] = "AAEC",
            ["data_size"] = 999,
        });

        _formatter.Format(payload).Should().Contain("Size=999");
    }

    [Theory]
    [InlineData(true, "Valid=1")]
    [InlineData("0", "Valid=0")]
    public void Format_SyncBiometrics_Fingerprint_IsValidFlag_IsIncludedWhenProvided(object isValid, string expectedFragment)
    {
        var payload = Payload("SYNC_BIOMETRICS", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["biometric_type"] = "fingerprint",
            ["template_data"] = "AAEC",
            ["is_valid"] = isValid,
        });

        _formatter.Format(payload).Should().Contain(expectedFragment);
    }

    [Fact]
    public void Format_SyncBiometrics_Fingerprint_UdpTransport_ReencodesTemplate()
    {
        var rawBytes = new byte[] { 0, 1, 2 };
        var templateData = Convert.ToBase64String(rawBytes);

        var payload = Payload("SYNC_BIOMETRICS", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["biometric_type"] = "fingerprint",
            ["template_data"] = " " + templateData + " ",
            ["template_transport"] = "udp",
        });

        var expected = Convert.ToBase64String(rawBytes);
        _formatter.Format(payload).Should().Contain($"TMP={expected}").And.Contain($"Size={expected.Length}");
    }

    [Fact]
    public void Format_SyncBiometrics_Face_UploadPrefixed_SlicesFirst16Bytes()
    {
        var fullBytes = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
        var templateData = Convert.ToBase64String(fullBytes);
        var expectedSlice = Convert.ToBase64String(fullBytes[16..]);

        var payload = Payload("SYNC_BIOMETRICS", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["biometric_type"] = "face",
            ["template_data"] = templateData,
            ["face_upload_prefixed"] = true,
        });

        var result = _formatter.Format(payload);

        result.Should().Contain($"TMP={expectedSlice}");
        result.Should().Contain($"Size={expectedSlice.Length}");
    }

    [Fact]
    public void Format_SyncBiometrics_Photo_UsesFileNameFallbackFromPin()
    {
        var payload = Payload("SYNC_BIOMETRICS", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["biometric_type"] = "photo",
            ["template_data"] = "AAEC",
        });

        _formatter.Format(payload).Should().Be("C:1:DATA UPDATE USERPIC PIN=1001\tFileName=1001.jpg\tSize=4\tContent=AAEC");
    }

    // --- ENROLL_FINGERPRINT / ENROLL_FACE -------------------------------------------------------

    [Fact]
    public void Format_EnrollFingerprint_ClampsFidAndRetry_DefaultsOverwriteToTrue()
    {
        var payload = Payload("ENROLL_FINGERPRINT", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["fid"] = 15,
            ["retry"] = 20,
        });

        _formatter.Format(payload).Should().Be("C:1:ENROLL_FP PIN=1001\tFID=9\tRETRY=15\tOVERWRITE=1");
    }

    [Fact]
    public void Format_EnrollFingerprint_WithoutPin_Throws()
    {
        var act = () => _formatter.Format(Payload("ENROLL_FINGERPRINT"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Format_EnrollFace_ClampsRetryAndType_IncludesCardNumberWhenPresent()
    {
        var payload = Payload("ENROLL_FACE", new Dictionary<string, object?>
        {
            ["pin"] = "1001",
            ["card_no"] = "CARD1",
            ["retry"] = 0,
            ["type"] = 15,
            ["overwrite"] = false,
        });

        _formatter.Format(payload).Should().Be("C:1:ENROLL_FACE TYPE=9\tPIN=1001\tCardNo=CARD1\tRETRY=1\tOVERWRITE=0");
    }

    [Fact]
    public void Format_EnrollFace_WithoutCardNumber_OmitsCardNoField()
    {
        var payload = Payload("ENROLL_FACE", new Dictionary<string, object?> { ["pin"] = "1001" });

        _formatter.Format(payload).Should().NotContain("CardNo=");
    }

    [Fact]
    public void Format_EnrollFace_WithoutPin_Throws()
    {
        var act = () => _formatter.Format(Payload("ENROLL_FACE"));

        act.Should().Throw<ArgumentException>();
    }

    // --- Raw / unknown commands and CommandPayload precedence -----------------------------------

    [Fact]
    public void Format_UnknownCommand_PrefixesWithDeviceId()
    {
        _formatter.Format(Payload("PING")).Should().Be("C:1:PING");
    }

    [Fact]
    public void Format_UnknownCommand_AlreadyPrefixed_PassesThroughUnchanged()
    {
        var payload = Payload("C:1:CUSTOM RAW COMMAND");

        _formatter.Format(payload).Should().Be("C:1:CUSTOM RAW COMMAND");
    }

    [Fact]
    public void Format_CommandPayload_TakesPrecedenceOverCommand()
    {
        var payload = Payload("REBOOT", commandPayload: "SET OPTION Foo=Bar");

        _formatter.Format(payload).Should().Be("C:1:SET OPTION Foo=Bar");
    }

    [Fact]
    public void Format_CommandPayload_AlreadyPrefixed_PassesThroughUnchanged()
    {
        var payload = Payload("REBOOT", commandPayload: "C:9:RAW PAYLOAD");

        _formatter.Format(payload).Should().Be("C:9:RAW PAYLOAD");
    }
}
