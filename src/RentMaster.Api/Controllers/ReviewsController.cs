using Microsoft.AspNetCore.Mvc;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;

namespace RentMaster.Api.Controllers;

[ApiController]
[Route("api/v1/reviews")]
public sealed class ReviewsController(IReviewService reviewService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<object>> Submit(
        SubmitReviewRequest request,
        CancellationToken cancellationToken)
    {
        var id = await reviewService.SubmitAsync(request, cancellationToken);
        return Accepted(new { reviewId = id, status = "PendingModeration" });
    }


    [HttpPost("{id:guid}/dispute")]
    public async Task<IActionResult> Dispute(
        Guid id,
        DisputeReviewRequest request,
        CancellationToken cancellationToken)
    {
        await reviewService.DisputeAsync(id, request, cancellationToken);
        return NoContent();
    }
}
