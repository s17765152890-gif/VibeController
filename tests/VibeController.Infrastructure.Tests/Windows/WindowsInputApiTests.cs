using VibeController.Core.Domain;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class WindowsInputApiTests
{
    [Fact]
    public void SendKeyboard_WhenNativeSendFails_ReleasesEveryPossiblyPressedKey()
    {
        var sender = new FakeNativeInputSender { ThrowOnCall = 3 };
        var input = new WindowsInputApi(sender);
        var strokes = KeyboardInputBuilder.Build(new KeyboardShortcut(
            "D",
            [KeyModifier.Control, KeyModifier.Shift]));

        Assert.Throws<InvalidOperationException>(() => input.SendKeyboard(strokes));

        Assert.Equal(
        [
            new KeyboardInputStroke(VirtualKey.Control, KeyDirection.Down),
            new KeyboardInputStroke(VirtualKey.Shift, KeyDirection.Down),
            new KeyboardInputStroke(VirtualKey.D, KeyDirection.Down),
            new KeyboardInputStroke(VirtualKey.D, KeyDirection.Up),
            new KeyboardInputStroke(VirtualKey.Shift, KeyDirection.Up),
            new KeyboardInputStroke(VirtualKey.Control, KeyDirection.Up),
        ],
            sender.KeyboardStrokes);
    }

    private sealed class FakeNativeInputSender : INativeInputSender
    {
        private int _callCount;

        public int ThrowOnCall { get; init; }

        public List<KeyboardInputStroke> KeyboardStrokes { get; } = [];

        public void SendKeyboardStroke(KeyboardInputStroke stroke)
        {
            _callCount++;
            KeyboardStrokes.Add(stroke);
            if (_callCount == ThrowOnCall)
            {
                throw new InvalidOperationException("simulated SendInput failure");
            }
        }

        public void MoveMouse(int deltaX, int deltaY)
        {
        }

        public void Click(MouseButton button)
        {
        }

        public void Scroll(int delta)
        {
        }
    }
}
