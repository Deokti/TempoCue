namespace TempoCue.Application;

/// <summary>Публикует только подтверждённые изменения видимости участников.</summary>
public interface IParticipantVisibilityTransitionSource
{
    event EventHandler<ParticipantVisibilityTransition>? TransitionAvailable;
}
