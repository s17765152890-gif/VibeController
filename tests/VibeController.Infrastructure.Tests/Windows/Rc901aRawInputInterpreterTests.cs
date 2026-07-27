using VibeController.Core.Devices;
using VibeController.Core.Domain;
using VibeController.Infrastructure.Windows;

namespace VibeController.Infrastructure.Tests.Windows;

public sealed class Rc901aRawInputInterpreterTests
{
    [Fact]
    public void Bindings_ContainTheDriverTableAndWindowsFallbackSignals()
    {
        var interpreter = new Rc901aRawInputInterpreter();

        Assert.Equal(
            22,
            interpreter.Bindings.Count(item =>
                item.Kind == Rc901aRawInputKind.DriverHidUsage));
        Assert.Equal(
            6,
            interpreter.Bindings.Count(item =>
                item.Kind == Rc901aRawInputKind.Keyboard));
        Assert.Contains(
            interpreter.Bindings,
            item =>
                item.Kind == Rc901aRawInputKind.Keyboard &&
                item.Code == 0x5D &&
                item.Control == ControllerControl.RemoteMenu);
    }

    [Theory]
    [MemberData(nameof(GuessedSignals))]
    public void TryInterpret_GuessedSignalsRemainInactiveUntilLearned(
        Rc901aRawInputKind kind,
        ushort code)
    {
        var interpreter = new Rc901aRawInputInterpreter();

        var interpreted = interpreter.TryInterpret(
            new Rc901aRawInputEvent(
                DateTimeOffset.Parse("2026-07-26T12:00:00Z"),
                kind,
                code,
                IsPressed: true),
            ControllerSnapshot.Empty,
            out var snapshot);

        Assert.False(interpreted);
        Assert.Same(ControllerSnapshot.Empty, snapshot);
    }

    [Fact]
    public void TryInterpret_LearnedBindingMapsTheCapturedSignal()
    {
        var interpreter = new Rc901aRawInputInterpreter(
        [
            new(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                ControllerControl.RemoteBack,
                Rc901aBindingSource.Learned),
        ]);

        var interpreted = interpreter.TryInterpret(
            new Rc901aRawInputEvent(
                DateTimeOffset.Parse("2026-07-26T12:00:00Z"),
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                IsPressed: true),
            ControllerSnapshot.Empty,
            out var snapshot);

        Assert.True(interpreted);
        Assert.Equal(1f, snapshot.GetValue(ControllerControl.RemoteBack));
    }

    [Fact]
    public void Constructor_AppliesExplicitCompatibilityOverrides()
    {
        var interpreter = new Rc901aRawInputInterpreter(
        [
            new(
                Rc901aRawInputKind.Keyboard,
                0x26,
                ControllerControl.RemoteBack,
                Rc901aBindingSource.Learned),
            new(
                Rc901aRawInputKind.ConsumerControl,
                0x1234,
                ControllerControl.RemoteUp,
                Rc901aBindingSource.Learned),
        ]);

        var verifiedInterpreted = interpreter.TryInterpret(
            new Rc901aRawInputEvent(
                DateTimeOffset.Parse("2026-07-26T12:00:00Z"),
                Rc901aRawInputKind.Keyboard,
                0x26,
                IsPressed: true),
            ControllerSnapshot.Empty,
            out var verifiedSnapshot);
        var conflictingLearnedInterpreted = interpreter.TryInterpret(
            new Rc901aRawInputEvent(
                DateTimeOffset.Parse("2026-07-26T12:00:01Z"),
                Rc901aRawInputKind.ConsumerControl,
                0x1234,
                IsPressed: true),
            verifiedSnapshot,
            out var conflictingSnapshot);

        Assert.True(verifiedInterpreted);
        Assert.Equal(
            1f,
            verifiedSnapshot.GetValue(ControllerControl.RemoteBack));
        Assert.True(conflictingLearnedInterpreted);
        Assert.Equal(
            1f,
            conflictingSnapshot.GetValue(ControllerControl.RemoteUp));
        Assert.Equal(27, interpreter.Bindings.Count);
    }

