using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Services;
using SignalNine.Persistence.Entities.Users;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Data.Authentication;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class AuthenticationEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;

    public AuthenticationEndpointTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
        _previousRootDirectory = Environment.GetEnvironmentVariable(RootDirectoryVariableName);
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _rootDirectory);

        _factory = new WebApplicationFactory<Program>();
    }

    [Fact]
    public async Task Post_Login_ValidCredentials_ReturnsToken()
    {
        var user = SeedUser("test-admin", "test-admin@signalnine.local", "correct-password", UserRoleType.Admin);
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Username = "test-admin",
                Password = "correct-password"
            }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.Equal("Bearer", login.TokenType);
        Assert.Equal(user.Id, login.User.Id);
        Assert.Equal(user.Username, login.User.Username);
        Assert.Equal(user.Email, login.User.Email);
        Assert.Equal(UserRoleType.Admin.ToString(), login.User.Role);

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };
        var principal = handler.ValidateToken(
            login.AccessToken,
            JwtTokenService.CreateTokenValidationParameters(new SignalNineConfig().Jwt),
            out _
        );

        Assert.Equal(user.Id.ToString(), principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.NotNull(GetUser(user.Id)?.LastLoginAt);
    }

    [Fact]
    public async Task Post_Login_InvalidPassword_ReturnsUnauthorized()
    {
        SeedUser("operator", "operator@signalnine.local", "correct-password", UserRoleType.User);
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Username = "operator",
                Password = "wrong-password"
            }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_Login_BlankCredentials_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Username = "",
                Password = ""
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private UserEntity SeedUser(string username, string email, string password, UserRoleType role)
    {
        using var scope = _factory.Services.CreateScope();
        var freeSql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
        freeSql.CodeFirst.SyncStructure<UserEntity>();

        var passwordHasher = new PasswordHasher<UserEntity>();
        var user = new UserEntity
        {
            Username = username,
            Email = email,
            Role = role
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        var dataAccess = scope.ServiceProvider.GetRequiredService<IDataAccess<UserEntity>>();
        dataAccess.Insert(user);

        return user;
    }

    private UserEntity? GetUser(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var dataAccess = scope.ServiceProvider.GetRequiredService<IDataAccess<UserEntity>>();

        return dataAccess.GetByKey(id);
    }

    public void Dispose()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _previousRootDirectory);

        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, true);
        }

        GC.SuppressFinalize(this);
    }
}
