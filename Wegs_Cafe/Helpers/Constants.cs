namespace Wegs_Cafe.Helpers;

public static class Constants
{
    public const string AppName = "Wegs Cafe";
    public const string CurrencySymbol = "₱";

    /// <summary>Top-level node names in the Firebase Realtime Database.</summary>
    public static class Nodes
    {
        public const string Users = "users";
        public const string Categories = "categories";
        public const string Menu = "menu";
        public const string Orders = "orders";
    }
}
