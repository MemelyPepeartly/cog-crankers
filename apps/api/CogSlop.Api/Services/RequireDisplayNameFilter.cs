using CogSlop.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace CogSlop.Api.Services;

public class RequireDisplayNameFilter(
    CogSlopDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var requestAborted = context.HttpContext.RequestAborted;
        var principal = context.HttpContext.User;

        string? displayName = null;
        var userAccountIdClaim = principal.FindFirst(CogClaimTypes.UserAccountId)?.Value;
        if (int.TryParse(userAccountIdClaim, out var userAccountId))
        {
            displayName = await dbContext.UserAccounts
                .AsNoTracking()
                .Where(x => x.UserAccountId == userAccountId)
                .Select(x => x.DisplayName)
                .FirstOrDefaultAsync(requestAborted);
        }

        if (displayName is null)
        {
            ExternalUserIdentity identity;
            try
            {
                identity = currentUserAccessor.GetRequiredIdentity(principal);
            }
            catch (InvalidOperationException)
            {
                await next();
                return;
            }

            displayName = await dbContext.UserAccounts
                .AsNoTracking()
                .Where(x => x.GoogleSubject == identity.GoogleSubject || x.Email == identity.Email)
                .Select(x => x.DisplayName)
                .FirstOrDefaultAsync(requestAborted);
        }

        if (DisplayNameRules.IsConfigured(displayName))
        {
            await next();
            return;
        }

        context.Result = new ObjectResult(new
        {
            message = "Set your display name before entering the Cog Slop economy.",
            requiresDisplayName = true
        })
        {
            StatusCode = StatusCodes.Status428PreconditionRequired
        };
    }
}
