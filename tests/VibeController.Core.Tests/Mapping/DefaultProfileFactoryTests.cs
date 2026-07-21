using VibeController.Core.Domain;
using VibeController.Core.Mapping;

namespace VibeController.Core.Tests.Mapping;

public sealed class DefaultProfileFactoryTests
{
    [Theory]
    [InlineData("MouseScrollUp")]
    [InlineData("MouseScrollDown")]
    public void MappedActionKind_ContainsDirectionalScrollActions(string actionName)
    {
        Assert.True(Enum.IsDefined(typeof(MappedActionKind), actionName));
    }

    [Theory]
    [InlineData(ControllerControl.Menu, MappedActionKind.ActivateCodex)]
    [InlineData(ControllerControl.X, MappedActionKind.CodexDictation)]
    [InlineData(ControllerControl.A, MappedActionKind.Send)]
    [InlineData(ControllerControl.Y, MappedActionKind.CommandPalette)]
    [InlineData(ControllerControl.LeftBumper, MappedActionKind.PreviousChat)]
    [InlineData(ControllerControl.RightBumper, MappedActionKind.NextChat)]
    [InlineData(ControllerControl.DPadLeft, MappedActionKind.DecreaseReasoning)]
    [InlineData(ControllerControl.DPadRight, MappedActionKind.IncreaseReasoning)]
    [InlineData(ControllerControl.LeftStickX, MappedActionKind.MouseMove)]
    [InlineData(ControllerControl.LeftStickY, MappedActionKind.MouseMove)]
    [InlineData(ControllerControl.RightTrigger, MappedActionKind.MouseLeftClick)]
    [InlineData(ControllerControl.LeftTrigger, MappedActionKind.MouseRightClick)]
    public void Create_ContainsExpectedBuiltInMappings(
        ControllerControl control,
        MappedActionKind expectedKind)
    {
        var profile = DefaultProfileFactory.Create();

        Assert.True(profile.TryGetAction(control, out var action));
        Assert.Equal(expectedKind, action.Kind);
    }

    [Theory]
    [InlineData(ControllerControl.DPadUp, "ArrowUp")]
    [InlineData(ControllerControl.DPadDown, "ArrowDown")]
    [InlineData(ControllerControl.RightStickUp, "ArrowUp")]
    [InlineData(ControllerControl.RightStickDown, "ArrowDown")]
    [InlineData(ControllerControl.RightStickLeft, "ArrowLeft")]
    [InlineData(ControllerControl.RightStickRight, "ArrowRight")]
    public void Create_MapsTextNavigationControlsToKeyboardKeys(
        ControllerControl control,
        string expectedKey)
    {
        var profile = DefaultProfileFactory.Create();

        Assert.True(profile.TryGetAction(control, out var action));
        Assert.Equal(MappedActionKind.KeyboardShortcut, action.Kind);
        Assert.NotNull(action.Shortcut);
        Assert.Equal(expectedKey, action.Shortcut.Key);
        Assert.Empty(action.Shortcut.Modifiers);
    }

    [Fact]
    public void Create_MapsBToBackspaceInsteadOfCancel()
    {
        var profile = DefaultProfileFactory.Create();

        Assert.True(profile.TryGetAction(ControllerControl.B, out var action));
        Assert.Equal(MappedActionKind.KeyboardShortcut, action.Kind);
        Assert.Equal("Backspace", action.Shortcut?.Key);
        Assert.Empty(action.Shortcut?.Modifiers ?? []);
    }

    [Fact]
    public void Create_AddsDualSenseTouchpadDefaultsWithoutChangingXboxButtons()
    {
        var profile = DefaultProfileFactory.Create();

        Assert.Equal(MappedActionKind.CodexDictation,
            profile.Mappings[ControllerControl.X].Kind);
        Assert.Equal(MappedActionKind.Send,
            profile.Mappings[ControllerControl.A].Kind);
        Assert.Equal("Backspace",
            profile.Mappings[ControllerControl.B].Shortcut?.Key);
        Assert.Equal(MappedActionKind.CommandPalette,
            profile.Mappings[ControllerControl.Y].Kind);
        Assert.Equal(MappedActionKind.MouseMove,
            profile.Mappings[ControllerControl.TouchpadX].Kind);
        Assert.Equal(MappedActionKind.MouseMove,
            profile.Mappings[ControllerControl.TouchpadY].Kind);
        Assert.Equal(MappedActionKind.MouseLeftClick,
            profile.Mappings[ControllerControl.TouchpadButton].Kind);
    }

    [Theory]
    [InlineData(ControllerControl.View)]
    [InlineData(ControllerControl.LeftStickButton)]
    [InlineData(ControllerControl.RightStickButton)]
    [InlineData(ControllerControl.RightStickX)]
    [InlineData(ControllerControl.RightStickY)]
    public void Create_LeavesNonEssentialControlsUnassigned(ControllerControl control)
    {
        var profile = DefaultProfileFactory.Create();

        Assert.True(profile.TryGetAction(control, out var action));
        Assert.Equal(MappedActionKind.None, action.Kind);
    }

    [Theory]
    [InlineData(ControllerControl.RemoteOk, MappedActionKind.Send)]
    [InlineData(ControllerControl.RemoteHome, MappedActionKind.ActivateCodex)]
    [InlineData(ControllerControl.RemoteMenu, MappedActionKind.CommandPalette)]
    [InlineData(ControllerControl.RemoteMic, MappedActionKind.CodexDictation)]
    public void Create_ContainsRc901aSemanticDefaults(
        ControllerControl control,
        MappedActionKind expectedKind)
    {
        var profile = DefaultProfileFactory.Create();

        Assert.Equal(expectedKind, profile.Mappings[control].Kind);
    }

    [Theory]
    [InlineData(ControllerControl.RemoteBack, "Backspace")]
    [InlineData(ControllerControl.RemoteUp, "ArrowUp")]
    [InlineData(ControllerControl.RemoteDown, "ArrowDown")]
    [InlineData(ControllerControl.RemoteLeft, "ArrowLeft")]
    [InlineData(ControllerControl.RemoteRight, "ArrowRight")]
    public void Create_MapsRc901aEditingControlsToKeyboardKeys(
        ControllerControl control,
        string expectedKey)
    {
        var profile = DefaultProfileFactory.Create();

        Assert.Equal(MappedActionKind.KeyboardShortcut, profile.Mappings[control].Kind);
        Assert.Equal(expectedKey, profile.Mappings[control].Shortcut?.Key);
    }

    [Theory]
    [InlineData(ControllerControl.RemoteVolumeUp)]
    [InlineData(ControllerControl.RemoteVolumeDown)]
    [InlineData(ControllerControl.RemoteMute)]
    [InlineData(ControllerControl.RemoteChannelUp)]
    [InlineData(ControllerControl.RemoteChannelDown)]
    [InlineData(ControllerControl.RemoteDigit0)]
    [InlineData(ControllerControl.RemoteDigit9)]
    public void Create_LeavesUnverifiedRc901aControlsUnassigned(ControllerControl control)
    {
        var profile = DefaultProfileFactory.Create();

        Assert.Equal(MappedActionKind.None, profile.Mappings[control].Kind);
    }
}
