using Hrms.adms.Models.DTO;
using System.Text.RegularExpressions;

namespace Hrms.adms.Utilities;

public class DeviceCommandFormmaterPayload
{
    public required string Id { get; set; }
    public string? Command { get; set; }
    public string? CommandPayload { get; set; }
    public Dictionary<string, object?>? Parameters { get; set; }
}

public class ZKTecoCommandFormatter(ISystemClockService systemClockService)
{
    protected readonly ISystemClockService _systemClockService = systemClockService;

    public string Format(DeviceCommandFormmaterPayload command)
    {
        string payload = (command.CommandPayload ?? "").ToString().Trim();
        if (!string.IsNullOrEmpty(payload))
        {
            string preCommand = payload.StartsWith("C:") ? payload : $"C:{command.Id}:{payload}";
            return preCommand;
        }

        return command.Command?.ToUpperInvariant() switch
        {
            "RESTART" or "REBOOT" => $"C:{command.Id}:REBOOT",
            "CLEAR_LOGS" => $"C:{command.Id}:CLEAR LOG",
            "SET_TIME" or "UPDATE_TIME" => FormatTimeCommand(command),
            "SET_TIMEZONE" => FormatOptionCommand(command),
            "ENABLE_ATTENDANCE" => $"C:{command.Id}:SET OPTION Realtime=1",
            "DISABLE_ATTENDANCE" => $"C:{command.Id}:SET OPTION Realtime=0",
            "SET_ATTENDANCE_MODE" => FormatOptionCommand(command),
            "SYNC_EMPLOYEES" => FormatUserCommand(command),
            "SYNC_BIOMETRICS" => FormatBiometricCommand(command),
            "ENROLL_FINGERPRINT" => FormatFingerprintEnrollmentCommand(command),
            "ENROLL_FACE" => FormatFaceEnrollmentCommand(command),
            "PULL_EMPLOYEES" => FormatEmployeePullCommand(command),
            "PULL_ATTENDANCE" => FormatAttendancePullCommand(command),
            "RM_ADMIN_PRIVILEGE" => $"C:{command.Id}:CLEAR ADMIN",
            "DELETE_USER" => FormatDelete(command),
            "DELETE_BIOMETRICS" => FormatDeleteFP(command),
            //"RM_ADMIN_PRIVILEGE" => $"C:{command.Id}:SET OPTION ClearAdmin=1",
            //"RM_ADMIN_PRIVILEGE" => $"C:{command.Id}:ClearAdmin",
            _ => FormatRawCommand(command)
        };
    }

    private string FormatTimeCommand(DeviceCommandFormmaterPayload command)
    {
        var parameters = command.Parameters ?? [];

        if (parameters.TryGetValue("option", out var option) && option != null && parameters.ContainsKey("value"))
        {
            return $"C:{command.Id}:SET OPTION {option}={parameters["value"]}";
        }

        parameters.TryGetValue("timestamp", out var timestamp);
        string normalizedTimestamp = _systemClockService.NormalizeForDevice(timestamp);

        return $"C:{command.Id}:SET OPTION DateTime={normalizedTimestamp}";
    }

    private string FormatOptionCommand(DeviceCommandFormmaterPayload command)
    {
        var parameters = command.Parameters ?? [];

        string option = Sanitize(parameters.TryGetValue("option", out var o) ? o?.ToString() : "Realtime");
        string value = Sanitize(parameters.TryGetValue("value", out var v) ? v?.ToString() : "1");

        return $"C:{command.Id}:SET OPTION {option}={value}";
    }

