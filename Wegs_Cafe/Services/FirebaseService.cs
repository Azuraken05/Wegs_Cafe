using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;

namespace Wegs_Cafe.Services;

/// <summary>
/// Firebase configuration holder for Authentication and Realtime Database,
/// including a secondary FirebaseAuthClient instance so the Owner can register new staff
/// accounts without signing out the active Owner session.
/// </summary>
public class FirebaseService
{
    public FirebaseAuthClient PrimaryAuthenticationClient { get; }
    public FirebaseAuthClient SecondaryStaffRegistrationClient { get; }
    public FirebaseClient RealtimeDatabaseClient { get; }

    public FirebaseService()
    {
        var primaryConfig = new FirebaseAuthConfig
        {
            ApiKey = FirebaseConfiguration.WebApiKey,
            Providers = new FirebaseAuthProvider[] { new EmailProvider() }
        };
        PrimaryAuthenticationClient = new FirebaseAuthClient(primaryConfig);

        var secondaryConfig = new FirebaseAuthConfig
        {
            ApiKey = FirebaseConfiguration.WebApiKey,
            Providers = new FirebaseAuthProvider[] { new EmailProvider() }
        };
        SecondaryStaffRegistrationClient = new FirebaseAuthClient(secondaryConfig);

        RealtimeDatabaseClient = new FirebaseClient(FirebaseConfiguration.RealtimeDatabaseUrl);
    }
}
