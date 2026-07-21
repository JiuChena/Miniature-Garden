using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Profile {
	public class Timer {
		public long Diff => NanoTime() - _Start;
		public float DiffMs => (float)(Diff + _Tracked) / 1000000;
		protected long DiffTimer => (Diff + _Tracked) / 100000;

		private float _MaxExecTime = 50;

		public float MaxExecTime
		{
			get => _MaxExecTime / 10;
			set => _MaxExecTime = value * 10;
		}

		private long _Tracked;
		private int _Frame;

		public bool LazyMode;

#if UNITY_EDITOR
		public bool IsTimeoutRequired => (LazyMode || Application.isPlaying) && DiffTimer > _MaxExecTime;
#else
		public bool IsTimeoutRequired => DiffTimer > _MaxExecTime;
#endif

		private long _Start;

		public async Task Wait() {
			Stop();
			await Task.Yield();
			Start();
		}

		public void Start(float maxExecTime) {
			MaxExecTime = maxExecTime;

			Start();
		}

		public void Start() {
			if (_Frame != Time.frameCount) {
				_Tracked = 0;
				_Frame = Time.frameCount;
			}

			_Start = NanoTime();
		}

		public void Stop() {
			_Frame = Time.frameCount;
			TrackTime();
		}

		protected void TrackTime() {
			var diff = Diff;

			TrackProfile(diff);
			_Tracked += diff;
			_Start += diff;
		}

		protected virtual void TrackProfile(long diff) => ProfilerModule.BakeTime.Value += diff;

		public static int MS() => (int)(NanoTime() / 1000000);

		public static long NanoTime() {
			var nano = 10000L * Stopwatch.GetTimestamp();
			nano /= TimeSpan.TicksPerMillisecond;
			nano *= 100L;
			return nano;
		}
	}
}