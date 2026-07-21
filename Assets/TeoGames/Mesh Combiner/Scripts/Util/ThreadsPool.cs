using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Util {
	internal class Queue {
		private readonly Func<bool> _Validate;
		private readonly Func<Task> _Action;
		private readonly TaskCompletionSource<bool> _Task;

		public static bool IsValid(Queue row) => row._Validate();

		public Queue(Func<bool> validate, Func<Task> action, TaskCompletionSource<bool> task) {
			_Validate = validate;
			_Action = action;
			_Task = task;
		}

		public async Task Execute() {
			try {
				if (_Action != null) await _Action();
				_Task.SetResult(true);
			} catch (Exception e) {
				_Task.SetException(e);
			}
		}
	}

	public class ThreadsPool {
		public static bool RunAlways() => true;

		public static Func<bool> RunWithDelay(float sec) {
			var end = Time.time + sec;

			return () => Time.time >= end;
		}

		private readonly List<Queue> _Queue = new List<Queue>();

		public bool HasTasks => _Queue.Any();

		public async Task<bool> Schedule(Func<bool> validate, Func<Task> action) {
			var promise = new TaskCompletionSource<bool>();
			var hadTasks = HasTasks;

			_Queue.Add(new Queue(validate, action, promise));
			if (!hadTasks) {
				await Task.Yield();
				RunNext().Forget();
			}

			return await promise.Task;
		}

		private async Task RunNext() {
			while (true) {
				var next = _Queue.FirstOrDefault(Queue.IsValid);
				if (next == null) {
					if (_Queue.Any()) {
						await Task.Yield();
						continue;
					}

					return;
				}

				await next.Execute();
				_Queue.Remove(next);
			}
		}
	}
}