    [Fact]
    public void TryInterpret_ReleaseClearsTheMappedControl()
    {
        var interpreter = new Rc901aRawInputInterpreter();
        var pressed = ControllerSnapshot.Empty.With(
            ControllerControl.RemoteOk,
            1f);

        var interpreted = interpreter.TryInterpret(
            new Rc901aRawInputEvent(
                DateTimeOffset.Parse("2026-07-26T12:00:00Z"),
                Rc901aRawInputKind.Keyboard,
                0x0D,
                IsPressed: false),
            pressed,
            out var snapshot);

        Assert.True(interpreted);
        Assert.Equal(0f, snapshot.GetValue(ControllerControl.RemoteOk));
    }

    [Fact]
    public void TryInterpret_UnknownInputLeavesSnapshotUnchanged()
    {
        var interpreter = new Rc901aRawInputInterpreter();

        var interpreted = interpreter.TryInterpret(
            new Rc901aRawInputEvent(
                DateTimeOffset.Parse("2026-07-26T12:00:00Z"),
                Rc901aRawInputKind.ConsumerControl,
                0xFFFF,
                IsPressed: true),
            ControllerSnapshot.Empty,
            out var snapshot);

        Assert.False(interpreted);
        Assert.Same(ControllerSnapshot.Empty, snapshot);
    }

    [Theory]
    [InlineData(0x0416, 0x0301, true)]
    [InlineData(0x0416, 0x0302, false)]
    [InlineData(0x054C, 0x0CE6, false)]
    public void DeviceIdentity_RequiresTheRc901aVendorAndProduct(
        ushort vendorId,
        ushort productId,
        bool expected)
    {
        Assert.Equal(
            expected,
            Rc901aRawInputDeviceIdentity.IsSupported(
                vendorId,
                productId,
                versionNumber: 0x0003,
                usagePage: 0x0001,
                usage: 0x0006));
    }

    [Fact]
    public void Decoder_KeyboardBreakFlagCreatesARelease()
    {
        var decoder = new Rc901aRawInputDecoder();
        _ = decoder.TryDecodeKeyboard(
            DateTimeOffset.Parse("2026-07-26T11:59:59Z"),
            virtualKey: 0x0D,
            flags: 0x0000,
            out _);

        var decoded = decoder.TryDecodeKeyboard(
            DateTimeOffset.Parse("2026-07-26T12:00:00Z"),
            virtualKey: 0x0D,
            flags: 0x0001,
            out var input);

        Assert.True(decoded);
        Assert.Equal(Rc901aRawInputKind.Keyboard, input.Kind);
        Assert.Equal((ushort)0x0D, input.Code);
        Assert.False(input.IsPressed);
    }

    [Fact]
    public void Decoder_DuplicateKeyboardMakeIsIgnored()
    {
        var decoder = new Rc901aRawInputDecoder();
        var timestamp = DateTimeOffset.Parse("2026-07-26T12:00:00Z");
        var first = decoder.TryDecodeKeyboard(
            timestamp,
            virtualKey: 0x26,
            flags: 0x0000,
            out _);

        var duplicate = decoder.TryDecodeKeyboard(
            timestamp.AddMilliseconds(20),
            virtualKey: 0x26,
            flags: 0x0000,
            out _);

        Assert.True(first);
        Assert.False(duplicate);
    }

    [Fact]
    public void Decoder_ConsumerReportCreatesPressThenRelease()
    {
        var decoder = new Rc901aRawInputDecoder();
        var timestamp = DateTimeOffset.Parse("2026-07-26T12:00:00Z");

        var pressed = decoder.DecodeConsumerReport(
            timestamp,
            [0x03, 0xE9, 0x00]);
        var released = decoder.DecodeConsumerReport(
            timestamp.AddMilliseconds(20),
            [0x03, 0x00, 0x00]);

        var press = Assert.Single(pressed);
        Assert.Equal((ushort)0x00E9, press.Code);
        Assert.True(press.IsPressed);
        var release = Assert.Single(released);
        Assert.Equal((ushort)0x00E9, release.Code);
        Assert.False(release.IsPressed);
    }

