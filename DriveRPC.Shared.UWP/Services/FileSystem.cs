using DriveRPC.Shared.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace DriveRPC.Shared.UWP.Services
{
    public class FileSystem : IFileSystem
    {
        private readonly StorageFolder _root;

        public FileSystem()
        {
            _root = ApplicationData.Current.LocalFolder;
            Debug.WriteLine($"[FileSystem] Initialized. Root = {_root.Path}");
        }

        public string AppDataDirectory => _root.Path;

        public string Combine(params string[] parts)
        {
            var combined = Path.Combine(parts);
            return combined;
        }

        private string GetRelativePath(string fullPath)
        {
            if (fullPath.StartsWith(_root.Path, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(_root.Path.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return fullPath;
        }

        public async Task<bool> FileExistsAsync(string path)
        {
            string relativePath = GetRelativePath(path);

            try
            {
                await StorageFile.GetFileFromPathAsync(path);

                Debug.WriteLine($"[FileSystem] Exists: YES ({relativePath})");
                return true;
            }
            catch (FileNotFoundException)
            {
                Debug.WriteLine($"[FileSystem] Exists: NO ({relativePath})");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileSystem] Exists: ERROR ({relativePath}) → {ex.Message}");
                return false;
            }
        }

        public async Task<string> ReadTextAsync(string path)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                var text = await FileIO.ReadTextAsync(file);
                return text;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileSystem] Read FAILED ({path}) → {ex.Message}");
                throw;
            }
        }

        public async Task WriteTextAsync(string path, string content)
        {
            string relativePath = GetRelativePath(path);
            Debug.WriteLine($"[FileSystem] WriteTextAsync → {relativePath}");

            try
            {
                StorageFolder targetFolder = _root;
                string folderName = Path.GetDirectoryName(relativePath);
                string fileName = Path.GetFileName(relativePath);

                if (!string.IsNullOrEmpty(folderName))
                {
                    targetFolder = await _root.GetFolderAsync(folderName);
                }

                var file = await targetFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, content);

                Debug.WriteLine($"[FileSystem] Write OK ({fileName})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileSystem] Write FAILED ({relativePath}) → {ex}");
                throw;
            }
        }

        public async Task CreateFolderAsync(string folder)
        {
            string relativePath = GetRelativePath(folder);
            Debug.WriteLine($"[FileSystem] CreateFolderAsync → {relativePath}");

            try
            {
                await _root.CreateFolderAsync(relativePath, CreationCollisionOption.OpenIfExists);
                Debug.WriteLine($"[FileSystem] Folder OK ({relativePath})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileSystem] CreateFolder FAILED ({relativePath}) → {ex}");
                throw;
            }
        }

        public async Task<IEnumerable<string>> GetFilesAsync(string folder)
        {
            string relativePath = GetRelativePath(folder);
            Debug.WriteLine($"[FileSystem] GetFilesAsync → {relativePath}");

            try
            {
                var f = await _root.GetFolderAsync(relativePath);
                var items = await f.GetFilesAsync();
                return items.Select(i => i.Path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileSystem] GetFiles FAILED ({relativePath}) → {ex}");
                throw;
            }
        }

        public async Task DeleteFileAsync(string path)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                await file.DeleteAsync();
                Debug.WriteLine($"[FileSystem] Delete OK ({path})");
            }
            catch (FileNotFoundException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileSystem] Delete FAILED ({path}) → {ex.Message}");
            }
        }

        public async Task DeleteFolderAsync(string folder, bool recursive = true)
        {
            string relativePath = GetRelativePath(folder);
            try
            {
                var f = await _root.GetFolderAsync(relativePath);
                await f.DeleteAsync(recursive ? StorageDeleteOption.PermanentDelete : StorageDeleteOption.Default);
            }
            catch (FileNotFoundException) {  }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileSystem] DeleteFolder FAILED ({folder}) → {ex.Message}");
            }
        }
    }
}