    private string FormatUserCommand(DeviceCommandFormmaterPayload command)
    {
        var parameters = command.Parameters ?? [];
        string action = (parameters.TryGetValue("action", out var act) ? act?.ToString() : "add_user")?.ToLowerInvariant() ?? "add_user";

        if (action == "query")
        {
            string queryPin = Sanitize(GetParameterFallback(parameters, "pin", "employee_code"));
            return $"C:{command.Id}:DATA QUERY USERINFO PIN={queryPin}";
        }

        string pin = Sanitize(GetParameterFallback(parameters, "pin", "employee_code"));
        string name = Sanitize(parameters.TryGetValue("name", out var n) ? n?.ToString() : "Unknown User");
        string card = Sanitize(parameters.TryGetValue("card_no", out var c) ? c?.ToString() : "");

        int privilege = parameters.TryGetValue("privilege", out var priv) ? Convert.ToInt32(priv) : 0;
        string password = Sanitize(parameters.TryGetValue("password", out var pwd) ? pwd?.ToString() : "");

        var fields = new List<string>
            {
                $"PIN={pin}",
                $"Name={name}",
                $"Pri={Math.Max(0, Math.Min(privilege, 14))}",
                $"Passwd={password}",
                $"enable=true",
                $"Card={card}"
            };

        var optionalFields = new Dictionary<string, object?>
            {
                { "Grp", parameters.GetValueOrDefault("group_code") },
                { "TZ", parameters.GetValueOrDefault("timezone_code") },
                { "Verify", parameters.GetValueOrDefault("verification_mode") },
                { "ViceCard", parameters.GetValueOrDefault("vice_card_no") },
                { "StartDatetime", parameters.GetValueOrDefault("valid_from") },
                { "EndDatetime", parameters.GetValueOrDefault("valid_until") }
            };

        foreach (var kvp in optionalFields)
        {
            string? valStr = kvp.Value?.ToString();
            if (string.IsNullOrEmpty(valStr)) continue;

            fields.Add($"{kvp.Key}={Sanitize(valStr)}");
        }

        return $"C:{command.Id}:DATA UPDATE USERINFO " + string.Join("\t", fields);
    }

    private string FormatEmployeePullCommand(DeviceCommandFormmaterPayload command)
    {
        var parameters = command.Parameters ?? [];
        string pin = Sanitize(GetParameterFallback(parameters, "pin", "employee_code"));

        if (!string.IsNullOrEmpty(pin))
        {
            return $"C:{command.Id}:DATA QUERY USERINFO PIN={pin}";
        }

        return $"C:{command.Id}:DATA QUERY USERINFO";
    }

    private string FormatAttendancePullCommand(DeviceCommandFormmaterPayload command)
    {
        var parameters = command.Parameters ?? [];
        var parts = new List<string> { $"C:{command.Id}:DATA QUERY ATTLOG" };

        string startTime = Sanitize(parameters.GetValueOrDefault("start_time")?.ToString() ?? "");
        string endTime = Sanitize(parameters.GetValueOrDefault("end_time")?.ToString() ?? "");

        if (!string.IsNullOrEmpty(startTime)) parts.Add($"StartTime={startTime}");
        if (!string.IsNullOrEmpty(endTime)) parts.Add($"EndTime={endTime}");

        return string.Join(" ", parts);
    }

    private string FormatBiometricCommand(DeviceCommandFormmaterPayload command)
    {
        var parameters = command.Parameters ?? [];
        string pin = Sanitize(GetParameterFallback(parameters, "pin", "employee_code"));
        string biometricType = (parameters.GetValueOrDefault("biometric_type")?.ToString() ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(pin))
        {
            throw new ArgumentException("Biometric sync command requires a device PIN");
        }

        return biometricType switch
        {
            "fingerprint" => FormatFingerprintCommand(command, pin, parameters),
            "face" => FormatFaceCommand(command, pin, parameters),
            "photo" => FormatPhotoCommand(command, pin, parameters),
            _ => throw new ArgumentException("Unsupported biometric sync command type")
        };
    }
    private string FormatDelete(DeviceCommandFormmaterPayload command)
    {
        var parameters = command.Parameters ?? [];
        string pin = Sanitize(GetParameterFallback(parameters, "pin", "employee_code"));
        if (string.IsNullOrEmpty(pin))
        {
            throw new ArgumentException("Biometric sync command requires a device PIN");
        }

        var fields = new List<string>
            {
                $"PIN={pin}",
            };
        return $"C:{command.Id}:DATA DELETE USERINFO " + string.Join("\t", fields);
    }

    private string FormatDeleteFP(DeviceCommandFormmaterPayload command)
    {
        var parameters = command.Parameters ?? [];
        string pin = Sanitize(GetParameterFallback(parameters, "pin", "employee_code"));
        if (string.IsNullOrEmpty(pin))
        {
            throw new ArgumentException("Biometric sync command requires a device PIN");
        }

        var fields = new List<string>
            {
                $"PIN={pin}",
            };

        string fingerIndex = Sanitize(parameters.GetValueOrDefault("template_index")?.ToString() ?? "");
        if (!string.IsNullOrEmpty(fingerIndex) && !fingerIndex.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            fields.Add($"FingerID={fingerIndex}");
        }
        return $"C:{command.Id}:DATA DELETE templatev10 " + string.Join("\t", fields);
    }

