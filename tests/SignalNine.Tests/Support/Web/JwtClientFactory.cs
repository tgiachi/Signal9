using System.Net.Http.Headers;

using SignalNine.Core.Data.Authentication;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Services;

namespace SignalNine.Tests.Support.Web;

public static class JwtClientFactory
{
    public static HttpClient CreateAuthorizedClient(HttpClient client)
    {
        var token = CreateAccessToken();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    public static string CreateAccessToken()
        => new JwtTokenService(new SignalNineConfig())
           .CreateToken(
               new JwtTokenUser
               {
                   UserId = Guid.NewGuid(),
                   Username = "tests",
                   Email = "tests@signalnine.local",
                   Role = "Admin"
               }
           )
           .AccessToken;
}