    [Fact]
    public void Decoder_ConsumerUsageChangeReleasesBeforePressingNext()
    {
        var decoder = new Rc901aRawInputDecoder();
        var timestamp = DateTimeOffset.Parse("2026-07-26T12:00:00Z");
        _ = decoder.DecodeConsumerReport(timestamp, [0x03, 0xE9, 0x00]);

        var events = decoder.DecodeConsumerReport(
            timestamp.AddMilliseconds(20),
            [0x03, 0xEA, 0x00]);

        Assert.Collection(
            events,
            item =>
            {
                Assert.Equal((ushort)0x00E9, item.Code);
                Assert.False(item.IsPressed);
            },
            item =>
            {
                Assert.Equal((ushort)0x00EA, item.Code);
                Assert.True(item.IsPressed);
            });
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x03 })]
    [InlineData(new byte[] { 0xE8, 0x01, 0x02 })]
    public void Decoder_RejectsShortOrNonConsumerReports(byte[] report)
    {
        var decoder = new Rc901aRawInputDecoder();

        var events = decoder.DecodeConsumerReport(
            DateTimeOffset.Parse("2026-07-26T12:00:00Z"),
            report);

        Assert.Empty(events);
    }

    [Theory]
    [InlineData(0x0416, 0x0301, 0x0002, 0x0001, 0x0006)]
    [InlineData(0x0416, 0x0301, 0x0003, 0x0001, 0x0002)]
    [InlineData(0x0416, 0x0301, 0x0003, 0x000C, 0x0002)]
    public void DeviceIdentity_RejectsWrongRevisionOrTopLevelUsage(
        ushort vendorId,
        ushort productId,
        ushort versionNumber,
        ushort usagePage,
        ushort usage)
    {
        Assert.False(Rc901aRawInputDeviceIdentity.IsSupported(
            vendorId,
            productId,
            versionNumber,
            usagePage,
            usage));
    }

    public static TheoryData<Rc901aRawInputKind, ushort> GuessedSignals => new()
    {
        { Rc901aRawInputKind.Keyboard, 0x1B },
        { Rc901aRawInputKind.Keyboard, 0xA6 },
        { Rc901aRawInputKind.Keyboard, 0x24 },
        { Rc901aRawInputKind.Keyboard, 0xAC },
        { Rc901aRawInputKind.Keyboard, 0xAD },
        { Rc901aRawInputKind.Keyboard, 0xAE },
        { Rc901aRawInputKind.Keyboard, 0xAF },
        { Rc901aRawInputKind.Keyboard, 0x30 },
        { Rc901aRawInputKind.Keyboard, 0x39 },
        { Rc901aRawInputKind.ConsumerControl, 0x0040 },
        { Rc901aRawInputKind.ConsumerControl, 0x0041 },
        { Rc901aRawInputKind.ConsumerControl, 0x0042 },
        { Rc901aRawInputKind.ConsumerControl, 0x0043 },
        { Rc901aRawInputKind.ConsumerControl, 0x0044 },
        { Rc901aRawInputKind.ConsumerControl, 0x0045 },
        { Rc901aRawInputKind.ConsumerControl, 0x0046 },
        { Rc901aRawInputKind.ConsumerControl, 0x009C },
        { Rc901aRawInputKind.ConsumerControl, 0x009D },
        { Rc901aRawInputKind.ConsumerControl, 0x00CF },
        { Rc901aRawInputKind.ConsumerControl, 0x00E2 },
        { Rc901aRawInputKind.ConsumerControl, 0x00E9 },
        { Rc901aRawInputKind.ConsumerControl, 0x00EA },
        { Rc901aRawInputKind.ConsumerControl, 0x0223 },
        { Rc901aRawInputKind.ConsumerControl, 0x0224 },
    };
}
