using System.Security.Claims;
using CogSlop.Api.Models.Dtos;
using CogSlop.Api.Models.Entities;

namespace CogSlop.Api.Services;

public interface ICurrentUserService
{
    Task<UserAccount> EnsureUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<UserAccount> GetExistingUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<UserProfileDto> GetProfileAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<UserProfileDto> UpdateDisplayNameAsync(
        ClaimsPrincipal principal,
        string displayName,
        CancellationToken cancellationToken);

    Task<UserProfileDto> BuildProfileAsync(UserAccount user, CancellationToken cancellationToken);

    Task<int> GetCogBalanceAsync(int userAccountId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetRoleNamesAsync(int userAccountId, CancellationToken cancellationToken);
}
