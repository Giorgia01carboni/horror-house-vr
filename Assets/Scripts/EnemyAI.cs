using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Idle, Chase }

    [Header("Detection")]
    [SerializeField] private float detectionRange = 5f;

    [Header("Alert Sound")]
    [SerializeField] private AudioClip alertClip;
    [SerializeField] private float alertVolume = 0.8f;

    [Header("Step Sound")]
    [SerializeField] private AudioClip stepClip;
    [SerializeField] private float stepInterval  = 0.55f;
    [SerializeField] private float stepVolume    = 0.65f;
    [SerializeField] private float stepMinDist   = 8f;
    [SerializeField] private float stepMaxDist   = 40f;
    [SerializeField] private float stepDoppler   = 2f;

    [Header("Hit Reaction")]
    [SerializeField] private AudioClip hurtClip;
    [SerializeField] private float slowdownFactor   = 0.35f;
    [SerializeField] private float slowdownDuration = 2f;

    [Header("Kill")]
    [SerializeField] private float killRange = 1.3f;

    private NavMeshAgent agent;
    private Animator animator;
    private AudioSource audioSource;
    private AudioSource stepAudioSource;
    private AudioSource hurtAudioSource;
    private Transform player;

    private State state = State.Idle;
    private float _normalSpeed;
    private Coroutine _slowdown;

    static readonly int IsChasing = Animator.StringToHash("isChasing");

    void Start()
    {
        agent    = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.playOnAwake  = false;
        audioSource.loop         = false;

        stepAudioSource = gameObject.AddComponent<AudioSource>();
        stepAudioSource.spatialBlend  = 1f;
        stepAudioSource.playOnAwake   = false;
        stepAudioSource.loop          = false;
        stepAudioSource.volume        = stepVolume;
        stepAudioSource.dopplerLevel  = stepDoppler;
        stepAudioSource.rolloffMode   = AudioRolloffMode.Linear;
        stepAudioSource.minDistance   = stepMinDist;
        stepAudioSource.maxDistance   = stepMaxDist;
        stepAudioSource.spread        = 90f;

        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
            player = playerGO.transform;
        else
            Debug.LogError("EnemyAI: No GameObject tagged 'Player' found.", this);

        // 2D source for hurt screech — always audible regardless of distance
        hurtAudioSource              = gameObject.AddComponent<AudioSource>();
        hurtAudioSource.spatialBlend = 0f;
        hurtAudioSource.playOnAwake  = false;
        hurtAudioSource.loop         = false;
        hurtAudioSource.priority     = 0;

        _normalSpeed = agent.speed > 0f ? agent.speed : 3.5f;

        if (agent.isOnNavMesh)
            agent.isStopped = true;
    }

    void Update()
    {
        if (player == null) return;

        // Player reached the truck and is escaping — freeze the enemy for good
        if (TruckInteraction.WinSequenceActive)
        {
            if (state == State.Chase) StopChase();
            return;
        }

        float dist = Vector3.Distance(transform.position, player.position);

        float flatDist = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(player.position.x,    player.position.z));
        if (flatDist <= killRange && !TruckInteraction.WinSequenceActive)
        {
            GameOverScreen.Show();
            return;
        }

        switch (state)
        {
            case State.Idle:
                if (dist <= detectionRange)
                    StartChase();
                break;

            case State.Chase:
                if (agent.isOnNavMesh)
                    agent.SetDestination(player.position);
                break;
        }
    }

    public void StartChase()
    {
        bool wasAlreadyChasing = state == State.Chase;
        state = State.Chase;
        if (!wasAlreadyChasing && alertClip != null) audioSource.PlayOneShot(alertClip, alertVolume);
        if (agent.isOnNavMesh) agent.isStopped = false;
        animator.SetBool(IsChasing, true);
        if (!wasAlreadyChasing) StartCoroutine(StepLoop());
    }

    public void StopChase()
    {
        state = State.Idle;
        if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
        animator.SetBool(IsChasing, false);
    }

    IEnumerator StepLoop()
    {
        while (state == State.Chase)
        {
            if (stepClip != null) stepAudioSource.PlayOneShot(stepClip);
            yield return new WaitForSeconds(stepInterval);
        }
    }

    void OnShot(RaycastHit hit)
    {
        if (hurtClip != null) hurtAudioSource.PlayOneShot(hurtClip);
        if (_slowdown != null) StopCoroutine(_slowdown);
        _slowdown = StartCoroutine(SlowdownCoroutine());
    }

    IEnumerator SlowdownCoroutine()
    {
        agent.speed = _normalSpeed * slowdownFactor;
        yield return new WaitForSeconds(slowdownDuration);
        agent.speed = _normalSpeed;
        _slowdown = null;
    }

    // Silences the AnimationEvent baked into the shared Idle.fbx
    void PlayerFootstepsSoundStop() { }
}
