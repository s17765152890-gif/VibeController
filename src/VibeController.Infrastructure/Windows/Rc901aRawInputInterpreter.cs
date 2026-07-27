using VibeController.Core.Devices;
using VibeController.Core.Domain;

namespace VibeController.Infrastructure.Windows;

public static class Rc901aRawInputDeviceIdentity
{
    public const ushort VendorId = 0x0416;

    public const ushort ProductId = 0x0301;

    public const ushort VersionNumber = 0x0003;

    public static bool IsSupported(
        ushort vendorId,
        ushort productId,
        ushort versionNumber,
        ushort usagePage,
        ushort usage) =>
        vendorId == VendorId &&
        productId == ProductId &&
        versionNumber == VersionNumber &&
        (usagePage, usage) is
            (0x0001, 0x0006) or
            (0x000C, 0x0001);
}

public sealed class Rc901aRawInputDecoder
{
    private const ushort KeyBreak = 0x0001;
    private readonly HashSet<ushort> _pressedKeyboardCodes = [];
    private ushort _consumerUsage;

    public bool TryDecodeKeyboard(
        DateTimeOffset timestamp,
        ushort virtualKey,
        ushort flags,
        out Rc901aRawInputEvent input)
    {
        var isPressed = (flags & KeyBreak) == 0;
        var changed = isPressed
            ? _pressedKeyboardCodes.Add(virtualKey)
            : _pressedKeyboardCodes.Remove(virtualKey);
        if (!changed)
        {
            input = default!;
            return false;
        }

        input = new Rc901aRawInputEvent(
            timestamp,
            Rc901aRawInputKind.Keyboard,
            virtualKey,
            isPressed);
        return true;
    }

    public IReadOnlyList<Rc901aRawInputEvent> DecodeConsumerReport(
        DateTimeOffset timestamp,
        ReadOnlySpan<byte> report)
    {
        ushort usage;
        if (report.Length == 2)
        {
            usage = (ushort)(report[0] | (report[1] << 8));
        }
        else if (report.Length >= 3 && report[0] == 0x03)
        {
            usage = (ushort)(report[1] | (report[2] << 8));
        }
        else
        {
            return [];
        }

        if (usage == _consumerUsage)
        {
            return [];
        }

        var events = new List<Rc901aRawInputEvent>(2);
        if (_consumerUsage != 0)
        {
            events.Add(new Rc901aRawInputEvent(
                timestamp,
                Rc901aRawInputKind.ConsumerControl,
                _consumerUsage,
                IsPressed: false));
        }
        if (usage != 0)
        {
            events.Add(new Rc901aRawInputEvent(
                timestamp,
                Rc901aRawInputKind.ConsumerControl,
                usage,
                IsPressed: true));
        }

        _consumerUsage = usage;
        return events;
    }

    public IReadOnlyList<Rc901aRawInputEvent> Reset(DateTimeOffset timestamp)
    {
        var events = _pressedKeyboardCodes
            .Select(code => new Rc901aRawInputEvent(
                timestamp,
                Rc901aRawInputKind.Keyboard,
                code,
                IsPressed: false))
            .ToList();
        _pressedKeyboardCodes.Clear();
        if (_consumerUsage != 0)
        {
            events.Add(new Rc901aRawInputEvent(
                timestamp,
                Rc901aRawInputKind.ConsumerControl,
                _consumerUsage,
                IsPressed: false));
            _consumerUsage = 0;
        }
        return events;
    }
}

public sealed class Rc901aRawInputInterpreter
{
    private readonly IReadOnlyDictionary<
        (Rc901aRawInputKind Kind, ushort Code),
        ControllerControl> _controlsBySignal;

    public Rc901aRawInputInterpreter(
        IEnumerable<Rc901aInputBinding>? learnedBindings = null)
    {
        Bindings = Rc901aInputBindings.CombineWithVerifiedDefaults(
            learnedBindings);
        _controlsBySignal = Bindings.ToDictionary(
            binding => (binding.Kind, binding.Code),
            binding => binding.Control);
    }

    public IReadOnlyList<Rc901aInputBinding> Bindings { get; }

    public bool TryInterpret(
        Rc901aRawInputEvent input,
        ControllerSnapshot previous,
        out ControllerSnapshot snapshot)
    {
        if (!_controlsBySignal.TryGetValue(
                (input.Kind, input.Code),
                out var control))
        {
            snapshot = previous;
            return false;
        }

        snapshot = previous.With(control, input.IsPressed ? 1f : 0f);
        return true;
    }
}
