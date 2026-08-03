using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;

namespace RentMaster.Api.Controllers;

[ApiController]
[Route("api/v1/properties")]
public sealed class PropertiesController(IPropertyService propertyService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<OwnerPropertyDto>> Create(
        CreatePropertyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await propertyService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDetails), new { id = result.Id }, result);
    }

    [HttpGet("mine")]
    [Authorize(Roles = AppRoles.Owner)]
    public Task<PagedResult<OwnerPropertyDto>> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        propertyService.GetMineAsync(page, pageSize, cancellationToken);

    [HttpGet("search")]
    public Task<PagedResult<PublicPropertyDto>> Search(
        [FromQuery] PropertySearchRequest request,
        CancellationToken cancellationToken) =>
        propertyService.SearchAsync(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<PublicPropertyDetailsDto> GetDetails(
        Guid id,
        CancellationToken cancellationToken) =>
        propertyService.GetPublicDetailsAsync(id, cancellationToken);

    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.Owner)]
    public Task<OwnerPropertyDto> Update(
        Guid id,
        UpdatePropertyRequest request,
        CancellationToken cancellationToken) =>
        propertyService.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await propertyService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/photos")]
    [Authorize(Roles = AppRoles.Owner)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<ActionResult<PropertyPhotoDto>> AddPhoto(
        Guid id,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { detail = "Choose a property photo." });
        await using var stream = file.OpenReadStream();
        var result = await propertyService.AddPhotoAsync(
            id,
            new UploadPropertyPhotoCommand(file.FileName, file.ContentType, file.Length, stream),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{propertyId:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> Photo(
        Guid propertyId,
        Guid photoId,
        CancellationToken cancellationToken)
    {
        var photo = await propertyService.OpenPhotoAsync(propertyId, photoId, cancellationToken);
        Response.Headers["Cache-Control"] = "private,max-age=3600";
        return File(photo.Content, photo.ContentType);
    }

    [HttpDelete("{propertyId:guid}/photos/{photoId:guid}")]
    [Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> DeletePhoto(
        Guid propertyId,
        Guid photoId,
        CancellationToken cancellationToken)
    {
        await propertyService.DeletePhotoAsync(propertyId, photoId, cancellationToken);
        return NoContent();
    }
}
