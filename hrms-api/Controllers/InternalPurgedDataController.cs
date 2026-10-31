using Asp.Versioning;
using Hrms.Core.Services.Auditing;
using Hrms.Core.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Onepunch.AuditEngine;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Api.Controllers
{
    /// <summary>A short-lived link to one company's archive. <see cref="Url"/> is null when the storage can't hand out links (local disk).</summary>
    public sealed record InternalArchiveLink(string? Url, string FileName, int ExpiresInMinutes);

    // Service-to-service: the Platform Console (through Tenant Service) reads what is kept of a PURGED
    // company -- its audit trail (the yearly purge keeps it) and the link to its compliance archive. Api-key
    // protected, no user token. Tenant Service has already checked that the operator may do this, took a
    // reason and recorded it; the company is the route's.
    [Route("api/v{version:apiVersion}/internal/purged/{tenantId:guid}")]
    [ApiVersion("1.0")]
    [ApiController]
    [AllowAnonymous]
    [ApiKey]
    public class InternalPurgedDataController : ControllerBase
    {
        // Matches the lifetime of the link GetDownloadUrlAsync hands out.
        private const int LinkMinutes = 10;

        private readonly IAuditQueryService _audits;
        private readonly IFileStorage _storage;

        public InternalPurgedDataController(IAuditQueryService audits, IFileStorage storage)
        {
            _audits = audits;
            _storage = storage;
        }

        private static Guid Company(Guid tenantId) =>
            tenantId == Guid.Empty ? throw new GuardException("No company was given.") : tenantId;

        /// <summary>The company's retained audit trail, read-only and always scoped to the route's company.</summary>
        [HttpGet("audit")]
        [ProducesResponseType(typeof(ResponseModel<AuditLogPage>), 200)]
        public async Task<IActionResult> Audit(Guid tenantId, [FromQuery] AuditLogQuery query, CancellationToken token)
        {
            var page = await _audits.QueryAsync(AuditLogPresenter.ToEngineQuery(query, Company(tenantId).ToString()), token);
            return Ok(AuditLogPresenter.ToPage(page));
        }

        /// <summary>A short-lived download link for one archive of the company. The key must be inside the company's archive folder.</summary>
        [HttpGet("archive-link")]
        [ProducesResponseType(typeof(ResponseModel<InternalArchiveLink>), 200)]
        public async Task<IActionResult> ArchiveLink(Guid tenantId, [FromQuery] string archiveKey, CancellationToken token)
        {
            var key = archiveKey;
            if (!ArchiveBelongsTo(key, Company(tenantId))) throw new GuardException("That isn't one of this company's archives.");
            if (!await _storage.ExistsAsync(key, token)) throw new GuardException("This archive no longer exists.");

            var fileName = key[(key.LastIndexOf('/') + 1)..];
            var url = await _storage.GetDownloadUrlAsync(key, fileName, token);
            return Ok(new InternalArchiveLink(url?.ToString(), fileName, LinkMinutes));
        }

        /// <summary>Only a key inside <c>archives/{company}/</c>: never trust one that came from a request as it is.</summary>
        public static bool ArchiveBelongsTo(string? key, Guid tenantId) =>
            StorageKeys.IsValid(key) && key!.StartsWith(StorageKeys.ArchivePrefix(tenantId), StringComparison.Ordinal);
    }
}
