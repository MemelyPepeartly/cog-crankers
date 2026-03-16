using CogSlop.Api.Models.Dtos;
using CogSlop.Api.Models.Requests;
using CogSlop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CogSlop.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.AdminOnly)]
[Route("api/[controller]")]
public class AdminController(IAdminService adminService) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserSummaryDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await adminService.GetUsersAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("gear-items")]
    public async Task<ActionResult<IReadOnlyList<StoreItemDto>>> GetGearItems(
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        var items = await adminService.GetGearItemsAsync(includeInactive, cancellationToken);
        return Ok(items);
    }

    [HttpPost("grant-cogs")]
    public async Task<ActionResult<AdminUserSummaryDto>> GrantCogs(
        [FromBody] GrantCogsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await adminService.GrantCogsAsync(User, request, cancellationToken);
            return Ok(summary);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("grant-gear")]
    public async Task<ActionResult<InventoryItemDto>> GrantGear(
        [FromBody] GrantGearRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var inventoryItem = await adminService.GrantGearAsync(User, request, cancellationToken);
            return Ok(inventoryItem);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("gear-items")]
    public async Task<ActionResult<StoreItemDto>> CreateGearItem(
        [FromBody] UpsertGearItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await adminService.CreateGearItemAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetGearItems), new { includeInactive = true }, item);
    }

    [HttpPut("gear-items/{gearItemId:int}")]
    public async Task<ActionResult<StoreItemDto>> UpdateGearItem(
        int gearItemId,
        [FromBody] UpsertGearItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await adminService.UpdateGearItemAsync(gearItemId, request, cancellationToken);
            return Ok(item);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
