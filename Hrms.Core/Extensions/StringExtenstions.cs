using Hrms.Domain.Entities.EmployeeEntities;
using System.Text;

namespace Hrms.Core.Extensions;
public static class StringFormatter
{
    public static string FormatCode(this int count)
    {
        return count.ToString().PadLeft(6, '0');
    }
}

public static class Extensions
{
    public static string FullName(this Employee? employee)
    {
        if (employee == null) return "";
        string full_name = $"{employee.LastName}, {employee.FirstName} {employee.Suffix} {employee.MiddleName}";
        if (full_name.Trim().StartsWith(","))
        {
            full_name = full_name.Substring(1, full_name.Length - 1);
        }
        else if (full_name.Trim().StartsWith("-, "))
        {
            full_name = full_name.Substring(3, full_name.Length - 3);
        }
        return full_name;
    }

    public static string FullName(this EmployeeDto? employee)
    {
        if (employee == null) return "";
        string full_name = $"{employee.LastName}, {employee.FirstName} {employee.Suffix} {employee.MiddleName}";
        if (full_name.Trim().StartsWith(","))
        {
            full_name = full_name.Substring(1, full_name.Length - 1);
        }
        else if (full_name.Trim().StartsWith("-, "))
        {
            full_name = full_name.Substring(3, full_name.Length - 3);
        }
        return full_name;
    }


    public static string RemoveSpacesBeforeCaps(this string input)
    {
        input = input.Trim();
        if (string.IsNullOrEmpty(input) || input.Length == 1)
            return input;

        var result = new StringBuilder(input.Length);
        for (int i = 0; i < input.Length; i++)
        {
            char current = input[i];
            if (current == ' ' && i + 1 < input.Length && char.IsUpper(input[i + 1]))
            {
                continue;
            }
            result.Append(current);
        }
        return result.ToString();
    }

}
