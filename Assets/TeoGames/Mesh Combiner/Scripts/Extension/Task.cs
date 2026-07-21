using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Extension {
    public static class TaskExtension {
        public static async Task WaitUntil(this Task task, Func<bool> action) {
            await task;

            while (!action()) {
                await Task.Yield();
            }
        }

        public static IEnumerator ToCoroutine(this Task task) {
            while (!task.IsCompleted) {
                yield return null;
            }

            if (task.Exception != null) throw task.Exception;
        }

        public static async Task WaitUntil(this Task task, float timeoutSec, Func<bool> action) {
            await task;

            await Task.CompletedTask.WaitUntil(() => action() || (timeoutSec -= Time.deltaTime) < 0);
        }

        public static async Task WaitForUpdate(this Task task) {
            if (task.Status < TaskStatus.RanToCompletion) await task;

            await Task.Yield();
        }

        public static Task WaitForSeconds(this Task task, float seconds) {
            return task.ContinueWith(() => Task.Delay(Mathf.CeilToInt(seconds * 1000)));
        }

        public static Task ContinueWith(this Task task, Action action) {
            return task.ContinueWith(
                t => {
                    if (t.Exception != null) throw t.Exception;

                    action();

                    return Task.CompletedTask;
                },
                TaskScheduler.FromCurrentSynchronizationContext()
            );
        }

        public static async Task ContinueWith(this Task task, Func<Task> action) {
            await task.ContinueWith(
                t => {
                    if (t.Exception != null) throw t.Exception;

                    return Task.CompletedTask;
                },
                TaskScheduler.FromCurrentSynchronizationContext()
            );

            var res = action();
            if (res != null) await res;
        }

        public static void Catch(this Task task, Action<Exception> action) {
            task.ContinueWith(
                t => {
                    if (t.Exception != null) action(t.Exception);

                    return Task.CompletedTask;
                },
                TaskScheduler.FromCurrentSynchronizationContext()
            ).Forget();
        }

        public static void Catch(this Task task) => task.Catch(Debug.LogException);

        public static async Task CatchAnContinue(this Task task, Action<Exception> action) {
            await task.ContinueWith(
                t => {
                    if (t.Exception != null) action(t.Exception);

                    return Task.CompletedTask;
                },
                TaskScheduler.FromCurrentSynchronizationContext()
            );
        }

        public static Task CatchAnContinue(this Task task) => task.CatchAnContinue(Debug.LogException);

        /// <remarks>WARNING: All errors won't appear in console. Better to use `.catch()`</remarks>
        public static void Forget(this Task _) { }
    }
}