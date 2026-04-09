using Android.Content;
using DriveRPC.Shared.Services;
using System.Text;

namespace DriveRPC.Android.Services;

internal sealed class AndroidFileSystem : IFileSystem
{
    private readonly string _rootPath;

    public AndroidFileSystem(Context context)
    {
        _rootPath = context.FilesDir?.AbsolutePath
            ?? throw new InvalidOperationException("App files directory is unavailable.");
    }

    public string AppDataDirectory => _rootPath;

    public string Combine(params string[] parts)
    {
        var normalized = parts
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .Select(static part => part.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar))
            .ToArray();

        return normalized.Length == 0 ? _rootPath : Path.Combine(normalized);
    }

    public Task<bool> FileExistsAsync(string path)
        => Task.FromResult(File.Exists(NormalizePath(path)));

    public async Task<string> ReadTextAsync(string path)
    {
        var fullPath = NormalizePath(path);
        using var stream = File.OpenRead(fullPath);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    public async Task WriteTextAsync(string path, string content)
    {
        var fullPath = NormalizePath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content ?? string.Empty, Encoding.UTF8);
    }

    public Task DeleteFileAsync(string path)
    {
        var fullPath = NormalizePath(path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetFilesAsync(string folder)
    {
        var fullPath = NormalizePath(folder);
        return Task.FromResult<IEnumerable<string>>(Directory.Exists(fullPath)
            ? Directory.GetFiles(fullPath)
            : Array.Empty<string>());
    }

    public Task<IEnumerable<string>> GetFoldersAsync(string folder)
    {
        var fullPath = NormalizePath(folder);
        return Task.FromResult<IEnumerable<string>>(Directory.Exists(fullPath)
            ? Directory.GetDirectories(fullPath)
            : Array.Empty<string>());
    }

    public Task CreateFolderAsync(string folder)
    {
        Directory.CreateDirectory(NormalizePath(folder));
        return Task.CompletedTask;
    }

    public Task DeleteFolderAsync(string folder, bool recursive = true)
    {
        var fullPath = NormalizePath(folder);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive);
        }

        return Task.CompletedTask;
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return _rootPath;
        }

        var normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized))
        {
            return normalized;
        }

        return Path.Combine(_rootPath, normalized);
    }
}
