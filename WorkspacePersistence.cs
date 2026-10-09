using System.IO;
using System.Text.Json;

namespace Cloudless;

internal static class WorkspacePersistence
{
    private static readonly JsonSerializerOptions SaveOptions = new()
    {
        WriteIndented = true
    };

    internal static void Save(string filePath, CloudlessWorkspace workspace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(workspace);

        string fullPath = Path.GetFullPath(filePath);
        string directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            string json = JsonSerializer.Serialize(workspace, SaveOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    internal static CloudlessWorkspace? Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var workspace = JsonSerializer.Deserialize<CloudlessWorkspace>(File.ReadAllText(filePath));
        if (workspace == null)
            return null;

        workspace.CloudlessWindows ??= new List<CloudlessWindowState>();
        if (workspace.CloudlessWindows.Any(window => window == null))
            throw new JsonException("Workspace contains a null window state.");

        return workspace;
    }
}
