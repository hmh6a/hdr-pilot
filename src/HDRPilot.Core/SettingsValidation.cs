namespace HDRPilot.Core;

public static class SettingsValidation
{
    public static void Validate(AppSettings value)
    {
        if (value.SchemaVersion != 1) throw new InvalidDataException("Unsupported configuration version. Use a compatible version of HDR Pilot.");
        if (!Enum.IsDefined(value.Mode) || value.CooldownMs is < 1000 or > 120000) throw new InvalidDataException("Invalid automation mode or cooldown (1–120 seconds).");
        if (value.Profiles is null || value.Profiles.Count > 2000 || value.Hotkeys is null) throw new InvalidDataException("Invalid profiles or hotkeys.");
        var ids = new HashSet<Guid>();
        foreach (var p in value.Profiles)
        {
            if (p is null || p.Id == Guid.Empty || !ids.Add(p.Id) || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 150 || !Enum.IsDefined(p.Mode))
                throw new InvalidDataException("A profile has an invalid name, ID, or mode.");
            if (string.IsNullOrWhiteSpace(p.ExecutablePath) || p.ExecutablePath.Length > 1024 || !Path.IsPathFullyQualified(p.ExecutablePath) || !p.ExecutablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Profiles must contain an absolute .exe path.");
            if (p.PreferredDisplayId?.Length > 512 || p.Source is null || p.Source.Length > 100 || p.Priority is < -1000 or > 1000 || p.ActivationDelayMs is < 0 or > 120000 || p.DeactivationDelayMs is < 0 or > 120000)
                throw new InvalidDataException("A profile has invalid timing, priority, or display settings.");
        }
        if (value.Hotkeys.Count > 4 || value.Hotkeys.Any(p => p.Key is not ("Toggle" or "Automatic" or "Pause" or "Cycle") || p.Value is null || p.Value.Key is < 1 or > 254 || p.Value.Modifiers > 15))
            throw new InvalidDataException("Invalid global shortcuts.");
    }
}
