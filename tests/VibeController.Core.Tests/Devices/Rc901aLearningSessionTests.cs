using VibeController.Core.Devices;
using VibeController.Core.Domain;

namespace VibeController.Core.Tests.Devices;

public sealed class Rc901aLearningSessionTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-07-26T12:00:00Z");

    [Fact]
    public void Start_CreatesAnOpaqueThirtySecondSession()
    {
        var learning = new Rc901aLearningSession();

        var firstSessionId = learning.Start(
            ControllerControl.RemoteChannelUp,
            Now);

        Assert.False(string.IsNullOrWhiteSpace(firstSessionId));
        Assert.Equal(Rc901aLearningPhase.AwaitingPress, learning.Status.Phase);
        Assert.Equal(firstSessionId, learning.Status.SessionId);
        Assert.Equal(
            ControllerControl.RemoteChannelUp,
            learning.Status.Target);
        Assert.Equal(Now.AddSeconds(30), learning.Status.ExpiresAt);
        Assert.True(learning.IsActive);

        Assert.True(learning.Cancel(firstSessionId));
        var secondSessionId = learning.Start(
            ControllerControl.RemoteChannelDown,
            Now.AddSeconds(1));
        Assert.NotEqual(firstSessionId, secondSessionId);
    }

    [Theory]
    [InlineData(ControllerControl.RemoteUp)]
    [InlineData(ControllerControl.RemoteDown)]
    [InlineData(ControllerControl.RemoteLeft)]
    [InlineData(ControllerControl.RemoteRight)]
    [InlineData(ControllerControl.RemoteOk)]
    public void Start_RejectsHardwareVerifiedSemanticControls(
        ControllerControl control)
    {
        var learning = new Rc901aLearningSession();

        var sessionId = learning.Start(control, Now);

        Assert.Null(sessionId);
        Assert.Equal(Rc901aLearningPhase.Idle, learning.Status.Phase);
        Assert.False(learning.IsActive);
    }

    [Fact]
    public void Start_AdvancedCompatibilityModeAllowsAVerifiedSemanticControl()
    {
        var learning = new Rc901aLearningSession();

        var sessionId = learning.Start(
            ControllerControl.RemoteBack,
            Now,
            allowVerifiedOverride: true);

        Assert.NotNull(sessionId);
        Assert.Equal(
            ControllerControl.RemoteBack,
            learning.Status.Target);
    }

    [Fact]
    public void MatchingRelease_AdvancesTheCandidateToReview()
    {
        var learning = Started();
        var sessionId = learning.Status.SessionId!;

        Assert.True(learning.ObserveInput(
            Input(Rc901aRawInputKind.ConsumerControl, 0x0224, isPressed: true),
            []));
        Assert.Equal(Rc901aLearningPhase.AwaitingRelease, learning.Status.Phase);
        Assert.Equal(
            new Rc901aInputSignal(
                Rc901aRawInputKind.ConsumerControl,
                0x0224),
            learning.Status.Candidate);

        Assert.True(learning.ObserveInput(
            Input(Rc901aRawInputKind.ConsumerControl, 0x0224, isPressed: false),
            []));

        Assert.Equal(Rc901aLearningPhase.Review, learning.Status.Phase);
        Assert.Equal(sessionId, learning.Status.SessionId);
    }

    [Fact]
    public void Start_IgnoresPressAndReleaseAtOrBeforeTheInputCutoff()
    {
        var learning = Started();

        Assert.False(learning.ObserveInput(
            Input(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                isPressed: true,
                timestamp: Now.AddMilliseconds(-1)),
            []));
        Assert.False(learning.ObserveInput(
            Input(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                isPressed: true,
                timestamp: Now),
            []));
        Assert.Equal(Rc901aLearningPhase.AwaitingPress, learning.Status.Phase);

        Assert.True(learning.ObserveInput(
            Input(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                isPressed: true,
                timestamp: Now.AddMilliseconds(1)),
            []));
        Assert.False(learning.ObserveInput(
            Input(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                isPressed: false,
                timestamp: Now),
            []));
        Assert.Equal(Rc901aLearningPhase.AwaitingRelease, learning.Status.Phase);

        Assert.True(learning.ObserveInput(
            Input(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                isPressed: false,
                timestamp: Now.AddMilliseconds(2)),
            []));
        Assert.Equal(Rc901aLearningPhase.Review, learning.Status.Phase);
    }

    [Fact]
    public void AwaitingRelease_IgnoresDuplicateMakeWrongReleaseAndOtherInput()
    {
        var learning = Started();
        var press = Input(
            Rc901aRawInputKind.ConsumerControl,
            0x0224,
            isPressed: true);
        Assert.True(learning.ObserveInput(press, []));

        Assert.False(learning.ObserveInput(press with
        {
            Timestamp = Now.AddMilliseconds(10),
        }, []));
        Assert.False(learning.ObserveInput(
            Input(Rc901aRawInputKind.ConsumerControl, 0x0225, isPressed: false),
            []));
        Assert.False(learning.ObserveInput(
            Input(Rc901aRawInputKind.Keyboard, 0x0224, isPressed: false),
            []));
        Assert.False(learning.ObserveInput(
            Input(Rc901aRawInputKind.Keyboard, 0x26, isPressed: true),
            Rc901aInputBindings.VerifiedDefaults));

        Assert.Equal(Rc901aLearningPhase.AwaitingRelease, learning.Status.Phase);
        Assert.Equal(
            new Rc901aInputSignal(
                Rc901aRawInputKind.ConsumerControl,
                0x0224),
            learning.Status.Candidate);
    }

    [Fact]
    public void Retry_ReturnsReviewSessionToAwaitingPress()
    {
        var learning = InReview([]);
        var sessionId = learning.Status.SessionId!;
        var retriedAt = Now.AddSeconds(10);

        Assert.True(learning.Retry(sessionId, retriedAt));

        Assert.Equal(Rc901aLearningPhase.AwaitingPress, learning.Status.Phase);
        Assert.Null(learning.Status.Candidate);
        Assert.Null(learning.Status.Conflict);
        Assert.Equal(retriedAt.AddSeconds(30), learning.Status.ExpiresAt);
    }

    [Fact]
    public void Retry_IgnoresInputsAtOrBeforeTheNewInputCutoff()
    {
        var learning = InReview([]);
        var sessionId = learning.Status.SessionId!;
        var retriedAt = Now.AddSeconds(10);
        Assert.True(learning.Retry(sessionId, retriedAt));

        Assert.False(learning.ObserveInput(
            Input(
                Rc901aRawInputKind.ConsumerControl,
                0x0225,
                isPressed: true,
                timestamp: retriedAt),
            []));
        Assert.False(learning.ObserveInput(
            Input(
                Rc901aRawInputKind.ConsumerControl,
                0x0225,
                isPressed: false,
                timestamp: retriedAt.AddMilliseconds(-1)),
            []));
        Assert.Equal(Rc901aLearningPhase.AwaitingPress, learning.Status.Phase);

        Assert.True(learning.ObserveInput(
            Input(
                Rc901aRawInputKind.ConsumerControl,
                0x0225,
                isPressed: true,
                timestamp: retriedAt.AddMilliseconds(1)),
            []));
        Assert.True(learning.ObserveInput(
            Input(
                Rc901aRawInputKind.ConsumerControl,
                0x0225,
                isPressed: false,
                timestamp: retriedAt.AddMilliseconds(2)),
            []));
        Assert.Equal(Rc901aLearningPhase.Review, learning.Status.Phase);
    }

    [Fact]
    public void CancelTimeoutAndDisconnect_ReturnToIdleWithoutProducingABinding()
    {
        var cancelled = InReview([]);
        var cancelledId = cancelled.Status.SessionId!;
        Assert.True(cancelled.Cancel(cancelledId));
        AssertIdle(cancelled);
        Assert.False(cancelled.TryBeginSave(cancelledId, out _));

        var timedOut = InReview([]);
        var timedOutId = timedOut.Status.SessionId!;
        Assert.True(timedOut.Expire(Now.AddSeconds(30)));
        AssertIdle(timedOut);
        Assert.False(timedOut.TryBeginSave(timedOutId, out _));

        var disconnected = InReview([]);
        var disconnectedId = disconnected.Status.SessionId!;
        Assert.True(disconnected.Disconnect());
        AssertIdle(disconnected);
        Assert.False(disconnected.TryBeginSave(disconnectedId, out _));
    }

    [Fact]
    public void VerifiedConflict_IsExposedAndCannotBeConfirmed()
    {
        var learning = InReview(
            Rc901aInputBindings.VerifiedDefaults,
            kind: Rc901aRawInputKind.Keyboard,
            code: 0x26);
        var sessionId = learning.Status.SessionId!;

        Assert.Equal(
            new Rc901aLearningConflict(
                ControllerControl.RemoteUp,
                Rc901aBindingSource.VerifiedDefault),
            learning.Status.Conflict);
        Assert.False(learning.TryBeginSave(sessionId, out var binding));
        Assert.Null(binding);
        Assert.Equal(Rc901aLearningPhase.Review, learning.Status.Phase);
    }

    [Fact]
    public void AdvancedCompatibilityModeCanConfirmAVerifiedConflict()
    {
        var learning = new Rc901aLearningSession();
        var sessionId = learning.Start(
            ControllerControl.RemoteBack,
            Now,
            allowVerifiedOverride: true)!;
        Assert.True(learning.ObserveInput(
            Input(
                Rc901aRawInputKind.DriverHidUsage,
                0xF1,
                isPressed: true),
            Rc901aInputBindings.VerifiedDefaults));
        Assert.True(learning.ObserveInput(
            Input(
                Rc901aRawInputKind.DriverHidUsage,
                0xF1,
                isPressed: false),
            Rc901aInputBindings.VerifiedDefaults));

        Assert.True(learning.TryBeginSave(sessionId, out var binding));
        Assert.Equal(
            new Rc901aInputBinding(
                Rc901aRawInputKind.DriverHidUsage,
                0xF1,
                ControllerControl.RemoteBack,
                Rc901aBindingSource.Learned),
            binding);
    }

    [Fact]
    public void CancelClearsTheAdvancedOverrideGate()
    {
        var learning = new Rc901aLearningSession();
        var sessionId = learning.Start(
            ControllerControl.RemoteBack,
            Now,
            allowVerifiedOverride: true)!;
        Assert.True(learning.Cancel(sessionId));

        Assert.Null(learning.Start(ControllerControl.RemoteBack, Now));
    }

    [Fact]
    public void LearnedConflict_ExplicitConfirmProducesAnUpsertThatMovesTheSignal()
    {
        var existing = new Rc901aInputBinding(
            Rc901aRawInputKind.ConsumerControl,
            0x0224,
            ControllerControl.RemoteHome,
            Rc901aBindingSource.Learned);
        var learning = InReview([existing]);
        var sessionId = learning.Status.SessionId!;

        Assert.Equal(
            new Rc901aLearningConflict(
                ControllerControl.RemoteHome,
                Rc901aBindingSource.Learned),
            learning.Status.Conflict);
        Assert.True(learning.TryBeginSave(sessionId, out var replacement));
        Assert.Equal(Rc901aLearningPhase.Saving, learning.Status.Phase);
        Assert.Equal(
            new Rc901aInputBinding(
                Rc901aRawInputKind.ConsumerControl,
                0x0224,
                ControllerControl.RemoteChannelUp,
                Rc901aBindingSource.Learned),
            replacement);

        var updated = Rc901aInputBindings.Upsert([existing], replacement!);
        var moved = Assert.Single(updated);
        Assert.Equal(ControllerControl.RemoteChannelUp, moved.Control);
    }

    [Fact]
    public void StaleSessionIds_AreSafeNoOps()
    {
        var learning = InReview([]);
        var before = learning.Status;

        Assert.False(learning.Retry("stale-session", Now.AddSeconds(10)));
        Assert.False(learning.Cancel("stale-session"));
        Assert.False(learning.TryBeginSave("stale-session", out var binding));

        Assert.Null(binding);
        Assert.Equal(before, learning.Status);
    }

    [Fact]
    public void CompleteSave_ReturnsToIdleAndOnlyActivePhasesSuppressMapping()
    {
        var learning = Started();
        var sessionId = learning.Status.SessionId!;
        Assert.False(true && !learning.IsActive);

        _ = learning.ObserveInput(
            Input(Rc901aRawInputKind.ConsumerControl, 0x0224, isPressed: true),
            []);
        Assert.False(true && !learning.IsActive);

        _ = learning.ObserveInput(
            Input(Rc901aRawInputKind.ConsumerControl, 0x0224, isPressed: false),
            []);
        Assert.False(true && !learning.IsActive);

        Assert.True(learning.TryBeginSave(sessionId, out _));
        Assert.False(true && !learning.IsActive);
        Assert.True(learning.CompleteSave(sessionId));

        AssertIdle(learning);
        Assert.True(true && !learning.IsActive);
    }

    [Fact]
    public void DisconnectDuringSaving_InvalidatesThePendingCommit()
    {
        var learning = InReview([]);
        var sessionId = learning.Status.SessionId!;
        Assert.True(learning.TryBeginSave(sessionId, out _));
        Assert.True(learning.CanCompleteSave(sessionId));

        Assert.True(learning.Disconnect());

        Assert.False(learning.CanCompleteSave(sessionId));
        Assert.False(learning.CompleteSave(sessionId));
        AssertIdle(learning);
    }

    private static Rc901aLearningSession Started()
    {
        var learning = new Rc901aLearningSession();
        Assert.NotNull(learning.Start(
            ControllerControl.RemoteChannelUp,
            Now));
        return learning;
    }

    private static Rc901aLearningSession InReview(
        IReadOnlyList<Rc901aInputBinding> bindings,
        Rc901aRawInputKind kind = Rc901aRawInputKind.ConsumerControl,
        ushort code = 0x0224)
    {
        var learning = Started();
        Assert.True(learning.ObserveInput(
            Input(kind, code, isPressed: true),
            bindings));
        Assert.True(learning.ObserveInput(
            Input(kind, code, isPressed: false),
            bindings));
        return learning;
    }

    private static Rc901aRawInputEvent Input(
        Rc901aRawInputKind kind,
        ushort code,
        bool isPressed,
        DateTimeOffset? timestamp = null) => new(
        timestamp ?? Now.AddMilliseconds(1),
        kind,
        code,
        isPressed);

    private static void AssertIdle(Rc901aLearningSession learning)
    {
        Assert.Equal(Rc901aLearningPhase.Idle, learning.Status.Phase);
        Assert.Null(learning.Status.SessionId);
        Assert.Null(learning.Status.Target);
        Assert.Null(learning.Status.Candidate);
        Assert.Null(learning.Status.Conflict);
        Assert.Null(learning.Status.ExpiresAt);
        Assert.False(learning.IsActive);
    }
}
