using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 逐Tick确定性回放：每个FixedUpdate读取一条TickRecord，
/// 将输入喂给PlayerController，实现与录制完全一致的操作复刻。
/// 无需空间修正、跳跃前瞻等复杂逻辑。
/// </summary>
public class TraceCloneReplay : MonoBehaviour
{
    private List<TraceCloneManager.TickRecord> records;
    private PlayerController controller;
    private int tickIndex;
    private bool finished;
    private bool initialized;
    private int postFinishTicks;

    public void Initialize(List<TraceCloneManager.TickRecord> records, PlayerController controller)
    {
        this.records = records;
        this.controller = controller;
        tickIndex = 0;
        finished = false;
        initialized = false;
        postFinishTicks = 0;
    }

    void Start()
    {
        StartCoroutine(DelayedInit());
    }

    private System.Collections.IEnumerator DelayedInit()
    {
        yield return new WaitForFixedUpdate();
        initialized = true;
    }

    void FixedUpdate()
    {
        if (!initialized || finished) return;
        if (controller == null) return;

        if (tickIndex < records.Count)
        {
            var rec = records[tickIndex];
            controller.SetExternalInput(rec.moveInput, false, rec.sprint);
            controller.overrideCameraYaw = rec.cameraYaw;

            if (rec.jumpTrigger)
                controller.TriggerJumpBuffer();

            tickIndex++;
        }
        else
        {
            // 所有Tick已消费，等待落地后销毁
            controller.SetExternalInput(Vector2.zero, false, false);

            if (!controller.IsAirborne)
            {
                postFinishTicks++;
                if (postFinishTicks > 10)
                {
                    finished = true;
                    controller.canMove = false;
                    Debug.Log($"[TraceReplay] Replay finished after {tickIndex} ticks");
                    Destroy(gameObject, 0.5f);
                }
            }
            else
            {
                postFinishTicks = 0;
            }
        }
    }
}
