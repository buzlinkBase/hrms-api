using System.Security.Claims;
using Hrms.Api.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace hrms.test.FilterTests;

/// <summary>
/// "Your account isn't linked to an Employee record" used to leave the user guessing what to ask
/// an admin for. It now names the caller's own login email -- putting that email on the Employee
/// record is all that's needed to link them (EmployeeService.ResolveEmployeeIdAsync falls back to
/// matching Employee.Email). Services can't know the caller, so GlobalExceptionHandler fills it in.
/// </summary>
public class AccountNotLinkedMessageTests
{
    [Fact]
    public void MessageFor_NamesTheEmailAndWhatToDoWithIt()
    {
        var ex = new AccountNotLinkedException("this approval action can't be recorded");

        var message = ex.MessageFor("jane@company.com");

        message.Should().Contain("(jane@company.com)")
            .And.Contain("so this approval action can't be recorded.")
            .And.Contain("enter jane@company.com in the Email field of your Employee record");
    }

    [Fact]
    public void Message_WithoutEmail_StillSaysWhatToDo()
    {
        var ex = new AccountNotLinkedException("this approval action can't be recorded");

        ex.Message.Should().Contain("enter your login email in the Email field of your Employee record");
        ex.Should().BeAssignableTo<InvalidOperationException>("existing InvalidOperationException handling keeps working");
    }

    [Fact]
    public async Task GlobalExceptionHandler_ReturnsBadRequestNamingTheCallersEmail()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("email", "jane@company.com")], "test")),
        };
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(Substitute.For<IHostEnvironment>());

        await handler.TryHandleAsync(context, new AccountNotLinkedException("this approval action can't be recorded"), CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Contain("jane@company.com in the Email field of your Employee record");
    }
}
