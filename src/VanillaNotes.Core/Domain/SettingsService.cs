using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Models;
using VanillaNotes.Core.Validation;

namespace VanillaNotes.Core.Domain;

/// <summary>The settings of the interface the open profile keeps.</summary>
public sealed class SettingsService
{
    private readonly ProfileSettingsRepository _settings;

    public SettingsService(ProfileSettingsRepository settings)
    {
        _settings = settings;
    }

    /// <summary>The settings of the profile, with the default of every one it never changed.</summary>
    public ProfileSettings Get() => _settings.Find() ?? ProfileSettings.Default;

    /// <summary>Stores the settings of the profile, when every value is one its setting allows.</summary>
    public ProfileSettings Update(ProfileSettings settings)
    {
        ProfileSettings accepted = SettingsValidator.Settings(settings);
        _settings.Save(accepted);
        return accepted;
    }
}
