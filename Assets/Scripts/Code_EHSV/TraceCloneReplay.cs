using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 确定性事件驱动回放 + 平滑空间修正（施力而非瞬移）
/// </summary>
public class TraceCloneReplay : MonoBehaviour
{
    // === 静态引用计数，用于管理 Physics.simulationMode ===
    private static int activeReplayCount = 0;

    [Header("空间修正力参数")]
    public float correctionForceGain = 8f;          // 比例增益（位置误差→速度）
    public float maxCorrectionSpeed = 4f;           // 最大修正速度
    public float correctionVelocityDamping = 10f;   // 每帧衰减系数

    private List<TraceCloneManager.InputEvent> events;
    private List<TraceCloneManager.StateSnapshot> snapshots;
    private PlayerController controller;
    private CharacterController charController;

    private float correctionTolerance;
    private float correctionBreakThreshold;
    private float jumpLookaheadWindow;
    private float jumpLookbackWindow;

    private float playbackTime;
    private int eventIndex;
    private int snapshotIndex;
    private bool finished;
    private bool initialized;

    // 当前插值后的输入状态
    private Vector2 currentMove;
    private bool currentJump;
    private bool currentSprint;
    private float currentYaw;

    private bool correctionBroken = false;

    // 用于跳跃状态查询的缓存索引（优化性能）
    private int lastJumpStateQueryIndex = 0;

    public void Initialize(
        List<TraceCloneManager.InputEvent> events,
        List<TraceCloneManager.StateSnapshot> snapshots,
        PlayerController controller,
        float correctionTolerance,
        float correctionBreakThreshold,
        float jumpLookaheadWindow,
        float jumpLookbackWindow)
    {
        this.events = events;
        this.snapshots = snapshots;
        this.controller = controller;
        this.correctionTolerance = correctionTolerance;
        this.correctionBreakThreshold = correctionBreakThreshold;
        this.jumpLookaheadWindow = jumpLookaheadWindow;
        this.jumpLookbackWindow = jumpLookbackWindow;

        playbackTime = 0f;
        eventIndex = 0;
        snapshotIndex = 0;
        finished = false;
        initialized = false;

        currentMove = Vector2.zero;
        currentJump = false;
        currentSprint = false;
        currentYaw = transform.eulerAngles.y;
        correctionBroken = false;
    }

    void Awake()
    {
        if (activeReplayCount == 0)
        {
            Physics.simulationMode = SimulationMode.Script;
            Debug.Log("[TraceReplay] Physics switched to Script mode.");
        }
        activeReplayCount++;
    }

    void Start()
    {
        StartCoroutine(DelayedInit());
    }

    private System.Collections.IEnumerator DelayedInit()
    {
        yield return null;
        if (controller != null)
        {
            charController = controller.Controller;
            if (charController == null)
                Debug.LogError("[TraceReplay] CharacterController is null");
        }
        initialized = true;
    }

    void FixedUpdate()
    {
        if (!initialized || finished) return;
        if (controller == null || charController == null) return;

        float dt = Time.fixedDeltaTime;

        // 1. 消费事件并插值当前输入（跳跃状态处理修复在此）
        ConsumeInputEvents(playbackTime);

        // 2. 将输入喂给 PlayerController
        controller.SetExternalInput(currentMove, currentJump, currentSprint);
        controller.overrideCameraYaw = currentYaw;

        // 3. 手动模拟物理（确保 simulationMode 为 Script）
        Physics.Simulate(dt);

        // 4. 平滑空间修正（施加力，而非瞬移）
        if (!correctionBroken)
            ApplySmoothCorrection(playbackTime, dt);

        // 5. 推进回放时间
        playbackTime += dt;

        // 6. 结束检测
        if (eventIndex >= events.Count && !controller.IsAirborne)
        {
            float lastEventTime = events.Count > 0 ? events[events.Count - 1].time : 0f;
            if (playbackTime > lastEventTime + 0.5f)
            {
                finished = true;
                controller.canMove = false;
                Debug.Log($"[TraceReplay] Replay finished at t={playbackTime:F3}s");
            }
        }
    }

