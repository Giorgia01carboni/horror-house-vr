using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    [SerializeField] private AudioClip outdoorFootsteps;
    [SerializeField] private AudioClip woodenFloorFootsteps;
    [SerializeField] private LayerMask woodenFloorMask;
    [SerializeField] private float runPitch = 1.6f;

    private AudioSource animationSoundPlayer;
    private bool insideHouse;
    private bool isGrounded = true;

    void Start()
    {
        animationSoundPlayer = GetComponent<AudioSource>();
        animationSoundPlayer.loop = true;
    }

    public void SetInsideHouse(bool inside)
    {
        insideHouse = inside;
        if (animationSoundPlayer == null || !animationSoundPlayer.isPlaying) return;
        AudioClip clip = GetFootstepClip();
        if (animationSoundPlayer.clip != clip)
        {
            animationSoundPlayer.clip = clip;
            animationSoundPlayer.Play();
        }
    }

    public void SetGrounded(bool grounded)
    {
        isGrounded = grounded;
        if (!grounded)
            animationSoundPlayer.Stop();
    }

    public void UpdateMovement(bool moving, bool sprinting)
    {
        if (!isGrounded || !moving)
        {
            animationSoundPlayer.Stop();
            return;
        }

        AudioClip clip = GetFootstepClip();
        float targetPitch = sprinting ? runPitch : 1f;

        if (animationSoundPlayer.clip != clip)
        {
            animationSoundPlayer.clip = clip;
            animationSoundPlayer.pitch = targetPitch;
            animationSoundPlayer.Play();
        }
        else
        {
            animationSoundPlayer.pitch = targetPitch;
            if (!animationSoundPlayer.isPlaying)
                animationSoundPlayer.Play();
        }
    }

    // Kept as stubs so existing animation events don't throw errors.
    private void PlayerFootstepsSound() { }
    private void PlayerFootstepsSoundStop() { }

    private AudioClip GetFootstepClip()
    {
        if (insideHouse)
            return woodenFloorFootsteps != null ? woodenFloorFootsteps : outdoorFootsteps;

        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 3f, woodenFloorMask))
            return woodenFloorFootsteps != null ? woodenFloorFootsteps : outdoorFootsteps;

        return outdoorFootsteps;
    }
}
