using System.Security.Claims;
using CogSlop.Api.Controllers;
using CogSlop.Api.Models.Dtos;
using CogSlop.Api.Models.Entities;
using CogSlop.Api.Models.Requests;
using CogSlop.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CogSlop.Api.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task UpdateDisplayName_ReturnsOkProfile_WhenServiceSucceeds()
    {
        var expectedProfile = new UserProfileDto(
            7,
            "Gear Captain",
            "pilot7@cogslop.test",
            null,
            0,
            [],
            [],
            false);

        var userService = new StubCurrentUserService
        {
            UpdateDisplayNameResult = expectedProfile
        };

        var controller = CreateController(userService);
        var result = await controller.UpdateDisplayName(
            new UpdateDisplayNameRequest { DisplayName = "  Gear Captain  " },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<UserProfileDto>(ok.Value);
        Assert.Equal("Gear Captain", payload.DisplayName);
        Assert.Equal("  Gear Captain  ", userService.CapturedDisplayName);
    }

    [Fact]
    public async Task UpdateDisplayName_ReturnsBadRequest_WhenServiceRejectsInput()
    {
        var userService = new StubCurrentUserService
        {
            UpdateDisplayNameException = new InvalidOperationException("Display name required.")
        };

        var controller = CreateController(userService);
        var result = await controller.UpdateDisplayName(
            new UpdateDisplayNameRequest { DisplayName = "   " },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    private static AuthController CreateController(ICurrentUserService currentUserService)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = "http://localhost:4200"
            })
            .Build();

        var controller = new AuthController(currentUserService, config)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreatePrincipal()
                }
            }
        };

        return controller;
    }

    private static ClaimsPrincipal CreatePrincipal()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "sub-7"),
            new Claim(ClaimTypes.Email, "pilot7@cogslop.test")
        ], "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        public UserProfileDto UpdateDisplayNameResult { get; set; } = new(
            0,
            string.Empty,
            string.Empty,
            null,
            0,
            [],
            [],
            false);

        public Exception? UpdateDisplayNameException { get; set; }

        public string? CapturedDisplayName { get; private set; }

        public Task<UserAccount> EnsureUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<UserAccount> GetExistingUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<UserProfileDto> GetProfileAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<UserProfileDto> UpdateDisplayNameAsync(
            ClaimsPrincipal principal,
            string displayName,
            CancellationToken cancellationToken)
        {
            CapturedDisplayName = displayName;

            if (UpdateDisplayNameException is not null)
            {
                throw UpdateDisplayNameException;
            }

            return Task.FromResult(UpdateDisplayNameResult);
        }

        public Task<UserProfileDto> BuildProfileAsync(UserAccount user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetCogBalanceAsync(int userAccountId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<string>> GetRoleNamesAsync(int userAccountId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
