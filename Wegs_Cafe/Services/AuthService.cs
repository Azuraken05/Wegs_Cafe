using Firebase.Auth;
using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>Email/password authentication + user profile. No roles yet.</summary>
public class AuthService
{
    private readonly FirebaseService _firebase;
    private readonly FirebaseRepository<AppUser> _users;

    public AuthService(FirebaseService firebase)
    {
        _firebase = firebase;
        _users = new FirebaseRepository<AppUser>(firebase, Constants.Nodes.Users);
    }

    public User? CurrentUser => _firebase.Auth.User;
    public bool IsLoggedIn => CurrentUser != null;

    public async Task<User> LoginAsync(string email, string password)
    {
        var credential = await _firebase.Auth.SignInWithEmailAndPasswordAsync(email, password);
        return credential.User;
    }

    public async Task<User> RegisterAsync(string email, string password, string displayName)
    {
        var credential = await _firebase.Auth.CreateUserWithEmailAndPasswordAsync(email, password, displayName);

        // Save a profile under /users/{uid}
        await _users.AddAsync(new AppUser
        {
            Id = credential.User.Uid,
            DisplayName = displayName,
            Email = email
        });

        return credential.User;
    }

    public Task SendPasswordResetAsync(string email) => _firebase.Auth.ResetEmailPasswordAsync(email);

    public void Logout() => _firebase.Auth.SignOut();

    public Task<AppUser?> GetProfileAsync()
        => CurrentUser == null ? Task.FromResult<AppUser?>(null) : _users.GetByIdAsync(CurrentUser.Uid);

    public Task UpdateProfileAsync(AppUser profile) => _users.UpdateAsync(profile);

    /// <summary>Turns Firebase error codes into friendly messages for the UI.</summary>
    public static string GetFriendlyError(Exception ex) => ex switch
    {
        FirebaseAuthException fae => fae.Reason switch
        {
            AuthErrorReason.WrongPassword or AuthErrorReason.UnknownEmailAddress
                or AuthErrorReason.InvalidEmailAddress => "Invalid email or password.",
            AuthErrorReason.EmailExists => "This email is already registered.",
            AuthErrorReason.WeakPassword => "Password must be at least 6 characters.",
            AuthErrorReason.TooManyAttemptsTryLater => "Too many attempts. Please try again later.",
            _ => "Authentication failed. Please try again."
        },
        HttpRequestException => "No internet connection.",
        _ => ex.Message
    };
}
