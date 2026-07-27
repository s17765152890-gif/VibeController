using System.Collections.ObjectModel;
using VibeController.Core.Domain;

namespace VibeController.Core.Devices;

public enum Rc901aRawInputKind
{
    Keyboard,
    ConsumerControl,
    DriverHidUsage,
}

public sealed record Rc901aRawInputEvent(
    DateTimeOffset Timestamp,
    Rc901aRawInputKind Kind,
    ushort Code,
    bool IsPressed);

public enum Rc901aBindingSource
{
    VerifiedDefault,
    Learned,
}

public sealed record Rc901aInputBinding(
    Rc901aRawInputKind Kind,
    ushort Code,
    ControllerControl Control,
    Rc901aBindingSource Source);

public static class Rc901aInputBindings
{
    private static readonly ReadOnlyCollection<Rc901aInputBinding>
        HardwareVerifiedDefaults = Array.AsReadOnly<Rc901aInputBinding>(
        [
            Verified(Rc901aRawInputKind.DriverHidUsage, 0x52, ControllerControl.RemoteUp),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0x51, ControllerControl.RemoteDown),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0x50, ControllerControl.RemoteLeft),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0x4F, ControllerControl.RemoteRight),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0x28, ControllerControl.RemoteOk),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0x65, ControllerControl.RemoteMenu),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0xF1, ControllerControl.RemoteBack),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0x83, ControllerControl.RemoteHome),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0xED, ControllerControl.RemoteVolumeUp),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0xEE, ControllerControl.RemoteVolumeDown),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0xAD, ControllerControl.RemoteMic),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0xEF, ControllerControl.RemoteMute),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0x97, ControllerControl.RemoteInput),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0x99, ControllerControl.RemoteRed),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0x9A, ControllerControl.RemoteGreen),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0x9B, ControllerControl.RemoteBlue),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0xA8, ControllerControl.RemoteSettings),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0xD1, ControllerControl.RemoteApp1),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0xDE, ControllerControl.RemoteApp2),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0x9E, ControllerControl.RemoteBrightnessUp),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0x9F, ControllerControl.RemoteBrightnessDown),
            Verified(Rc901aRawInputKind.DriverHidUsage, 0xAA, ControllerControl.RemotePictureMode),
            Verified(Rc901aRawInputKind.Keyboard, 0x26, ControllerControl.RemoteUp),
            Verified(Rc901aRawInputKind.Keyboard, 0x28, ControllerControl.RemoteDown),
            Verified(Rc901aRawInputKind.Keyboard, 0x25, ControllerControl.RemoteLeft),
            Verified(Rc901aRawInputKind.Keyboard, 0x27, ControllerControl.RemoteRight),
            Verified(Rc901aRawInputKind.Keyboard, 0x0D, ControllerControl.RemoteOk),
            Verified(Rc901aRawInputKind.Keyboard, 0x5D, ControllerControl.RemoteMenu),
        ]);

    public static IReadOnlyList<Rc901aInputBinding> VerifiedDefaults =>
        HardwareVerifiedDefaults;

    public static IReadOnlyList<Rc901aInputBinding> NormalizeLearned(
        IEnumerable<Rc901aInputBinding>? bindings)
    {
        var normalized = new List<Rc901aInputBinding>();
        foreach (var binding in bindings ?? [])
        {
            if (!IsValidLearned(binding))
            {
                continue;
            }

            normalized = UpsertUnchecked(normalized, binding);
        }

        return Array.AsReadOnly(normalized.ToArray());
    }

    public static IReadOnlyList<Rc901aInputBinding> Upsert(
        IEnumerable<Rc901aInputBinding>? current,
        Rc901aInputBinding replacement)
    {
        var normalized = NormalizeLearned(current);
        if (!IsValidLearned(replacement))
        {
            return normalized;
        }

        return Array.AsReadOnly(
            UpsertUnchecked(normalized, replacement).ToArray());
    }

    public static IReadOnlyList<Rc901aInputBinding> CombineWithVerifiedDefaults(
        IEnumerable<Rc901aInputBinding>? learnedBindings)
    {
        var learned = NormalizeLearned(learnedBindings);
        return Array.AsReadOnly(
            HardwareVerifiedDefaults
                .Where(verified => learned.All(item =>
                    item.Control != verified.Control &&
                    (item.Kind != verified.Kind ||
                     item.Code != verified.Code)))
                .Concat(learned)
                .ToArray());
    }

    private static bool IsValidLearned(Rc901aInputBinding? binding) =>
        binding is not null &&
        binding.Source == Rc901aBindingSource.Learned &&
        binding.Code != 0 &&
        Enum.IsDefined(binding.Kind) &&
        Enum.IsDefined(binding.Control) &&
        binding.Control.ToString().StartsWith(
            "Remote",
            StringComparison.Ordinal);

    private static Rc901aInputBinding Verified(
        Rc901aRawInputKind kind,
        ushort code,
        ControllerControl control) =>
        new(
            kind,
            code,
            control,
            Rc901aBindingSource.VerifiedDefault);

    private static List<Rc901aInputBinding> UpsertUnchecked(
        IEnumerable<Rc901aInputBinding> current,
        Rc901aInputBinding replacement) =>
        current
            .Where(item =>
                item.Control != replacement.Control &&
                (item.Kind != replacement.Kind || item.Code != replacement.Code))
            .Append(replacement)
            .ToList();
}
