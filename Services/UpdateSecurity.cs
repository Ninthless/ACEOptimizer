namespace ACEOptimizer.Services
{
    internal static class UpdateSecurity
    {
        public const string AppCastUrl = "https://github.com/Ninthless/ACEOptimizer/releases/latest/download/appcast.xml";
        public const string ReleasePageUrl = "https://github.com/Ninthless/ACEOptimizer/releases/latest";

        public static readonly string[] TrustedPublicKeys =
        [
            "J4bP9IT3dIqnPnxI1Q+bx+dFP5z/J6IbirO8uiCgr1U=",
            "bLhABDaXnZN7t7TBkfn8/550/0aq3TuRmHG4BzMr634="
        ];
    }
}
