using Asp.Versioning;
using Hrms.Core.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    // What the caller may see of confidential employees -- drives the "N confidential employees
    // are excluded" notice on report, payroll and DTR summary screens. Kept out of MeController,
    // which is about the caller's own employee records.
    [Route("api/v{version:apiVersion}/me")]
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    public class DataAccessController : ControllerBase
    {
        private readonly ConfidentialAccessScope _access;

        public DataAccessController(ConfidentialAccessScope access) => _access = access;

        [HttpGet("data-access")]
        [ProducesResponseType(typeof(ResponseModel<DataAccessModel>), 200)]
        public IActionResult Get() =>
            Ok(new DataAccessModel(
                _access.Restricted,
                _access.HiddenEmployeeCount,
                _access.Grants.Operations,
                _access.Grants.Entries));
    }

    // HiddenEmployeeCount: confidential employees missing from pay figures and reports.
    // CanOperate / CanEnter: the caller's partial clearances (see ConfidentialGrants).
    public record DataAccessModel(bool Restricted, int HiddenEmployeeCount, bool CanOperate, bool CanEnter);
}
