using System.Collections.Immutable;

namespace VibeController.Core.Domain;

public sealed record ControllerSnapshot
{
    private readonly ImmutableDictionary<ControllerControl, float> _values;

    private ControllerSnapshot(ImmutableDictionary<ControllerControl, float> values)
    {
        _values = values;
    }

    public static ControllerSnapshot Empty { get; } =
        new(ImmutableDictionary<ControllerControl, float>.Empty);

    public IEnumerable<ControllerControl> Controls => _values.Keys;

    public float GetValue(ControllerControl control) =>
        _values.GetValueOrDefault(control, 0f);

    public ControllerSnapshot With(ControllerControl control, float value) =>
        new(_values.SetItem(control, value));
}
