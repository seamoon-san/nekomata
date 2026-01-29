using System.Collections.Generic;
using System.Globalization;

namespace Nekomata.Core.Interfaces;

public interface ILocalizationService
{
    event System.EventHandler? LanguageChanged;
    CultureInfo CurrentCulture { get; }
    void SetLanguage(string cultureCode);
    string GetString(string key);
}
