using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;

namespace Wegs_Cafe.Services
{
    public class FirebaseService
    {
        // Your Firebase configuration keys
        private const string WebApiKey = "AIzaSyDdrOa9CFySL0U9MWgUmHDc1sGunC__6Zc";
        private const string DatabaseUrl = "https://wegscafe-521d1-default-rtdb.firebaseio.com/";

        public FirebaseAuthClient Auth { get; private set; }
        public FirebaseClient Db { get; private set; }

        public FirebaseService()
        {
            // Initialize Firebase Authentication for Owner, Staff, and Users
            var config = new FirebaseAuthConfig
            {
                ApiKey = WebApiKey,
                Providers = new FirebaseAuthProvider[]
                {
                    new EmailProvider()
                }
            };
            Auth = new FirebaseAuthClient(config);

            // Initialize Firebase Realtime Database for Menu, Orders, and Inventory
            Db = new FirebaseClient(DatabaseUrl);
        }
    }
}