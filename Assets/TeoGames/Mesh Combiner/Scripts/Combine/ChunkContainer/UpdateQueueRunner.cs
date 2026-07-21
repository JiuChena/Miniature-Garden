using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using TeoGames.Mesh_Combiner.Scripts.Profile;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Combine.ChunkContainer {
	using Queue = UpdateQueue<AbstractCombinable, UpdateQueueItem>;

	public interface RunnableUpdateQueue {
#if DEBUG_BAKING
		int ID { get; }
#endif

		bool IsActive { get; }

		void Progress();

		bool HaveUpdates { get; }
		Task UpdateNew();

		bool HaveDynamic { get; }
		Task UpdateDynamic();
	}

	public class UpdateQueueRunner {
		public static Timer UpdateTimer = new UpdateQueueTimer();
		public static Timer InitTimer = new UpdateQueueTimer();

		private static readonly List<RunnableUpdateQueue> list = new List<RunnableUpdateQueue>();
		private static readonly List<RunnableUpdateQueue> processing = new List<RunnableUpdateQueue>();

		public static void Run(RunnableUpdateQueue instance) {
#if DEBUG_BAKING
			Debug.LogError($">> QUEUE {instance.ID} >> Register processing...");
#endif

			if (list.Contains(instance)) return;

			list.Add(instance);
			if (list.Count == 1) UpdateList().Forget();

		}

		public static void Stop(RunnableUpdateQueue instance) {
#if DEBUG_BAKING
			Debug.LogError($">> QUEUE {instance.ID} >> Stop and remove from list");
#endif
			list.Remove(instance);
		}

		private static async Task UpdateList() {
			UpdateTimer.MaxExecTime = 5f;
			InitTimer.MaxExecTime = 1.5f;

			var initialState = Application.isPlaying;
			await Task.Yield();

			int cnt;
			while ((cnt = list.Count) > 0 && initialState == Application.isPlaying) {
				processing.Clear();
				processing.AddRange(list);

				for (var i = 0; i < cnt; i++) {
					var queue = processing[i];

					try {
						if (queue is { IsActive: true }) {
							if (queue.HaveUpdates) await queue.UpdateNew();
							if (queue.HaveDynamic) await queue.UpdateDynamic();
							queue.Progress();
						} else Stop(queue);
					} catch (Exception ex) {
						Debug.LogException(ex);
					}
				}

				var execTime = Mathf.CeilToInt(UpdateTimer.DiffMs + InitTimer.DiffMs);
				await Task.Delay(Mathf.Max(100, 200 - execTime));
			}
		}

#if UNITY_EDITOR
		[InitializeOnEnterPlayMode, InitializeOnLoadMethod]
		private static void OnEnterPlaymodeInEditor() {
			UpdateTimer = new UpdateQueueTimer();
			InitTimer = new UpdateQueueTimer();
			list.Clear();
			processing.Clear();
		}
#endif
	}
}