using DriveRPC.Shared.Services;
using System.Threading.Tasks;

namespace DriveRPC.Shared.Services
{
    public class FirstRunService
    {
        private readonly IFileSystem _fs;
        private readonly string _path;

        public FirstRunService(IFileSystem fs)
        {
            _fs = fs;
            _path = _fs.Combine(_fs.AppDataDirectory, "first_run.flag");
        }

        public async Task<bool> IsFirstRunAsync()
        {
            return !await _fs.FileExistsAsync(_path);
        }

        public async Task MarkAsCompletedAsync()
        {
            await _fs.WriteTextAsync(_path, "done");
        }
    }
}