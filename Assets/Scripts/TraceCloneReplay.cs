using UnityEngine;
using System.Collections.Generic;

public class TraceCloneReplay : MonoBehaviour
{
    private List<TraceCloneManager.RecordedFrame> frames;
    private float fixedDeltaTime;
    private PlayerController controller;
    private float playbackTime = 0f;
    private int currentFrame = 0;
    private bool isPlaying = false;

    public void Initialize(List<TraceCloneManager.RecordedFrame> recordedFrames, float deltaTime, PlayerController playerController)
    {
        frames = recordedFrames;
        fixedDeltaTime = deltaTime;
        controller = playerController;
        playbackTime = 0f;
        currentFrame = 0;
        isPlaying = true;
        controller.canMove = true;
        controller.useExternalInput = true;
    }

    private void Update()
    {
        if (!isPlaying) return;

        playbackTime += Time.deltaTime;

        // 更新到当前时间对应的输入帧
        while (currentFrame < frames.Count && frames[currentFrame].time <= playbackTime)
        {
            var frame = frames[currentFrame];
            controller.SetExternalInput(frame.moveInput, frame.jump, frame.sprint);
            currentFrame++;
        }

        // 播放完毕，停止移动并销毁
        if (currentFrame >= frames.Count)
        {
            isPlaying = false;
            controller.canMove = false;
            controller.useExternalInput = false;
            // 可选：延迟销毁以便观察
            Destroy(gameObject, 1f);
        }
    }
}