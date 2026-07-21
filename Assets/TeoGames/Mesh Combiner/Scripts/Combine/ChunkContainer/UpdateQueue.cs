using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeoGames.Mesh_Combiner.Scripts.Combine.Interfaces;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Combine.ChunkContainer {
	public enum UpdateQueueStatus {
		New,
		InProgress,
		Started,
	}

	[Serializable]
	public class UpdateQueue<T, I> : RunnableUpdateQueue, IAsyncCombiner
		where T : AbstractCombinable where I : UpdateQueueItem {
#if DEBUG_BAKING
		private static int GLOBAL_ID;
		public int ID { get; } = ++GLOBAL_ID;
#endif
		
		[Tooltip("Distance between last update position and current position to trigger container check")]
		public float updateDistance = .5f;

		[Tooltip("Max amount of ticks before forcing container check")]
		public int forceUpdateAfter = 100;

		[Tooltip("Processing of new meshes will be delayed for 3 ticks if enabled")]
		public bool delayedUpdate;

		private readonly Dictionary<T, I> _List = new Dictionary<T, I>();
		private readonly Dictionary<T, UpdateType> _Updates = new Dictionary<T, UpdateType>();
		private IChunkContainer<T, I> _Owner;

		public bool IsStarted { get; protected set; }
		public int UpdatesCount => _List.Count;
		public UpdateQueueStatus Status { get; protected set; }
		public bool IsActive => IsStarted && _Owner != null;
		public bool HaveUpdates { get; private set; }
		public bool HaveDynamic => UpdatesCount > 0;

		public void Clear() {
			_Updates.Clear();
			HaveUpdates = false;

			_List.Clear();

			Stop();
			Status = UpdateQueueStatus.New;
		}

		public void Start(IChunkContainer<T, I> owner) {
#if DEBUG_BAKING
			Debug.LogError($">> QUEUE {ID} >> Start");
#endif
			if (!IsStarted) Status = UpdateQueueStatus.New;
			_Owner = owner;
			IsStarted = true;
			UpdateQueueRunner.Run(this);
		}

		public void Stop() {
#if DEBUG_BAKING
			Debug.LogError($">> QUEUE {ID} >> Stop");
#endif
			IsStarted = false;
			UpdateQueueRunner.Stop(this);
		}

		public void Schedule(T combinable) {
			_Updates[combinable] = UpdateType.Include;
			combinable.UpdateVisibility();
			HaveUpdates = true;
		}

		public void Remove(T combinable) {
			_Updates[combinable] = UpdateType.Exclude;
			HaveUpdates = true;
		}

		public async Task UpdateNew() {
			Progress();
			var cpy = _Updates.CopyAndClear();
			HaveUpdates = false;

			if (delayedUpdate) {
				Task.CompletedTask
					.WaitForUpdate()
					.WaitForUpdate()
					.WaitForUpdate()
					.ContinueWith(ProcessNew)
					.Forget();
			} else {
				await ProcessNew();
			}

			return;

			async Task ProcessNew() {
				UpdateQueueRunner.InitTimer.Start();

				var i = 0;
				foreach (var pair in cpy) {
					if (i++ % 10 == 0 && UpdateQueueRunner.InitTimer.IsTimeoutRequired) {
						await UpdateQueueRunner.InitTimer.Wait();

						if (!IsStarted) return;
					}

					try {
						var action = pair.Value;
						var comb = pair.Key;
						if (action == UpdateType.Include && comb.IsActive) {
							var item = _Owner.IncludeNew(comb);
							if (comb.IsStatic) continue;

							item.transform = comb.transform;
							item.position = item.transform.position;
							_List[comb] = item;
						} else _List.Remove(comb);
					} catch (Exception ex) {
						Debug.LogException(ex);
					}
				}

				Status = UpdateQueueStatus.Started;
				UpdateQueueRunner.InitTimer.Stop();
			}
		}

		public void Progress() => Status = Status == UpdateQueueStatus.New ? UpdateQueueStatus.InProgress : Status;

		public async Task UpdateDynamic() {
			UpdateQueueRunner.UpdateTimer.Start();
			var i = 0;
			var cpy = new Dictionary<T, I>(_List);
			foreach (var pair in cpy) {
				if (i++ % 10 == 0 && UpdateQueueRunner.UpdateTimer.IsTimeoutRequired) {
					await UpdateQueueRunner.UpdateTimer.Wait();
					if (!IsActive) return;
				}

				ProcessCombinable(pair.Key, pair.Value);
			}

			UpdateQueueRunner.UpdateTimer.Stop();
		}

		private void ProcessCombinable(T combinable, I item) {
			if (!combinable || !combinable.gameObject.activeInHierarchy) {
				Remove(combinable);
				return;
			}

			item.ticks++;

			if (item.ticks < forceUpdateAfter) {
				if (item.ticks % 10 != 9) return;

				var pos = item.transform.position;
				if (Vector3.Distance(item.position, pos) < updateDistance) return;
				Update(_Owner, combinable, item, pos);
			} else {
				Update(_Owner, combinable, item, item.transform.position);
			}

			return;

			static void Update(IChunkContainer<T, I> owner, T combinable, I item, Vector3 pos) {
				try {
					owner.UpdateDynamic(combinable, item);
					item.ticks = 0;
					item.position = pos;
				} catch (Exception ex) {
					Debug.LogException(ex);
				}
			}
		}

		public Task UpdateTask => Task.CompletedTask.WaitUntil(() => Status == UpdateQueueStatus.Started);
	}
}