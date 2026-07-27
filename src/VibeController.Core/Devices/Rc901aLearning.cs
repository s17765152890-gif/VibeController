using VibeController.Core.Domain;

namespace VibeController.Core.Devices;

public enum Rc901aLearningPhase
{
    Idle,
    AwaitingPress,
    AwaitingRelease,
    Review,
    Saving,
}

public sealed record Rc901aInputSignal(
    Rc901aRawInputKind Kind,
    ushort Code);

public sealed record Rc901aUnknownInputSignal(
    Rc901aRawInputKind Kind,
    ushort Code,
    DateTimeOffset Timestamp);

public sealed record Rc901aLearningConflict(
    ControllerControl Control,
    Rc901aBindingSource Source);

public sealed record Rc901aLearningStatus(
    Rc901aLearningPhase Phase,
    string? SessionId,
    ControllerControl? Target,
    Rc901aInputSignal? Candidate,
    Rc901aLearningConflict? Conflict,
    DateTimeOffset? ExpiresAt)
{
    public static Rc901aLearningStatus Idle { get; } = new(
        Rc901aLearningPhase.Idle,
        null,
        null,
        null,
        null,
        null);
}

public sealed class Rc901aLearningSession
{
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromSeconds(30);
    private DateTimeOffset _acceptInputsAfter = DateTimeOffset.MinValue;
    private bool _allowVerifiedOverride;

    public Rc901aLearningStatus Status { get; private set; } =
        Rc901aLearningStatus.Idle;

    public bool IsActive => Status.Phase != Rc901aLearningPhase.Idle;

    public string? Start(
        ControllerControl target,
        DateTimeOffset now,
        bool allowVerifiedOverride = false)
    {
        if (IsActive || !IsLearnable(target, allowVerifiedOverride))
        {
            return null;
        }

        var sessionId = Guid.NewGuid().ToString("N");
        _acceptInputsAfter = now;
        _allowVerifiedOverride = allowVerifiedOverride;
        Status = new Rc901aLearningStatus(
            Rc901aLearningPhase.AwaitingPress,
            sessionId,
            target,
            null,
            null,
            now.Add(SessionLifetime));
        return sessionId;
    }

    public bool ObserveInput(
        Rc901aRawInputEvent input,
        IReadOnlyList<Rc901aInputBinding> effectiveBindings)
    {
        if (input.Timestamp <= _acceptInputsAfter)
        {
            return false;
        }

        if (Status.Phase == Rc901aLearningPhase.AwaitingPress)
        {
            if (!input.IsPressed)
            {
                return false;
            }

            var candidate = new Rc901aInputSignal(input.Kind, input.Code);
            var conflict = effectiveBindings
                .FirstOrDefault(item =>
                    item.Kind == input.Kind &&
                    item.Code == input.Code);
            Status = Status with
            {
                Phase = Rc901aLearningPhase.AwaitingRelease,
                Candidate = candidate,
                Conflict = conflict is null
                    ? null
                    : new Rc901aLearningConflict(
                        conflict.Control,
                        conflict.Source),
            };
            return true;
        }

        if (Status.Phase != Rc901aLearningPhase.AwaitingRelease ||
            input.IsPressed ||
            Status.Candidate is not { } candidateSignal ||
            candidateSignal.Kind != input.Kind ||
            candidateSignal.Code != input.Code)
        {
            return false;
        }

        Status = Status with { Phase = Rc901aLearningPhase.Review };
        return true;
    }

    public bool Retry(
        string? sessionId,
        DateTimeOffset now)
    {
        if (!Matches(sessionId) ||
            Status.Phase != Rc901aLearningPhase.Review)
        {
            return false;
        }

        Status = Status with
        {
            Phase = Rc901aLearningPhase.AwaitingPress,
            Candidate = null,
            Conflict = null,
            ExpiresAt = now.Add(SessionLifetime),
        };
        _acceptInputsAfter = now;
        return true;
    }

    public bool Cancel(string? sessionId)
    {
        if (!Matches(sessionId))
        {
            return false;
        }

        Reset();
        return true;
    }

    public bool Expire(DateTimeOffset now)
    {
        if (!IsActive ||
            Status.ExpiresAt is not { } expiresAt ||
            now < expiresAt)
        {
            return false;
        }

        Reset();
        return true;
    }

    public bool Disconnect()
    {
        if (!IsActive)
        {
            return false;
        }

        Reset();
        return true;
    }

    public bool TryBeginSave(
        string? sessionId,
        out Rc901aInputBinding? binding)
    {
        binding = null;
        if (!Matches(sessionId) ||
            Status.Phase != Rc901aLearningPhase.Review ||
            Status.Target is not { } target ||
            Status.Candidate is not { } candidate ||
            (Status.Conflict?.Source ==
                Rc901aBindingSource.VerifiedDefault &&
             !_allowVerifiedOverride))
        {
            return false;
        }

        binding = new Rc901aInputBinding(
            candidate.Kind,
            candidate.Code,
            target,
            Rc901aBindingSource.Learned);
        Status = Status with { Phase = Rc901aLearningPhase.Saving };
        return true;
    }

    public bool CompleteSave(string? sessionId)
    {
        if (!CanCompleteSave(sessionId))
        {
            return false;
        }

        Reset();
        return true;
    }

    public bool CanCompleteSave(string? sessionId) =>
        Matches(sessionId) &&
        Status.Phase == Rc901aLearningPhase.Saving;

    private bool Matches(string? sessionId) =>
        IsActive &&
        !string.IsNullOrWhiteSpace(sessionId) &&
        string.Equals(
            Status.SessionId,
            sessionId,
            StringComparison.Ordinal);

    private static bool IsLearnable(
        ControllerControl target,
        bool allowVerifiedOverride) =>
        Enum.IsDefined(target) &&
        target.ToString().StartsWith("Remote", StringComparison.Ordinal) &&
        (allowVerifiedOverride ||
         Rc901aInputBindings.VerifiedDefaults.All(item =>
             item.Control != target));

    private void Reset()
    {
        _acceptInputsAfter = DateTimeOffset.MinValue;
        _allowVerifiedOverride = false;
        Status = Rc901aLearningStatus.Idle;
    }
}
