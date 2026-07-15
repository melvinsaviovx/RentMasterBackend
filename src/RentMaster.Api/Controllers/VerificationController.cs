using Microsoft.AspNetCore.Mvc;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Enums;

namespace RentMaster.Api.Controllers;

[ApiController]
[Route("api/v1/verification")]
public sealed class VerificationController(
    IIdentityVerificationService verificationService)
    : ControllerBase
{
    [HttpPost("documents")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<IdentityDocumentDto>> Submit(
        [FromForm] IdentityDocumentType documentType,
        [FromForm] string documentNumber,
        [FromForm] bool consentAccepted,
        [FromForm] string consentVersion,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();

        var result = await verificationService.SubmitAsync(
            new SubmitIdentityDocumentCommand(
                documentType,
                documentNumber,
                file.FileName,
                file.ContentType,
                file.Length,
                stream,
                consentAccepted,
                consentVersion,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("status")]
    public Task<VerificationStatusDto> Status(CancellationToken cancellationToken) =>
        verificationService.GetMyStatusAsync(cancellationToken);
}
