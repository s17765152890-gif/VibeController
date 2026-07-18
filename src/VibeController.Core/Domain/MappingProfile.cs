using System.Collections.Immutable;

namespace VibeController.Core.Domain;

public sealed record MappingProfile
{
    public MappingProfile()
    {
    }

    public MappingProfile(
        string name,
        IEnumerable<KeyValuePair<ControllerControl, MappedAction>> mappings)
    {
        Name = name;
        Mappings = mappings.ToImmutableDictionary();
    }

    public string Name { get; init; } = string.Empty;

    public ImmutableDictionary<ControllerControl, MappedAction> Mappings { get; init; } =
        ImmutableDictionary<ControllerControl, MappedAction>.Empty;

    public bool TryGetAction(ControllerControl control, out MappedAction action) =>
        Mappings.TryGetValue(control, out action!);

    public MappingProfile WithMapping(ControllerControl control, MappedAction action) =>
        new(Name, Mappings.SetItem(control, action));
}