    /// <summary>
    /// 消费输入事件：插值移动/视角，并确定当前时刻的跳跃状态（连续跳跃修复）
    /// </summary>
    private void ConsumeInputEvents(float currentTime)
    {
        // 将事件索引推进到当前时间所在区间
        while (eventIndex < events.Count - 1 && events[eventIndex + 1].time <= currentTime)
            eventIndex++;

        if (eventIndex >= events.Count) return;

        var currentEvent = events[eventIndex];
        var nextEvent = (eventIndex + 1 < events.Count) ? events[eventIndex + 1] : currentEvent;

        // 计算插值因子
        float interval = nextEvent.time - currentEvent.time;
        float t = (interval > 0.0001f) ? Mathf.Clamp01((currentTime - currentEvent.time) / interval) : 0f;

        // 插值移动输入
        currentMove = Vector2.Lerp(currentEvent.moveValue, nextEvent.moveValue, t);
        // 插值摄像机偏航
        currentYaw = Mathf.LerpAngle(currentEvent.cameraYaw, nextEvent.cameraYaw, t);
        // 冲刺状态取当前事件的值（不插值）
        currentSprint = currentEvent.sprintState;

        // === 修复跳跃：根据时间查询当前是否处于跳跃按下状态（连续跳跃生效） ===
        currentJump = GetJumpStateAtTime(currentTime);

        // 落地时的跳跃前瞻/回溯（可选，增强容错）
        if (charController != null && charController.isGrounded && !controller.IsAirborne)
        {
            CheckJumpLookaround(currentTime);
        }
    }

    /// <summary>
    /// 查询指定时刻的跳跃按下状态（true=按下，false=未按下）
    /// 通过查找最近的 JumpPressed / JumpReleased 事件确定
    /// </summary>
    private bool GetJumpStateAtTime(float time)
    {
        // 从缓存索引开始搜索，提高效率
        int startIdx = Mathf.Max(0, lastJumpStateQueryIndex - 5);
        for (int i = startIdx; i < events.Count; i++)
        {
            if (events[i].time > time)
            {
                // 超过当前时间，向前找最后一个相关事件
                for (int j = i - 1; j >= 0; j--)
                {
                    if (events[j].type == TraceCloneManager.InputEventType.JumpPressed)
                    {
                        lastJumpStateQueryIndex = j;
                        return true;
                    }
                    if (events[j].type == TraceCloneManager.InputEventType.JumpReleased)
                    {
                        lastJumpStateQueryIndex = j;
                        return false;
                    }
                }
                // 没有找到任何跳跃事件，默认为 false
                return false;
            }
        }
        // 所有事件时间均小于等于当前时间，取最后一个事件的状态
        for (int i = events.Count - 1; i >= 0; i--)
        {
            if (events[i].type == TraceCloneManager.InputEventType.JumpPressed)
            {
                lastJumpStateQueryIndex = i;
                return true;
            }
            if (events[i].type == TraceCloneManager.InputEventType.JumpReleased)
            {
                lastJumpStateQueryIndex = i;
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// 落地时检查附近是否有跳跃事件，触发跳跃缓冲（辅助确定性）
    /// </summary>
    private void CheckJumpLookaround(float currentTime)
    {
        // 前瞻
        for (int i = eventIndex; i < events.Count; i++)
        {
            float delta = events[i].time - currentTime;
            if (delta > jumpLookaheadWindow) break;
            if (delta >= 0 && events[i].type == TraceCloneManager.InputEventType.JumpPressed)
            {
                controller.TriggerJumpBuffer();
                return;
            }
        }
        // 回溯
        for (int i = eventIndex; i >= 0; i--)
        {
            float delta = currentTime - events[i].time;
            if (delta > jumpLookbackWindow) break;
            if (events[i].type == TraceCloneManager.InputEventType.JumpPressed)
            {
                controller.TriggerJumpBuffer();
                return;
            }
        }
    }

    /// <summary>
    /// 平滑空间修正：施加力而非瞬移
    /// </summary>
    private void ApplySmoothCorrection(float currentTime, float dt)
    {
        // 推进快照索引
        while (snapshotIndex < snapshots.Count - 1 && snapshots[snapshotIndex + 1].time <= currentTime)
            snapshotIndex++;

        if (snapshotIndex >= snapshots.Count) return;

        var snap = snapshots[snapshotIndex];
        float timeDiff = Mathf.Abs(currentTime - snap.time);
        if (timeDiff > Time.fixedDeltaTime * 1.5f) return;

        Vector3 error = snap.position - transform.position;
        // 如果接地状态不一致，忽略垂直修正
        if (charController.isGrounded != snap.grounded)
            error.y = 0f;

        float dist = error.magnitude;

        if (dist > correctionBreakThreshold)
        {
            correctionBroken = true;
            Debug.LogWarning($"[TraceReplay] Correction broken: drift {dist:F3} > {correctionBreakThreshold}");
        }
        else if (dist > correctionTolerance)
        {
            // 计算修正速度：比例控制 + 限幅
            Vector3 desiredCorrectionVel = error.normalized * Mathf.Min(dist * correctionForceGain, maxCorrectionSpeed);
            // 通过 PlayerController 施加外部速度
            controller.AddExternalVelocity(desiredCorrectionVel, correctionVelocityDamping * dt);
        }
    }

    void OnDestroy()
    {
        activeReplayCount--;
        if (activeReplayCount == 0)
        {
            Physics.simulationMode = SimulationMode.FixedUpdate;
            Debug.Log("[TraceReplay] Physics restored to FixedUpdate mode.");
        }
    }
}