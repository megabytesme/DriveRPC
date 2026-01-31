using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

public class AppearancePresetStore : IAppearancePresetStore
{
    private const string FileName = "appearance_presets.json";

    public async Task<IList<AppearancePreset>> LoadAsync()
    {
        var folder = ApplicationData.Current.LocalFolder;
        var file = await folder.TryGetItemAsync(FileName) as StorageFile;

        if (file == null)
            return new List<AppearancePreset>();

        var json = await FileIO.ReadTextAsync(file);
        return JsonConvert.DeserializeObject<List<AppearancePreset>>(json)
               ?? new List<AppearancePreset>();
    }

    public async Task SaveAsync(IList<AppearancePreset> presets)
    {
        var folder = ApplicationData.Current.LocalFolder;
        var file = await folder.CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting);

        var json = JsonConvert.SerializeObject(presets, Formatting.Indented);
        await FileIO.WriteTextAsync(file, json);
    }
}