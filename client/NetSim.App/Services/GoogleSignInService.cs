using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;

namespace NetSim.App.Services;

public class GoogleSignInService
{
    public async Task<string> GetIdTokenAsync()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<GoogleSignInService>()
            .Build();

        string? clientId = config["Google:ClientId"];
        string? clientSecret = config["Google:ClientSecret"];
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            throw new InvalidOperationException("Google client settings are missing.");

        var secrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            new[] { "openid", "email", "profile" },
            "user",
            cts.Token,
            new NullDataStore());

        return credential.Token.IdToken;
    }
}
