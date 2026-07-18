using VibeController.Core.Domain;
using VibeController.Core.Mapping;

namespace VibeController.Core.Tests.Mapping;

public sealed class MappedActionCodecTests
{
    [Fact]
    public void Core_ContainsSharedMappedActionCodec()
    {
        var codec = typeof(DefaultProfileFactory).Assembly.GetType(
            "VibeController.Core.Mapping.MappedActionCodec");

        Assert.NotNull(codec);
    }

    [Theory]
    [InlineData("dictation", MappedActionKind.CodexDictation)]
    [InlineData("send", MappedActionKind.Send)]
    [InlineData("cancel", MappedActionKind.Cancel)]
    [InlineData("commandPalette", MappedActionKind.CommandPalette)]
    [InlineData("previousChat", MappedActionKind.PreviousChat)]
    [InlineData("nextChat", MappedActionKind.NextChat)]
    [InlineData("increaseReasoning", MappedActionKind.IncreaseReasoning)]
    [InlineData("decreaseReasoning", MappedActionKind.DecreaseReasoning)]
    [InlineData("activateCodex", MappedActionKind.ActivateCodex)]
    [InlineData("mouseMove", MappedActionKind.MouseMove)]
    [InlineData("mouseLeftClick", MappedActionKind.MouseLeftClick)]
    [InlineData("mouseRightClick", MappedActionKind.MouseRightClick)]
    [InlineData("mouseScrollUp", MappedActionKind.MouseScrollUp)]
    [InlineData("mouseScrollDown", MappedActionKind.MouseScrollDown)]
    [InlineData("none", MappedActionKind.None)]
    public void TryParse_AcceptsEveryExposedAction(
        string name,
        MappedActionKind expectedKind)
    {
        var parsed = MappedActionCodec.TryParse(name, out var action);

        Assert.True(parsed);
        Assert.Equal(expectedKind, action.Kind);
        Assert.Equal(name, MappedActionCodec.Format(action));
    }

    [Theory]
    [InlineData("previousRecentThread", "PreviousRecentThread")]
    [InlineData("nextRecentThread", "NextRecentThread")]
    [InlineData("previousTab", "PreviousTab")]
    [InlineData("nextTab", "NextTab")]
    public void TryParse_AcceptsOptionalCodexNavigationActions(
        string name,
        string expectedKindName)
    {
        var parsed = MappedActionCodec.TryParse(name, out var action);

        Assert.True(parsed);
        Assert.Equal(expectedKindName, action.Kind.ToString());
        Assert.Equal(name, MappedActionCodec.Format(action));
    }

    [Fact]
    public void TryParse_AcceptsCustomKeyboardShortcut()
    {
        var parsed = MappedActionCodec.TryParse(
            "shortcut:Ctrl+Shift+K",
            out var action);

        Assert.True(parsed);
        Assert.Equal(MappedActionKind.KeyboardShortcut, action.Kind);
        Assert.Equal("K", action.Shortcut?.Key);
        Assert.Equal(
            [KeyModifier.Control, KeyModifier.Shift],
            action.Shortcut?.Modifiers);
        Assert.Equal("shortcut:Ctrl+Shift+K", MappedActionCodec.Format(action));
    }

    [Theory]
    [InlineData("previousModel")]
    [InlineData("nextModel")]
    [InlineData("mouseScroll")]
    public void TryParse_RejectsRemovedOrAmbiguousActions(string name)
    {
        Assert.False(MappedActionCodec.TryParse(name, out _));
    }
}
