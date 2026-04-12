using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 轨迹跟随式回放：根据位置快照施加移动力，跳跃由事件触发。
/// 保留物理碰撞，偏移超限则销毁。
/// </summary>
public class TraceCloneFollowPath : MonoBehaviour
{
    [Header("跟随参数")]
    public float followForce = 12f;            // 朝向目标点的加速度系数
    public float maxFollowSpeed = 10f;         // 最大跟随速度
    public float stoppingDistance = 0.15f;     // 到达目标点的判定距离
    public float pathLookAheadTime = 0.25f;    // 前瞻时间（秒）

    [Header("偏移阈值")]
    public float maxAllowedDrift = 2.5f;        // 超过此距离立即销毁分身

    // 外部注入数据
    private List<TraceCloneManager.InputEvent> events;
    private List<TraceCloneManager.StateSnapshot> snapshots;
    private PlayerController controller;
    private CharacterController charController;

    // 回放状态
    private float playbackTime;
    private int currentSnapshotIndex;
    private int currentEventIndex;
    private bool isActive = true;

    // 静态引用计数，用于管理 Physics.simulationMode
    private static int activeFollowCount = 0;

    public void Initialize(
        List<TraceCloneManager.InputEvent> events,
        List<TraceCloneManager.StateSnapshot> snapshots,
        PlayerController controller)
    {
        this.events = events;
        this.snapshots = snapshots;
        this.controller = controller;
        this.charController = controller.Controller;

        playbackTime = 0f;
        currentSnapshotIndex = 0;
        currentEventIndex = 0;
        isActive = true;

        // 配置 PlayerController：使用外部输入但给予零移动输入，禁用其内部移动逻辑
        controller.useExternalInput = true;
        controller.SetExternalInput(Vector2.zero, false, false);
        controller.canMove = true;          // 允许跳跃和重力
        controller.freezeGravity = false;
        controller.useFixedUpdateMode = true;

        // 初始对齐
        if (snapshots.Count > 0)
        {
            transform.position = snapshots[0].position;
            transform.rotation = snapshots[0].rotation;
            Physics.SyncTransforms();
        }
    }

    void Awake()
    {
        if (activeFollowCount == 0)
        {
            Physics.simulationMode = SimulationMode.Script;
            Debug.Log("[TraceFollow] Physics switched to Script mode.");
        }
        activeFollowCount++;
    }

    void FixedUpdate()
    {
        if (!isActive) return;
        if (controller == null || charController == null) return;

        float dt = Time.fixedDeltaTime;
        playbackTime += dt;

        // 1. 处理跳跃事件
        ProcessJumpEvents(playbackTime);

        // 2. 根据快照施加移动力（空中同样适用）
        ApplyPathFollowing(playbackTime, dt);

        // 3. 手动推进物理引擎
        Physics.Simulate(dt);

        // 4. 检查偏移，超出阈值则销毁
        CheckDriftAndDestroy(playbackTime);

        // 5. 完成检测
        if (currentSnapshotIndex >= snapshots.Count - 1)
        {
            float distToEnd = Vector3.Distance(transform.position, snapshots[snapshots.Count - 1].position);
            if (distToEnd < 0.5f)
            {
                Debug.Log("[TraceFollow] Replay finished.");
                Destroy(gameObject);
            }
        }
    }

    private void ProcessJumpEvents(float currentTime)
    {
        while (currentEventIndex < events.Count && events[currentEventIndex].time <= currentTime)
        {
            var evt = events[currentEventIndex];
            if (evt.type == TraceCloneManager.InputEventType.JumpPressed)
            {
                controller.TriggerJumpBuffer();
            }
            currentEventIndex++;
        }
    }

    private void ApplyPathFollowing(float currentTime, float dt)
    {
        // 找到当前时间对应的快照区间
        while (currentSnapshotIndex < snapshots.Count - 2 &&
               snapshots[currentSnapshotIndex + 1].time <= currentTime)
        {
            currentSnapshotIndex++;
        }

        if (currentSnapshotIndex >= snapshots.Count - 1)
            return;

        var snapA = snapshots[currentSnapshotIndex];
        var snapB = snapshots[currentSnapshotIndex + 1];

        // 计算插值因子
        float t = Mathf.InverseLerp(snapA.time, snapB.time, currentTime);
        Vector3 interpolatedPos = Vector3.Lerp(snapA.position, snapB.position, t);

        // 前瞻点
        float lookAheadT = Mathf.Clamp01(t + pathLookAheadTime / (snapB.time - snapA.time));
        Vector3 targetPos = Vector3.Lerp(snapA.position, snapB.position, lookAheadT);

        Vector3 toTarget = targetPos - transform.position;
        float distance = toTarget.magnitude;

        if (distance > stoppingDistance)
        {
            Vector3 desiredVelocity = toTarget.normalized * Mathf.Min(maxFollowSpeed, distance * followForce);
            // 空中/地面统一施加外部速度
            controller.AddExternalVelocity(desiredVelocity, 5f * dt);
        }

        // 旋转朝向移动方向
        if (toTarget.magnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * dt);
        }
    }

    private void CheckDriftAndDestroy(float currentTime)
    {
        int idx = 0;
        for (int i = 0; i < snapshots.Count - 1; i++)
        {
            if (snapshots[i + 1].time > currentTime)
            {
                idx = i;
                break;
            }
        }
        if (idx >= snapshots.Count) idx = snapshots.Count - 1;

        float drift = Vector3.Distance(transform.position, snapshots[idx].position);
        if (drift > maxAllowedDrift)
        {
            Debug.LogWarning($"[TraceFollow] Drift {drift:F2} > {maxAllowedDrift}, destroying clone.");
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        isActive = false;
        activeFollowCount--;
        if (activeFollowCount == 0)
        {
            Physics.simulationMode = SimulationMode.FixedUpdate;
            Debug.Log("[TraceFollow] Physics restored to FixedUpdate mode.");
        }
    }
}