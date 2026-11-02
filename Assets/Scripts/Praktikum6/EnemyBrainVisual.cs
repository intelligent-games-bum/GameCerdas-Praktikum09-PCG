using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// (Bonus Animator Integration) Tampilan Enemy, terpisah dari logika <see cref="EnemyBrain"/>:
/// - Animator "Speed"  : diisi kecepatan NavMeshAgent → blend Idle / Run.
/// - Animator "Attack" : trigger tiap pukulan → state Attack di layer UpperBody (clip ayunan tangan).
/// - Animator "Action" : int action aktif (lihat <see cref="EnemyAction"/>), bisa dipakai transisi lain.
/// - Indikator di atas kepala berwarna sesuai action, berkedip putih saat kena damage.
/// - Model maju sedikit saat memukul (lunge), dan rebah ke belakang saat mati.
/// Parameter yang tidak ada di Animator Controller dilewati, jadi controller lain tetap aman dipakai.
/// </summary>
public class EnemyBrainVisual : MonoBehaviour
{
    [SerializeField] private EnemyBrain brain;
    [SerializeField] private Health health;
    [SerializeField] private NavMeshAgent agent;
    [Tooltip("Animator pada model karakter (child).")]
    [SerializeField] private Animator animator;
    [Tooltip("Transform model (child) yang digeser saat lunge dan diputar saat mati.")]
    [SerializeField] private Transform model;
    [Tooltip("Renderer indikator action di atas kepala.")]
    [SerializeField] private Renderer actionIndicator;

    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string actionParameter = "Action";

    [SerializeField] private float lungeDistance = 0.4f;
    [SerializeField] private float lungeTime = 0.3f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock block;
    private int speedHash;
    private int attackHash;
    private int actionHash;
    private bool hasSpeed;
    private bool hasAttack;
    private bool hasAction;
    private Vector3 modelRestPosition;
    private float flashTimer;
    private Coroutine lungeRoutine;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        speedHash = Animator.StringToHash(speedParameter);
        attackHash = Animator.StringToHash(attackTrigger);
        actionHash = Animator.StringToHash(actionParameter);
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                hasSpeed |= p.nameHash == speedHash && p.type == AnimatorControllerParameterType.Float;
                hasAttack |= p.nameHash == attackHash && p.type == AnimatorControllerParameterType.Trigger;
                hasAction |= p.nameHash == actionHash && p.type == AnimatorControllerParameterType.Int;
            }
        }
        if (model != null)
            modelRestPosition = model.localPosition;
    }

    private void OnEnable()
    {
        brain.ActionChanged += OnActionChanged;
        brain.Attacked += OnAttacked;
        health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        brain.ActionChanged -= OnActionChanged;
        brain.Attacked -= OnAttacked;
        health.Damaged -= OnDamaged;
    }

    private void Update()
    {
        if (animator != null && animator.enabled && hasSpeed)
        {
            float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
            animator.SetFloat(speedHash, speed, 0.1f, Time.deltaTime);
        }

        flashTimer -= Time.deltaTime;
        SetIndicatorColor(flashTimer > 0f ? Color.white : EnemyBrain.ActionColor(brain.CurrentAction));

        if (actionIndicator != null && brain.CurrentAction != EnemyAction.Dead)
            actionIndicator.transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
    }

    private void SetIndicatorColor(Color c)
    {
        if (actionIndicator == null)
            return;
        actionIndicator.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        block.SetColor(ColorId, c);
        actionIndicator.SetPropertyBlock(block);
    }

    private void OnDamaged(float amount)
    {
        flashTimer = 0.15f;
    }

    private void OnAttacked()
    {
        if (animator != null && animator.enabled && hasAttack)
            animator.SetTrigger(attackHash);

        if (model == null)
            return;
        if (lungeRoutine != null)
            StopCoroutine(lungeRoutine);
        lungeRoutine = StartCoroutine(Lunge());
    }

    private IEnumerator Lunge()
    {
        for (float t = 0f; t < lungeTime; t += Time.deltaTime)
        {
            // 0 -> 1 -> 0 dalam satu kali lunge.
            float k = Mathf.Sin(t / lungeTime * Mathf.PI);
            model.localPosition = modelRestPosition + Vector3.forward * (lungeDistance * k);
            yield return null;
        }
        model.localPosition = modelRestPosition;
        lungeRoutine = null;
    }

    private void OnActionChanged(EnemyAction from, EnemyAction to)
    {
        if (animator != null && animator.enabled && hasAction)
            animator.SetInteger(actionHash, (int)to);
        if (to == EnemyAction.Dead)
            StartCoroutine(FallDown());
    }

    private IEnumerator FallDown()
    {
        if (lungeRoutine != null)
            StopCoroutine(lungeRoutine);
        if (animator != null)
            animator.enabled = false;
        if (model == null)
            yield break;

        model.localPosition = modelRestPosition;
        Quaternion start = model.localRotation;
        Quaternion end = Quaternion.Euler(-90f, 0f, 0f) * start;
        const float duration = 0.6f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            model.localRotation = Quaternion.Slerp(start, end, k * k);
            yield return null;
        }
        model.localRotation = end;
    }
}
