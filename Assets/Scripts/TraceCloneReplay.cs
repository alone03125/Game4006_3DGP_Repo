using UnityEngine;
using System.Collections.Generic;

public class TraceCloneReplay : MonoBehaviour
{
    private List<TraceCloneManager.RecordedFrame> frames;
    private PlayerController controller;
    private float playbackTime = 0f;
    private int currentFrame = 0;
    private bool isPlaying = false;

    public int FrameCount => frames?.Count ?? 0;

    public void Initialize(List<TraceCloneManager.RecordedFrame> recordedFrames, PlayerController playerController)
    {
        frames = recordedFrames;
        controller = playerController;
        playbackTime = 0f;
        currentFrame = 0;
        isPlaying = true;
        controller.canMove = true;
        controller.useExternalInput = true;
        controller.useCameraOverride = true;

        Debug.Log($"[TraceCloneReplay] Initialized with {frames.Count} frames");
    }

    private void Update()
    {
        if (!isPlaying) return;

        playbackTime += Time.unscaledDeltaTime;

        while (currentFrame < frames.Count && frames[currentFrame].time <= playbackTime)
        {
            var frame = frames[currentFrame];
            controller.SetExternalInput(frame.moveInput, frame.jump, frame.sprint);
            controller.overrideCameraYaw = frame.cameraYaw;

            if (currentFrame % 50 == 0)
                Debug.Log($"[TraceCloneReplay] Frame {currentFrame}, t={frame.time:F2}, input={frame.moveInput}");

            currentFrame++;
        }

        if (currentFrame >= frames.Count)
        {
            isPlaying = false;
            controller.canMove = false;
            controller.useExternalInput = false;
            controller.useCameraOverride = false;
            Debug.Log("[TraceCloneReplay] Playback finished, destroying");
            Destroy(gameObject, 1f);
        }
    }
}