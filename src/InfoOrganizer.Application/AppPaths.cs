namespace InfoOrganizer.Application;

public static class AppPaths
{
    public const string AppDirectoryName = "InfoOrganizer";

    /// <summary>Returns the per-user app-data directory and creates it if missing.</summary>
    public static string GetAppDataDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = AppContext.BaseDirectory;

        var path = Path.Combine(root, AppDirectoryName);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Returns the default SQLite database path under the per-user app-data directory.</summary>
    public static string GetDefaultDatabasePath() => Path.Combine(GetAppDataDirectory(), "infoorganizer.db");
}
