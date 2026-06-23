using UnityEngine;
using System.Collections.Generic;
using System;

namespace FORGE3D
{
    // Pulsewave 전용 타이머. 기존 F3DTime과 분리하여 충돌/오류 방지.
    public class ShieldFXTime : MonoBehaviour
    {
        public static ShieldFXTime time;

        List<Timer> timers;
        List<int> removalPending;

        private int idCounter;

        class Timer
        {
            public int id;
            public bool isActive;

            public float rate;
            public int ticks;
            public int ticksElapsed;
            public float last;
            public Action callBack;

            public Timer(int id_, float rate_, int ticks_, Action callback_)
            {
                id = id_;
                rate = rate_ < 0 ? 0 : rate_;
                ticks = ticks_ < 0 ? 0 : ticks_;
                callBack = callback_;
                last = 0;
                ticksElapsed = 0;
                isActive = true;
            }

            public void Tick()
            {
                last += Time.deltaTime;

                if (isActive && last >= rate)
                {
                    last = 0;
                    ticksElapsed++;
                    callBack.Invoke();

                    if (ticks > 0 && ticks == ticksElapsed)
                    {
                        isActive = false;
                        ShieldFXTime.time.RemoveTimer(id);
                    }
                }
            }
        }

        void Awake()
        {
            if (time != null && time != this)
            {
                Destroy(gameObject);
                return;
            }
            time = this;
            timers = new List<Timer>();
            removalPending = new List<int>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoCreate()
        {
            if (time != null) return;
            GameObject go = new GameObject("ShieldFXTime");
            go.AddComponent<ShieldFXTime>();
            DontDestroyOnLoad(go);
        }

        public int AddTimer(float rate, Action callBack)
        {
            return AddTimer(rate, 0, callBack);
        }

        public int AddTimer(float rate, int ticks, Action callBack)
        {
            Timer newTimer = new Timer(++idCounter, rate, ticks, callBack);
            timers.Add(newTimer);
            return newTimer.id;
        }

        public void RemoveTimer(int timerId)
        {
            removalPending.Add(timerId);
        }

        void Remove()
        {
            if (removalPending.Count > 0)
            {
                foreach (int id in removalPending)
                    for (int i = 0; i < timers.Count; i++)
                        if (timers[i].id == id)
                        {
                            timers.RemoveAt(i);
                            break;
                        }

                removalPending.Clear();
            }
        }

        void Tick()
        {
            for (int i = 0; i < timers.Count; i++)
                timers[i].Tick();
        }

        void Update()
        {
            Remove();
            Tick();
        }
    }
}
