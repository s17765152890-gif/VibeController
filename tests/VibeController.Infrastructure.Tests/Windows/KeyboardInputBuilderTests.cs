using VibeController.Core.Domain;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class KeyboardInputBuilderTests
{
    [Fact]
    public void Build_CreatesModifierDownKeyPressAndReverseReleaseOrder()
    {
        var shortcut = new KeyboardShortcut(
            "D",
            [KeyModifier.Control, KeyModifier.Shift]);

        var strokes = KeyboardInputBuilder.Build(shortcut);

        Assert.Collection(
            strokes,
            stroke => AssertStroke(stroke, VirtualKey.Control, KeyDirection.Down),
            stroke => AssertStroke(stroke, VirtualKey.Shift, KeyDirection.Down),
            stroke => AssertStroke(stroke, VirtualKey.D, KeyDirection.Down),
            stroke => AssertStroke(stroke, VirtualKey.D, KeyDirection.Up),
            stroke => AssertStroke(stroke, VirtualKey.Shift, KeyDirection.Up),
            stroke => AssertStroke(stroke, VirtualKey.Control, KeyDirection.Up));
    }

    [Theory]
    [InlineData("Enter", VirtualKey.Enter)]
    [InlineData("Escape", VirtualKey.Escape)]
    [InlineData("Backspace", VirtualKey.Backspace)]
    [InlineData("Tab", (VirtualKey)0x09)]
    [InlineData("PageUp", (VirtualKey)0x21)]
    [InlineData("PageDown", (VirtualKey)0x22)]
    [InlineData("ArrowUp", VirtualKey.Up)]
    [InlineData("ArrowDown", VirtualKey.Down)]
    [InlineData("ArrowLeft", VirtualKey.Left)]
    [InlineData("ArrowRight", VirtualKey.Right)]
    [InlineData("[", VirtualKey.OemOpenBrackets)]
    [InlineData("]", VirtualKey.OemCloseBrackets)]
    [InlineData("F8", (VirtualKey)0x77)]
    [InlineData("F9", (VirtualKey)0x78)]
    [InlineData("F10", (VirtualKey)0x79)]
    [InlineData("F11", (VirtualKey)0x7A)]
    [InlineData("F12", (VirtualKey)0x7B)]
    public void Build_MapsNamedKeys(string key, VirtualKey expectedVirtualKey)
    {
        var strokes = KeyboardInputBuilder.Build(new KeyboardShortcut(key));

        Assert.Collection(
            strokes,
            stroke => AssertStroke(stroke, expectedVirtualKey, KeyDirection.Down),
            stroke => AssertStroke(stroke, expectedVirtualKey, KeyDirection.Up));
    }

    [Fact]
    public void Build_CreatesRareGlobalDictationShortcutInSafeReleaseOrder()
    {
        var shortcut = new KeyboardShortcut(
            "F12",
            [KeyModifier.Control, KeyModifier.Alt, KeyModifier.Shift]);

        var strokes = KeyboardInputBuilder.Build(shortcut);

        Assert.Collection(
            strokes,
            stroke => AssertStroke(stroke, VirtualKey.Control, KeyDirection.Down),
            stroke => AssertStroke(stroke, VirtualKey.Alt, KeyDirection.Down),
            stroke => AssertStroke(stroke, VirtualKey.Shift, KeyDirection.Down),
            stroke => AssertStroke(stroke, (VirtualKey)0x7B, KeyDirection.Down),
            stroke => AssertStroke(stroke, (VirtualKey)0x7B, KeyDirection.Up),
            stroke => AssertStroke(stroke, VirtualKey.Shift, KeyDirection.Up),
            stroke => AssertStroke(stroke, VirtualKey.Alt, KeyDirection.Up),
            stroke => AssertStroke(stroke, VirtualKey.Control, KeyDirection.Up));
    }

    private static void AssertStroke(
        KeyboardInputStroke stroke,
        VirtualKey expectedKey,
        KeyDirection expectedDirection)
    {
        Assert.Equal(expectedKey, stroke.Key);
        Assert.Equal(expectedDirection, stroke.Direction);
    }
}
