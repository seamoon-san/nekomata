using System.Collections.Generic;
using System.Globalization;

namespace Nekomata.Core.Interfaces;

public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }
    void SetLanguage(string cultureCode);
    string GetString(string key);
}
