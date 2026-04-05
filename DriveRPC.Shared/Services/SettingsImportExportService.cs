using DriveRPC.Shared.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace DriveRPC.Shared.Services
{
    public class SettingsImportExportService
    {
        public string ExportToText(string appearanceTag, IEnumerable<AppearancePreset> presets)
        {
            try
            {
                var payload = new SettingsTransferPackage
                {
                    AppearanceTag = appearanceTag,
                    Vehicles = presets?.Select(CloneForTransfer).ToList() ?? new List<AppearancePreset>()
                };

                var json = JsonConvert.SerializeObject(payload);
                return Compress(json);
            }
            catch
            {
                return string.Empty;
            }
        }

        public SettingsTransferPackage ImportFromText(string exportedText)
        {
            if (string.IsNullOrWhiteSpace(exportedText))
                return null;

            try
            {
                var json = Decompress(exportedText);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                var payload = JsonConvert.DeserializeObject<SettingsTransferPackage>(json);
                if (payload == null)
                    return null;

                payload.Vehicles = (payload.Vehicles ?? new List<AppearancePreset>())
                    .Where(x => x != null)
                    .Select(CloneForTransfer)
                    .ToList();

                return payload;
            }
            catch
            {
                return null;
            }
        }

        private static AppearancePreset CloneForTransfer(AppearancePreset preset)
        {
            if (preset == null)
                return null;

            var clone = preset.Clone();
            clone.CachedLargeImageKey = null;
            clone.CachedSmallImageKey = null;
            return clone;
        }

        private static string Compress(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            var buffer = Encoding.UTF8.GetBytes(json);

            using (var memoryStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                {
                    gzipStream.Write(buffer, 0, buffer.Length);
                }

                return Convert.ToBase64String(memoryStream.ToArray());
            }
        }

        private static string Decompress(string compressedText)
        {
            var buffer = Convert.FromBase64String(compressedText);

            using (var memoryStream = new MemoryStream(buffer))
            using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzipStream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
