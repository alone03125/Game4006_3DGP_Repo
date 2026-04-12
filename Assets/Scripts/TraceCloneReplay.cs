using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 事件驱动 + FixedUpdate 确定性回放。
/// 
/// 工作原理：
/// 1. 在 FixedUpdate 中推进 playbackTime（使用 fixedDeltaTime）
/// 2. 每一 tick 消费所有 time <= playbackTime 的 InputEvent，
///    将最新的 moveInput / jump / sprint / cameraYaw 喂给 PlayerController
/// 3. 定期与 StateSnapshot 做空间修正（混合式回放）
/// </summary>
public class TraceCloneReplay : MonoBehaviour
{
    // 由 TraceCloneManager.Initialize() 设置
    private List<TraceCloneManager.InputEvent> events;
    private List<TraceCloneManager.StateSnapshot> snapshots;
    private PlayerController controller;
    private CharacterController charController;

    // 空间修正参数
    private float correctionTolerance;
    private float correctionBreakThreshold;
    private float jumpLookaheadWindow;
    private float jumpLookbackWindow;

    // 回放状态
    private float playbackTime;
    private int eventIndex;
    private int snapshotIndex;
    private bool finished;
    private bool initialized;

    // 当前应用的输入状态
    private Vector2 currentMove;
    private bool currentJump;
    private bool currentSprint;
    private float currentYaw;

    // 跳跃边沿检测
    private bool prevJumpState;

    // 空间修正状态
    private bool correctionBroken = false;

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
        prevJumpState = false;
        correctionBroken = false;
    }

    void Start()
    {
        // 延迟一帧获取 CharacterController，确保 PlayerController.Start() 已执行
        StartCoroutine(DelayedInit());
    }

    private System.Collections.IEnumerator DelayedInit()
    {
        yield return null;
        if (controller != null)
        {
            charController = controller.Controller;
            if (charController == null)
                Debug.LogError("[TraceReplay] CharacterController is null after delayed init");
        }
        initialized = true;
    }

    void FixedUpdate()
    {
        if (!initialized || finished) return;
        if (controller == null || charController == null) return;

        float dt = Time.fixedDeltaTime;
        playbackTime += dt;

        // --- 1. 消费事件 ---
        bool jumpTriggered = false;
        while (eventIndex < events.Count && events[eventIndex].time <= playbackTime)
        {
            var evt = events[eventIndex];
            switch (evt.type)
            {
                case TraceCloneManager.InputEventType.MoveChanged:
                    currentMove = evt.moveValue;
                    break;

                case TraceCloneManager.InputEventType.JumpPressed:
                    if (!prevJumpState)
                    {
                        jumpTriggered = true;
                    }
                    currentJump = true;
                    break;

                case TraceCloneManager.InputEventType.JumpReleased:
                    currentJump = false;
                    break;

                case TraceCloneManager.InputEventType.SprintChanged:
                    currentSprint = evt.sprintState;
                    break;
            }

            // 每个事件都更新 yaw 和 sprint
            currentYaw = evt.cameraYaw;
            currentSprint = evt.sprintState;

            eventIndex++;
        }

        // --- 2. 应用输入到 PlayerController ---
        controller.SetExternalInput(currentMove, currentJump, currentSprint);
        controller.overrideCameraYaw = currentYaw;

        // 如果本 tick 有跳跃事件，直接触发跳跃缓冲
        if (jumpTriggered)
        {
            controller.TriggerJumpBuffer();
        }

        // --- 3. 空间修正（混合式回放）---
        if (!correctionBroken)
        {
            ApplySpatialCorrection();
        }

        // --- 4. 跳跃前瞻/回溯：落地瞬间检查附近是否有跳跃事件 ---
        if (charController.isGrounded && !controller.IsAirborne)
        {
            CheckJumpLookahead();
        }

        // --- 5. 检查结束 ---
        if (eventIndex >= events.Count && !controller.IsAirborne)
        {
            // 所有事件已消费且已着地
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
    /// 对比最近的状态快照进行空间修正
    /// </summary>
    private void ApplySpatialCorrection()
    {
        // 找到当前时间对应的快照
        while (snapshotIndex < snapshots.Count - 1 && snapshots[snapshotIndex + 1].time <= playbackTime)
        {
            snapshotIndex++;
        }

        if (snapshotIndex >= snapshots.Count) return;

        var snap = snapshots[snapshotIndex];
        // 只在快照时间与当前时间接近时校验（±1个fixedDeltaTime）
        float timeDiff = Mathf.Abs(playbackTime - snap.time);
        if (timeDiff > Time.fixedDeltaTime * 1.5f) return;

        Vector3 actualPos = transform.position;
        Vector3 expectedPos = snap.position;
        Vector3 offset = expectedPos - actualPos;

        // Y轴修正策略：只在both grounded或both airborne时修正Y
        bool actualGrounded = charController.isGrounded;
        bool snapGrounded = snap.grounded;
        if (actualGrounded != snapGrounded)
        {
            offset.y = 0f; // 接地状态不同时忽略Y修正
        }

        float dist = offset.magnitude;

        if (dist > correctionBreakThreshold)
        {
            correctionBroken = true;
            Debug.LogWarning($"[TraceReplay] Spatial correction broken: drift={dist:F3} > threshold={correctionBreakThreshold}");
            return;
        }

        if (dist > 0.01f && dist <= correctionTolerance)
        {
            // 直接修正到快照位置
            charController.enabled = false;
            transform.position = actualPos + offset;
            charController.enabled = true;
        }
    }

    /// <summary>
    /// 落地前瞻：检查未来一小段时间内是否有跳跃事件，提前触发跳跃缓冲
    /// </summary>
    private void CheckJumpLookahead()
    {
        // 向前搜索
        for (int i = eventIndex; i < events.Count; i++)
        {
            float futureTime = events[i].time - playbackTime;
            if (futureTime > jumpLookaheadWindow) break;
            if (futureTime >= 0 && events[i].type == TraceCloneManager.InputEventType.JumpPressed)
            {
                controller.TriggerJumpBuffer();
                return;
            }
        }

        // 向后搜索（可能刚错过的跳跃事件）
        for (int i = eventIndex - 1; i >= 0; i--)
        {
            float pastTime = playbackTime - events[i].time;
            if (pastTime > jumpLookbackWindow) break;
            if (events[i].type == TraceCloneManager.InputEventType.JumpPressed)
            {
                controller.TriggerJumpBuffer();
                return;
            }
        }
    }
}