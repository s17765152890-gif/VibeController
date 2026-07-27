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
            new(
                Rc901aRawInputKind.Keyboard,
                0x26,
                ControllerControl.RemoteUp,
                Rc901aBindingSource.VerifiedDefault),
            new(
                Rc901aRawInputKind.Keyboard,
                0x28,
                ControllerControl.RemoteDown,
                Rc901aBindingSource.VerifiedDefault),
            new(
                Rc901aRawInputKind.Keyboard,
                0x25,
                ControllerControl.RemoteLeft,
                Rc901aBindingSource.VerifiedDefault),
            new(
                Rc901aRawInputKind.Keyboard,
                0x27,
                ControllerControl.RemoteRight,
                Rc901aBindingSource.VerifiedDefault),
            new(
                Rc901aRawInputKind.Keyboard,
                0x0D,
                ControllerControl.RemoteOk,
                Rc901aBindingSource.VerifiedDefault),
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
        IEnumerable<Rc901aInputBinding>? learnedBindings) =>
        Array.AsReadOnly(
            HardwareVerifiedDefaults
                .Concat(NormalizeLearned(learnedBindings))
                .ToArray());

    private static bool IsValidLearned(Rc901aInputBinding? binding) =>
        binding is not null &&
        binding.Source == Rc901aBindingSource.Learned &&
        binding.Code != 0 &&
        Enum.IsDefined(binding.Kind) &&
        Enum.IsDefined(binding.Control) &&
        binding.Control.ToString().StartsWith(
            "Remote",
            StringComparison.Ordinal) &&
        !HardwareVerifiedDefaults.Any(item =>
            item.Control == binding.Control ||
            (item.Kind == binding.Kind && item.Code == binding.Code));

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
