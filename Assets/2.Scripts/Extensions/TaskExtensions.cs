using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Extension methods for System.Threading.Tasks.Task.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Fire-and-forget a Task while ensuring any unhandled exception is logged
    /// instead of being silently swallowed (the default async void behavior).
    ///
    /// Usage: SomeAsyncMethod().Forget("context label");
    ///
    /// OperationCanceledException is intentionally ignored — callers that cancel
    /// via CancellationToken already log cancellation themselves.
    /// </summary>
    public static void Forget(this Task task, string context = "")
    {
        if (task == null) return;

        task.ContinueWith(
            t =>
            {
                // Unwrap AggregateException → get the real inner exception
                var ex = t.Exception?.InnerException ?? t.Exception;

                // Cancellation is a normal control-flow signal — not an error
                if (ex is System.OperationCanceledException) return;

                var label = string.IsNullOrEmpty(context) ? "Forget" : context;
                Debug.LogError($"[{label}] Unhandled exception:\n{ex}");
            },
            System.Threading.CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.FromCurrentSynchronizationContext()
        );
    }
}
