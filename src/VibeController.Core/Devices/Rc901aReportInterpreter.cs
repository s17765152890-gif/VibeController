using VibeController.Core.Domain;

namespace VibeController.Core.Devices;

public sealed record Rc901aReportBinding(
    Guid ServiceUuid,
    Guid CharacteristicUuid,
    string DataHex,
    ControllerControl Control,
    float Value);

public sealed class Rc901aReportInterpreter
{
    private readonly IReadOnlyDictionary<ReportSignature, Rc901aReportBinding> _bindings;

    public Rc901aReportInterpreter(IEnumerable<Rc901aReportBinding> bindings)
    {
        _bindings = bindings.ToDictionary(
            binding => new ReportSignature(
                binding.ServiceUuid,
                binding.CharacteristicUuid,
                NormalizeHex(binding.DataHex)));
    }

    public bool TryInterpret(
        Rc901aGattNotification notification,
        ControllerSnapshot previous,
        out ControllerSnapshot snapshot)
    {
        var signature = new ReportSignature(
            notification.ServiceUuid,
            notification.CharacteristicUuid,
            Rc901aGattProfile.FormatHex(notification.Data));
        if (!_bindings.TryGetValue(signature, out var binding))
        {
            snapshot = previous;
            return false;
        }

        snapshot = previous.With(binding.Control, binding.Value);
        return true;
    }

    private static string NormalizeHex(string value) => string.Join(
        " ",
        value.Split(
                [' ', '-', ':'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.ToUpperInvariant()));

    private sealed record ReportSignature(
        Guid ServiceUuid,
        Guid CharacteristicUuid,
        string DataHex);
}
