using UnityEngine;
using System.Collections.Generic;

public class TraceCloneReplay : MonoBehaviour
{
    private List<TraceCloneManager.TickRecord> records;
    private PlayerController controller;
    private int tickIndex;
    private bool finished;
    private bool initialized;
    private int postFinishTicks;
    private PlayerInteraction interaction;

    private bool wasGrounded = true;
    private float footstepTimer = 0f;
    private const float FOOTSTEP_INTERVAL = 0.4f;

    public void Initialize(
        List<TraceCloneManager.TickRecord> records,
        PlayerController controller,
        PlayerInteraction interaction)
    {
        this.records = records;
        this.controller = controller;
        tickIndex = 0;
        finished = false;
        initialized = false;
        postFinishTicks = 0;
        this.interaction = interaction;
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

            if (interaction != null)
            {
                if (rec.activatePressed)
                    interaction.RequestInteract();

                if (rec.activateReleased)
                    interaction.RequestReleaseInteract();
            }

            if (rec.jumpTrigger)
                controller.TriggerJumpBuffer();

            tickIndex++;
        }
        else
        {
            controller.SetExternalInput(Vector2.zero, false, false);

            if (interaction != null)
                interaction.RequestReleaseInteract();

            if (!controller.IsAirborne)
            {
                postFinishTicks++;
                if (postFinishTicks > 10)
                {
                    finished = true;
                    controller.canMove = false;
                    Destroy(gameObject, 0.5f);
                }
            }
            else
            {
                postFinishTicks = 0;
            }
        }

        HandleTraceCloneAudio();
    }

    private void HandleTraceCloneAudio()
    {
        if (AudioManager.Instance == null) return;

        bool isGrounded = controller.Controller.isGrounded;
        float speed = controller.GetCurrentHorizontalSpeed();

        if (!wasGrounded && isGrounded)
        {
            AudioManager.Instance.PlayTraceCloneLanding(
                transform.position,
                controller.GetCurrentVerticalSpeed()
            );
        }

        if (isGrounded && speed > 0.1f)
        {
            footstepTimer -= Time.fixedDeltaTime;
            if (footstepTimer <= 0f)
            {
                AudioManager.Instance.PlayTraceCloneFootstep(transform.position);
                footstepTimer = FOOTSTEP_INTERVAL;
            }
        }

        wasGrounded = isGrounded;
    }
}