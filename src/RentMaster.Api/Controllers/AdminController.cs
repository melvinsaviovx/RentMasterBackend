using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;

namespace RentMaster.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Moderator}")]
public sealed class AdminController(
    IIdentityVerificationService verificationService,
    IReviewService reviewService)
    : ControllerBase
{
    [HttpGet("verification/pending")]
    public Task<PagedResult<PendingIdentityDocumentDto>> PendingDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        verificationService.GetPendingAsync(page, pageSize, cancellationToken);

    [HttpGet("verification/{documentId:guid}/file")]
    public async Task<IActionResult> DocumentFile(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var file = await verificationService.OpenDocumentAsync(documentId, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        return File(file.Content, file.ContentType, file.DownloadFileName);
    }

    [HttpPost("verification/{documentId:guid}/decision")]
    public async Task<IActionResult> DecideDocument(
        Guid documentId,
        VerificationDecisionRequest request,
        CancellationToken cancellationToken)
    {
        await verificationService.DecideAsync(documentId, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("reviews/pending")]
    public Task<PagedResult<PendingReviewDto>> PendingReviews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        reviewService.GetPendingAsync(page, pageSize, cancellationToken);

    [HttpPost("reviews/{reviewId:guid}/decision")]
    public async Task<IActionResult> DecideReview(
        Guid reviewId,
        ReviewDecisionRequest request,
        CancellationToken cancellationToken)
    {
        await reviewService.DecideAsync(reviewId, request, cancellationToken);
        return NoContent();
    }
}
