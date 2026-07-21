using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CoreFramework
{
    /// <summary>
    /// 定时器事件管理器，支持添加延迟后一次性触发的回调。
    /// </summary>
    public class TimerEventManager : MonoBehaviour
    {
        private static TimerEventManager instance;
        public static TimerEventManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject obj = new GameObject();
                    instance = obj.AddComponent<TimerEventManager>();
                    obj.name = "TimerEventManager";
                    DontDestroyOnLoad(obj);
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 待处理的定时事件队列。
        /// </summary>
        public readonly Queue<TimerEvent> actions = new Queue<TimerEvent>();

        /// <summary>
        /// 添加一个延迟触发的定时事件。
        /// </summary>
        /// <param name="timer">延迟时间（秒）</param>
        /// <param name="action">到期后执行的回调</param>
        public void AddTimerEvent(float timer, UnityAction action)
        {
            actions.Enqueue(new TimerEvent(timer, action));
        }

        private void Update()
        {
            int count = actions.Count;
            for (int i = 0; i < count; i++)
            {
                TimerEvent timerEvent = actions.Dequeue();
                timerEvent.timer -= Time.deltaTime;

                if (timerEvent.timer <= 0)
                    timerEvent.action();
                else
                    actions.Enqueue(timerEvent);
            }
        }
    }

    /// <summary>
    /// 定时事件数据。
    /// </summary>
    public class TimerEvent
    {
        /// <summary>
        /// 剩余倒计时（秒）。
        /// </summary>
        public float timer;

        /// <summary>
        /// 到期后执行的回调。
        /// </summary>
        public UnityAction action;

        public TimerEvent(float timer, UnityAction action)
        {
            this.timer = timer;
            this.action = action;
        }
    }
}