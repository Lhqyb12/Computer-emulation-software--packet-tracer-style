using System;                              // TimeSpan, InvalidOperationException
using System.Threading;                    // CancellationTokenSource
using System.Threading.Tasks;              // Task, async/await
using Google.Apis.Auth.OAuth2;             // GoogleWebAuthorizationBroker, ClientSecrets, UserCredential
using Google.Apis.Util.Store;              // NullDataStore
using Microsoft.Extensions.Configuration;  // ConfigurationBuilder - reads settings

namespace NetSim.App.Services;

// The client's half of "Sign in with Google". It opens Google's sign-in page in the browser and
// returns the ID token Google issues - a signed text that states who the user is.
// The token is then sent to our server (AuthApiClient.GoogleLoginAsync), which checks Google's signature
public class GoogleSignInService
{
    // Purpose: Opens Google's sign-in page in the browser and gets the ID token Google signs for the user.
    // Input:   nothing (the Google client id and secret are read from the user secrets).
    // Output:  the ID token as text. Throws if the settings are missing or the user does not finish within 2 minutes.
    // Runs the Google sign-in and returns the ID token. Throws if the settings are missing,
    // or if the user does not finish signing in
    public async Task<string> GetIdTokenAsync()
    {
        // Read this project's "user secrets": settings kept in a file outside the project folder,
        // so they are never committed to git
        var config = new ConfigurationBuilder()
            .AddUserSecrets<GoogleSignInService>()
            .Build();

        // The id and the secret that Google gave our application when it was registered with Google
        string? clientId = config["Google:ClientId"];
        string? clientSecret = config["Google:ClientSecret"];

        // Without both of them Google will not let us start a sign-in
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            throw new InvalidOperationException("Google client settings are missing.");

        // Put the two values in the object Google's library expects
        var secrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };

        // A cancel signal that is raised by itself after 2 minutes, so we do not wait forever
        // for a user who closed the browser. "using var" = it is cleaned up when the method ends
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        // Opens the browser on Google's sign-in page and waits until the user finishes there
        UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,                                  // who is asking (our application)
            new[] { "openid", "email", "profile" },   // what we ask for: the user's identity, email and name
            "user",                                   // a name for this user inside the library
            cts.Token,                                // the 2-minute cancel signal
            new NullDataStore());                     // save nothing on disk: the user signs in again every time

        // The ID token that Google signed
        return credential.Token.IdToken;
    }
}
