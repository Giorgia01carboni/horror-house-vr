using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Animator;

public class PlayerMotor : MonoBehaviour
{
    private CharacterController controller;
    private Vector3 playerVelocity;
    private Animator animator;
    private PlayerAudio playerAudio;
    private bool isGrounded;
    private bool sprinting = false;
    public float speed = 4.0f;
    public float gravity = -25f;
    public float jumpHeight = 0.8f;

    private bool isRunning = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        playerAudio = GetComponent<PlayerAudio>();
    }

    void Update()
    {
        isGrounded = controller.isGrounded;
        playerAudio?.SetGrounded(isGrounded);
    }

    public bool MoveEnabled = true;

    public void ProcessMove(Vector2 input)
    {
        if (!MoveEnabled) return;
        Vector3 moveDirection = Vector3.zero;
        moveDirection.x = input.x;
        moveDirection.z = input.y;
        controller.Move(transform.TransformDirection(moveDirection) * speed * Time.deltaTime);

        if (isGrounded && playerVelocity.y < 0)
            playerVelocity.y = -2.0f;
        else
            playerVelocity.y += gravity * Time.deltaTime;

        // Cap fall speed so flickering isGrounded on slopes can't push the
        // CharacterController through terrain geometry over multiple frames.
        playerVelocity.y = Mathf.Max(playerVelocity.y, -20f);

        controller.Move(playerVelocity * Time.deltaTime);
        AnimateRun(moveDirection);
    }

    void AnimateRun(Vector3 desiredDirection)
    {
        isRunning = (desiredDirection.x > 0.1f || desiredDirection.x < -0.1f) || (desiredDirection.z > 0.1f || desiredDirection.z < -0.1f);
        animator.SetBool("isWalking", isRunning);
        animator.SetBool("isRunning", isRunning && sprinting);
        playerAudio?.UpdateMovement(isRunning, sprinting);
    }

    public void Jump()
    {
        if (isGrounded)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -3.0f * gravity);
            playerAudio?.SetGrounded(false);
        }
    }

    public void Sprint()
    {
        sprinting = !sprinting;
        if (sprinting)
        {
            speed = 15;
        } else
        {
            speed = 4;
        }
    }
}
