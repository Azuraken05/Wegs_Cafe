using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Storage;

namespace Wegs_Cafe.Services;

/// <summary>
/// Firebase configuration holder for Authentication, Realtime Database, and Storage,
/// including a secondary FirebaseAuthClient instance so the Owner can register new Manager/Staff
/// accounts without signing out the active Owner session.
/// </summary>
public class FirebaseService
{
    private const string WebApiKey = "AIzaSyDdrOa9CFySL0U9MWgUmHDc1sGunC__6Zc";
    private const string RealtimeDatabaseUrl = "https://wegscafe-521d1-default-rtdb.firebaseio.com/";
    private const string StorageBucket = "wegscafe-521d1.appspot.com";

    public FirebaseAuthClient PrimaryAuthenticationClient { get; }
    public FirebaseAuthClient SecondaryStaffRegistrationClient { get; }
    public FirebaseClient RealtimeDatabaseClient { get; }
    public FirebaseStorage StorageClient { get; }

    public FirebaseService()
    {
        var primaryConfig = new FirebaseAuthConfig
        {
            ApiKey = WebApiKey,
            Providers = new FirebaseAuthProvider[] { new EmailProvider() }
        };
        PrimaryAuthenticationClient = new FirebaseAuthClient(primaryConfig);

        // Secondary isolated client used by Owner when creating Manager/Staff accounts
        var secondaryConfig = new FirebaseAuthConfig
        {
            ApiKey = WebApiKey,
            Providers = new FirebaseAuthProvider[] { new EmailProvider() }
        };
        SecondaryStaffRegistrationClient = new FirebaseAuthClient(secondaryConfig);

        RealtimeDatabaseClient = new FirebaseClient(RealtimeDatabaseUrl);
        StorageClient = new FirebaseStorage(StorageBucket);
    }
}
