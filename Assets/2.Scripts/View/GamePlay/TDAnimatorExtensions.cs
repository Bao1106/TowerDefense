using UnityEngine;

public static class TDAnimatorExtensions
{
    // A controller without the trigger (Slime has no Walk) would log a warning on every SetTrigger.
    public static bool HasTrigger(this Animator animator, string triggerName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        var parameters = animator.parameters; // allocates: call it on events, never per frame
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger
                && parameters[i].name == triggerName) return true;
        }
        return false;
    }
}
