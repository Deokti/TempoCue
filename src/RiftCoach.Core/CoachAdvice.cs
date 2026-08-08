namespace RiftCoach.Core;

public sealed record CoachAdvice(
    string RuleId,
    string Title,
    string Message,
    PowerState PowerState,
    int Priority,
    string Source);
