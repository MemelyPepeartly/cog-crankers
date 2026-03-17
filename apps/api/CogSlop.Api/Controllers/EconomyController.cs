using CogSlop.Api.Models.Dtos;
using CogSlop.Api.Models.Requests;
using CogSlop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CogSlop.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class EconomyController(IEconomyService economyService) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var dashboard = await economyService.GetDashboardAsync(User, cancellationToken);
        return Ok(dashboard);
    }

    [HttpGet("store")]
    public async Task<ActionResult<IReadOnlyList<StoreItemDto>>> GetStore(CancellationToken cancellationToken)
    {
        var storeItems = await economyService.GetStoreItemsAsync(includeInactive: false, cancellationToken);
        return Ok(storeItems);
    }

    [HttpPost("store/{gearItemId:int}/buy")]
    public async Task<ActionResult<PurchaseReceiptDto>> BuyGear(
        int gearItemId,
        [FromBody] BuyGearRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var quantity = request?.Quantity ?? 1;
            var receipt = await economyService.BuyGearAsync(User, gearItemId, quantity, cancellationToken);
            return Ok(receipt);
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

    [HttpPost("craft")]
    public async Task<ActionResult<CraftGearReceiptDto>> CraftGear(
        [FromBody] CraftGearRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var receipt = await economyService.CraftGearAsync(User, request, cancellationToken);
            return Ok(receipt);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("marketplace/listings")]
    public async Task<ActionResult<IReadOnlyList<MarketplaceListingDto>>> GetMarketplaceListings(CancellationToken cancellationToken)
    {
        var listings = await economyService.GetMarketplaceListingsAsync(cancellationToken);
        return Ok(listings);
    }

    [HttpPost("marketplace/listings")]
    public async Task<ActionResult<MarketplaceListingDto>> CreateMarketplaceListing(
        [FromBody] CreateMarketplaceListingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var listing = await economyService.CreateMarketplaceListingAsync(User, request, cancellationToken);
            return Ok(listing);
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

    [HttpPost("marketplace/listings/{marketplaceListingId:int}/buy")]
    public async Task<ActionResult<MarketplacePurchaseReceiptDto>> BuyMarketplaceListing(
        int marketplaceListingId,
        CancellationToken cancellationToken)
    {
        try
        {
            var receipt = await economyService.BuyMarketplaceListingAsync(User, marketplaceListingId, cancellationToken);
            return Ok(receipt);
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

    [HttpPost("cog-sessions/in")]
    public async Task<ActionResult<CogSessionDto>> CogIn([FromBody] CogInRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await economyService.CogInAsync(User, request ?? new CogInRequest(), cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("cog-sessions/out")]
    public async Task<ActionResult<CogSessionDto>> CogOut([FromBody] CogOutRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await economyService.CogOutAsync(User, request ?? new CogOutRequest(), cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("cog-sessions/history")]
    public async Task<ActionResult<IReadOnlyList<CogSessionDto>>> GetCogSessionHistory(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var boundedTake = Math.Clamp(take, 1, 200);
        var history = await economyService.GetCogSessionHistoryAsync(User, boundedTake, cancellationToken);
        return Ok(history);
    }
}