    private string FormatDeleteFace(DeviceCommandFormmaterPayload command)
    {
        var parameters = command.Parameters ?? [];
        string pin = Sanitize(GetParameterFallback(parameters, "pin", "employee_code"));
        if (string.IsNullOrEmpty(pin))
        {
            throw new ArgumentException("Biometric sync command requires a device PIN");
        }

        var fields = new List<string>
            {
                $"PIN={pin}",
            };
        return $"C:{command.Id}:DATA DELETE face " + string.Join("\t", fields);
    }


    private string FormatFingerprintCommand(DeviceCommandFormmaterPayload command, string pin, Dictionary<string, object?> parameters)
    {
        var transfer = PrepareBinaryTransfer(parameters, "fingerprint");
        string fid = Sanitize(parameters.GetValueOrDefault("template_index")?.ToString() ?? "0");

        var fields = new List<string>
            {
                $"PIN={pin}",
                $"FID={fid}",
                $"Size={transfer.Size}"
            };

        if (parameters.TryGetValue("is_valid", out var isValid) && isValid != null && !string.IsNullOrEmpty(isValid.ToString()))
        {
            fields.Add($"Valid={(NormalizeBooleanParameter(isValid) ? "1" : "0")}");
        }

        fields.Add($"TMP={transfer.Data}");

        return $"C:{command.Id}:DATA UPDATE FINGERTMP " + string.Join("\t", fields);
    }

    private string FormatFaceCommand(DeviceCommandFormmaterPayload command, string pin, Dictionary<string, object?> parameters)
    {
        var transfer = PrepareBinaryTransfer(parameters, "face");
        string fid = Sanitize(parameters.GetValueOrDefault("template_index")?.ToString() ?? "50");

        var fields = new List<string>
            {
                $"PIN={pin}",
                $"FID={fid}",
                $"Size={transfer.Size}"
            };

        if (parameters.TryGetValue("is_valid", out var isValid) && isValid != null && !string.IsNullOrEmpty(isValid.ToString()))
        {
            fields.Add($"Valid={(NormalizeBooleanParameter(isValid) ? "1" : "0")}");
        }

        fields.Add($"TMP={transfer.Data}");

        return $"C:{command.Id}:DATA UPDATE FACE " + string.Join("\t", fields);
    }

    private string FormatPhotoCommand(DeviceCommandFormmaterPayload command, string pin, Dictionary<string, object?> parameters)
    {
        string fileName = Sanitize(parameters.GetValueOrDefault("file_name")?.ToString() ?? $"{pin}.jpg");
        var transfer = PrepareBinaryTransfer(parameters, "photo");

        var fields = new List<string>
            {
                $"PIN={pin}",
                $"FileName={fileName}",
                $"Size={transfer.Size}",
                $"Content={transfer.Data}"
            };

        return $"C:{command.Id}:DATA UPDATE USERPIC " + string.Join("\t", fields);
    }

    private string FormatFingerprintEnrollmentCommand(DeviceCommandFormmaterPayload command)
    {
        var parameters = command.Parameters ?? [];
        string pin = Sanitize(GetParameterFallback(parameters, "pin", "employee_code"));

        if (string.IsNullOrEmpty(pin))
        {
            throw new ArgumentException("Fingerprint enrollment command requires a device PIN");
        }

        int rawFid = parameters.TryGetValue("fid", out var f) ? Convert.ToInt32(f) :
                    parameters.TryGetValue("template_index", out var ti) ? Convert.ToInt32(ti) : 0;
        int fingerIndex = Math.Max(0, Math.Min(rawFid, 9));

        int rawRetry = parameters.TryGetValue("retry", out var r) ? Convert.ToInt32(r) : 3;
        int retry = Math.Max(1, Math.Min(rawRetry, 15));

        string overwrite = NormalizeBooleanParameter(parameters.GetValueOrDefault("overwrite") ?? true) ? "1" : "0";

        return $"C:{command.Id}:ENROLL_FP PIN={pin}\tFID={fingerIndex}\tRETRY={retry}\tOVERWRITE={overwrite}";
    }

