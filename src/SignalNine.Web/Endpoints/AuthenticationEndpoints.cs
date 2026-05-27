using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using SignalNine.Core.Data.Authentication;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Users;
using SignalNine.Persistence.Interfaces;
using SignalNine.Web.Data.Authentication;

namespace SignalNine.Web.Endpoints;

/// <summary>
/// Maps authentication-related HTTP endpoints.
/// </summary>
public static class AuthenticationEndpoints
{
    /// <summary>
    /// Maps authentication endpoints under <c>/api/auth</c>.
    /// </summary>
    public static WebApplication MapAuthenticationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").AllowAnonymous();

        group.MapPost(
            "/login",
            Login
        );

        return app;
    }

    private static Results<Ok<LoginResponse>, BadRequest<string>, UnauthorizedHttpResult> Login(
        LoginRequest request,
        IDataAccess<UserEntity> dataAccess,
        IJwtTokenService jwtTokenService,
        IPasswordHasher<UserEntity> passwordHasher
    )
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.BadRequest("Username and password are required.");
        }

        var username = request.Username.Trim();
        var user = FindUser(dataAccess, username);

        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return TypedResults.Unauthorized();
        }

        var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return TypedResults.Unauthorized();
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        var now = DateTime.UtcNow;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        dataAccess.Update(user);

        var token = jwtTokenService.CreateToken(
            new JwtTokenUser
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role.ToString()
            }
        );

        return TypedResults.Ok(
            new LoginResponse
            {
                AccessToken = token.AccessToken,
                ExpiresAt = new DateTimeOffset(token.ExpiresAt),
                User = new AuthenticatedUserResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    Role = user.Role.ToString()
                }
            }
        );
    }

    private static UserEntity? FindUser(IDataAccess<UserEntity> dataAccess, string username)
        => dataAccess.List()
                     .FirstOrDefault(
                         user =>
                             string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(user.Email, username, StringComparison.OrdinalIgnoreCase)
                     );
}
