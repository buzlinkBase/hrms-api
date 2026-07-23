namespace Hrms.Core;

public class CutoffMismatchException : Exception
{
    public CutoffMismatchException(string message) : base(message) { }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }
    public ValidationException(string message, Exception exception) : base(message, exception)
    {
    }
}