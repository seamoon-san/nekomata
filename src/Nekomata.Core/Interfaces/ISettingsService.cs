using System.Threading.Tasks;
using Nekomata.Models;

namespace Nekomata.Core.Interfaces;

public interface ISettingsService
{
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
}
