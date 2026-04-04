using System.Security.Claims;
using CogSlop.Api.Data;
using CogSlop.Api.Models.Entities;
using CogSlop.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CogSlop.Api.Tests;

public class RequireDisplayNameFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_Returns428_WhenDisplayNameIsMissing()
    {
        await using var dbContext = CreateDbContext();
        dbContext.UserAccounts.Add(new UserAccount
        {
            UserAccountId = 19,
            GoogleSubject = "sub-19",
            Email = "pilot19@cogslop.test",
            DisplayName = "",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-8),
            LastLoginAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        await dbContext.SaveChangesAsync();

        var filter = new RequireDisplayNameFilter(
            dbContext,
            new StubCurrentUserAccessor(new ExternalUserIdentity(
                "sub-19",
                "pilot19@cogslop.test",
                "External Name",
                null)));

        var context = CreateExecutingContext(CreatePrincipal(19));
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            var executed = new ActionExecutedContext(
                new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor),
                context.Filters,
                context.Controller);
            return Task.FromResult(executed);
        });

        Assert.False(nextCalled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status428PreconditionRequired, result.StatusCode);
        Assert.True(ReadRequiresDisplayName(result.Value));
    }

    [Fact]
    public async Task OnActionExecutionAsync_AllowsRequest_WhenDisplayNameIsConfigured()
    {
        await using var dbContext = CreateDbContext();
        dbContext.UserAccounts.Add(new UserAccount
        {
            UserAccountId = 20,
            GoogleSubject = "sub-20",
            Email = "pilot20@cogslop.test",
            DisplayName = "Gear Captain",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-8),
            LastLoginAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        await dbContext.SaveChangesAsync();

        var filter = new RequireDisplayNameFilter(
            dbContext,
            new StubCurrentUserAccessor(new ExternalUserIdentity(
                "sub-20",
                "pilot20@cogslop.test",
                "External Name",
                null)));

        var context = CreateExecutingContext(CreatePrincipal(20));
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            var executed = new ActionExecutedContext(
                new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor),
                context.Filters,
                context.Controller);
            return Task.FromResult(executed);
        });

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    private static CogSlopDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CogSlopDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new CogSlopDbContext(options);
    }

    private static ActionExecutingContext CreateExecutingContext(ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            controller: new object());
    }

    private static ClaimsPrincipal CreatePrincipal(int userAccountId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, $"sub-{userAccountId}"),
            new Claim(ClaimTypes.Email, $"pilot{userAccountId}@cogslop.test"),
            new Claim(CogClaimTypes.UserAccountId, userAccountId.ToString())
        ], "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    private static bool ReadRequiresDisplayName(object? value)
    {
        var property = value?.GetType().GetProperty("requiresDisplayName");
        return property?.GetValue(value) as bool? ?? false;
    }

    private sealed class StubCurrentUserAccessor(ExternalUserIdentity identity) : ICurrentUserAccessor
    {
        public ExternalUserIdentity GetRequiredIdentity(ClaimsPrincipal principal)
        {
            return identity;
        }
    }
}