    private string FormatFaceEnrollmentCommand(DeviceCommandFormmaterPayload command)
    {
        var parameters = command.Parameters ?? [];
        string pin = Sanitize(GetParameterFallback(parameters, "pin", "employee_code"));

        if (string.IsNullOrEmpty(pin))
        {
            throw new ArgumentException("Face enrollment command requires a device PIN");
        }

        int rawRetry = parameters.TryGetValue("retry", out var r) ? Convert.ToInt32(r) : 3;
        int retry = Math.Max(1, Math.Min(rawRetry, 15));

        string overwrite = NormalizeBooleanParameter(parameters.GetValueOrDefault("overwrite") ?? true) ? "1" : "0";

        // Note: config('zkteco.face_enrollment_type', 2) fallback logic mapped directly to fallback int: 2
        int rawType = parameters.TryGetValue("type", out var t) ? Convert.ToInt32(t) : 2;
        int faceType = Math.Max(0, Math.Min(rawType, 9));

        var fields = new List<string>
            {
                $"TYPE={faceType}",
                $"PIN={pin}"
            };

        string cardNumber = Sanitize(parameters.GetValueOrDefault("card_no")?.ToString() ?? "");
        if (!string.IsNullOrEmpty(cardNumber))
        {
            fields.Add($"CardNo={cardNumber}");
        }

        fields.Add($"RETRY={retry}");
        fields.Add($"OVERWRITE={overwrite}");

        return $"C:{command.Id}:ENROLL_FACE " + string.Join("\t", fields);
    }

    private string FormatRawCommand(DeviceCommandFormmaterPayload command)
    {
        string commandText = (command.Command ?? "").Trim();
        return commandText.StartsWith("C:", StringComparison.OrdinalIgnoreCase) ? commandText : $"C:{command.Id}:{commandText}";
    }

    private string Sanitize(string? value)
    {
        if (value == null) return "";
        return value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
    }

    private (string Data, int Size) PrepareBinaryTransfer(Dictionary<string, object?> parameters, string type)
    {
        string rawTemplate = (parameters.GetValueOrDefault("template_data")?.ToString() ?? "").Trim();
        if (string.IsNullOrEmpty(rawTemplate))
        {
            throw new ArgumentException($"{char.ToUpper(type[0])}{type[1..]} sync command requires template data");
        }

        string transport = (parameters.GetValueOrDefault("template_transport")?.ToString() ?? "").Trim().ToLowerInvariant();
        bool faceUploadPrefixed = type == "face" && NormalizeBooleanParameter(parameters.GetValueOrDefault("face_upload_prefixed") ?? false);

        string normalizedTemplate = Regex.Replace(rawTemplate, @"\s+", "");

        byte[]? decodedBytes = null;
        try
        {
            decodedBytes = Convert.FromBase64String(normalizedTemplate);
        }
        catch (FormatException) { /* Keep null if invalid Base64 */ }

        if (faceUploadPrefixed && decodedBytes != null && decodedBytes.Length > 16)
        {
            // Equivalent to PHP substr($decoded, 16)
            byte[] sliced = decodedBytes[16..];
            string encoded = Convert.ToBase64String(sliced);

            return (encoded, encoded.Length);
        }

        if (transport == "udp")
        {
            string encoded = Convert.ToBase64String(decodedBytes ?? System.Text.Encoding.UTF8.GetBytes(rawTemplate));
            return (encoded, encoded.Length);
        }

        return (rawTemplate, ResolveTransferSize(parameters, rawTemplate));
    }

    private int ResolveTransferSize(Dictionary<string, object?> parameters, string payload)
    {
        if (parameters.TryGetValue("data_size", out var sizeObj) && sizeObj != null && !string.IsNullOrEmpty(sizeObj.ToString()))
        {
            return Math.Max(0, Convert.ToInt32(sizeObj));
        }
        return payload.Length;
    }

    private bool NormalizeBooleanParameter(object? value)
    {
        if (value is bool b) return b;
        if (value == null) return false;

        string valStr = value.ToString()!.Trim().ToLowerInvariant();
        return valStr switch
        {
            "0" or "false" or "off" or "no" => false,
            _ => true
        };
    }

    // Helper shortcut function mimicking array fallback rules ($a['pin'] ?? $a['employee_code'] ?? '')
    private string GetParameterFallback(Dictionary<string, object?> parameters, string primaryKey, string fallbackKey)
    {
        if (parameters.TryGetValue(primaryKey, out var val) && val != null) return val.ToString() ?? "";
        if (parameters.TryGetValue(fallbackKey, out var fallbackVal) && fallbackVal != null) return fallbackVal.ToString() ?? "";
        return "";
    }
}