using System.Security.Claims;
using CogSlop.Api.Data;
using CogSlop.Api.Models.Entities;
using CogSlop.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CogSlop.Api.Tests;

public class CurrentUserServiceTests
{
    [Fact]
    public async Task GetProfileAsync_DoesNotTouchLastLoginAtUtc()
    {
        await using var dbContext = CreateDbContext();
        var createdAt = DateTime.UtcNow.AddDays(-10);
        var lastLoginAt = DateTime.UtcNow.AddDays(-1);

        dbContext.UserAccounts.Add(new UserAccount
        {
            UserAccountId = 41,
            GoogleSubject = "sub-41",
            Email = "pilot41@cogslop.test",
            DisplayName = "Pilot Forty One",
            AvatarUrl = "https://avatar.test/41.png",
            CreatedAtUtc = createdAt,
            LastLoginAtUtc = lastLoginAt
        });

        await dbContext.SaveChangesAsync();

        var accessor = new StubCurrentUserAccessor(new ExternalUserIdentity(
            "sub-41",
            "pilot41@cogslop.test",
            "External Name",
            "https://avatar.test/41.png"));
        var service = new CurrentUserService(dbContext, accessor);

        var principal = CreatePrincipal(41);
        var profile = await service.GetProfileAsync(principal, CancellationToken.None);
        var reloaded = await dbContext.UserAccounts.AsNoTracking().SingleAsync(x => x.UserAccountId == 41);

        Assert.Equal("Pilot Forty One", profile.DisplayName);
        Assert.Equal(lastLoginAt, reloaded.LastLoginAtUtc);
    }

    [Fact]
    public async Task UpdateDisplayNameAsync_TrimsAndPersistsDisplayName()
    {
        await using var dbContext = CreateDbContext();

        dbContext.UserAccounts.Add(new UserAccount
        {
            UserAccountId = 52,
            GoogleSubject = "sub-52",
            Email = "pilot52@cogslop.test",
            DisplayName = "",
            AvatarUrl = "https://avatar.test/52.png",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
            LastLoginAtUtc = DateTime.UtcNow.AddDays(-1)
        });

        await dbContext.SaveChangesAsync();

        var accessor = new StubCurrentUserAccessor(new ExternalUserIdentity(
            "sub-52",
            "pilot52@cogslop.test",
            "External Name",
            "https://avatar.test/52.png"));
        var service = new CurrentUserService(dbContext, accessor);

        var principal = CreatePrincipal(52);
        var profile = await service.UpdateDisplayNameAsync(principal, "  Gear Marshal  ", CancellationToken.None);
        var reloaded = await dbContext.UserAccounts.AsNoTracking().SingleAsync(x => x.UserAccountId == 52);

        Assert.Equal("Gear Marshal", profile.DisplayName);
        Assert.Equal("Gear Marshal", reloaded.DisplayName);
    }

    private static CogSlopDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CogSlopDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new CogSlopDbContext(options);
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

    private sealed class StubCurrentUserAccessor(ExternalUserIdentity identity) : ICurrentUserAccessor
    {
        public ExternalUserIdentity GetRequiredIdentity(ClaimsPrincipal principal)
        {
            return identity;
        }
    }
}
