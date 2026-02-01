using DriveRPC.Shared.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DriveRPC.Shared.Services
{
    public interface IAppearancePresetStore
    {
        Task<IList<AppearancePreset>> LoadAsync();
        Task SaveAsync(IList<AppearancePreset> presets);
    }

    public class AppearancePresetStore : IAppearancePresetStore
    {
        private readonly IFileSystem _fs;
        private readonly string _path;

        public AppearancePresetStore(IFileSystem fs)
        {
            _fs = fs;
            _path = _fs.Combine(_fs.AppDataDirectory, "presets.json");
        }

        public async Task<IList<AppearancePreset>> LoadAsync()
        {
            if (!await _fs.FileExistsAsync(_path))
                return new List<AppearancePreset>();

            var json = await _fs.ReadTextAsync(_path);
            return JsonConvert.DeserializeObject<List<AppearancePreset>>(json)
                   ?? new List<AppearancePreset>();
        }

        public async Task SaveAsync(IList<AppearancePreset> presets)
        {
            var json = JsonConvert.SerializeObject(presets, Formatting.Indented);
            await _fs.WriteTextAsync(_path, json);
        }
    }
}