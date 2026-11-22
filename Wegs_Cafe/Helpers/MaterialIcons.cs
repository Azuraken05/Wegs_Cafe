namespace Wegs_Cafe.Helpers;

/// <summary>
/// Google Material Symbols icon mapping.
/// Provides both SVG asset filenames (from Resources/Images/Icons/) and Material Symbols Rounded codepoints.
/// Strictly zero Unicode emojis are used anywhere in the application.
/// </summary>
public static class MaterialIcons
{
    public const string FontFamily = "MaterialSymbolsRounded";

    // Local SVG vector asset filenames (bundled via MauiImage in Resources/Images/Icons/)
    public static class Assets
    {
        public const string Menu = "menu.png";
        public const string Dashboard = "dashboard.png";
        public const string PointOfSale = "point_of_sale.png";
        public const string Inventory = "inventory_2.png";
        public const string AddBox = "add_box.png";
        public const string Group = "group.png";
        public const string AuditTrail = "history_edu.png";
        public const string ReceiptLong = "receipt_long.png";
        public const string Print = "print.png";
        public const string FileDownload = "file_download.png";
        public const string Warning = "warning.png";
        public const string Search = "search.png";
        public const string Payments = "payments.png";
        public const string QrCode = "qr_code_2.png";
        public const string CreditCard = "credit_card.png";
        public const string Logout = "logout.png";
    }

    // Material Symbols Rounded Unicode codepoints (when MaterialSymbolsRounded.ttf is loaded)
    public static class Glyphs
    {
        public const string Menu = "\ue5d2";
        public const string Dashboard = "\ue871";
        public const string PointOfSale = "\uf17e";
        public const string Inventory = "\ue179";
        public const string AddBox = "\ue146";
        public const string Group = "\ue7ef";
        public const string AuditTrail = "\uea3e";
        public const string ReceiptLong = "\uef6e";
        public const string Print = "\ue8ad";
        public const string FileDownload = "\ue2c4";
        public const string Warning = "\ue002";
        public const string Search = "\ue8b6";
        public const string Payments = "\uef63";
        public const string QrCode = "\uf00a";
        public const string CreditCard = "\ue870";
        public const string Logout = "\ue9ba";
    }
}
