using UnityEngine;
using System;

public enum EnemyState
{
    Formation,
    Diving,
    Returning
}

public enum EnemyReturnReason
{
    Killed,
    PlayerHit,
    MissedBottom
}

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float returnSpeed = 3f;

    private Rigidbody2D body;
    private Collider2D enemyCollider;
    private Camera gameplayCamera;
    private PlayerMovement player;
    private Transform formationRoot;
    private Vector3 slotLocalPosition;
    private Action<Enemy, EnemyReturnReason> returnToPool;
    private Action<Enemy> rejoinedFormation;
    private EnemyState state;
    private float diveSpeed;
    private float divePathLength;
    private float diveProgress;
    private Vector2 diveStart;
    private Vector2 diveControl;
    private Vector2 diveEnd;
    private bool divePathComplete;
    private bool isReturning;

    public EnemyState State => state;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        body.gravityScale = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        enemyCollider = GetComponent<Collider2D>();
        if (enemyCollider == null)
            enemyCollider = gameObject.AddComponent<BoxCollider2D>();
        enemyCollider.isTrigger = true;
    }

    public void Initialize(
        Camera cameraToUse,
        PlayerMovement playerToTarget,
        Transform parentFormation,
        Vector3 localSlotPosition,
        Action<Enemy, EnemyReturnReason> poolReturn,
        Action<Enemy> onRejoinedFormation)
    {
        gameplayCamera = cameraToUse;
        player = playerToTarget;
        formationRoot = parentFormation;
        slotLocalPosition = localSlotPosition;
        returnToPool = poolReturn;
        rejoinedFormation = onRejoinedFormation;
        state = EnemyState.Formation;
        isReturning = false;
        divePathComplete = false;
        body.position = transform.position;
        body.rotation = transform.eulerAngles.z;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        enemyCollider.enabled = true;
    }

    public void BeginDive(float speed, float passBelowPlayerDistance, float curveWidth)
    {
        if (state != EnemyState.Formation || player == null)
            return;

        state = EnemyState.Diving;
        diveSpeed = Mathf.Max(0.1f, speed);
        diveStart = body.position;
        diveEnd = new Vector2(player.transform.position.x, player.transform.position.y - passBelowPlayerDistance);
        float curveDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        Vector2 midpoint = (diveStart + diveEnd) * 0.5f;
        diveControl = midpoint + Vector2.right * curveDirection * curveWidth;
        divePathLength = Vector2.Distance(diveStart, diveControl) + Vector2.Distance(diveControl, diveEnd);
        diveProgress = 0f;
        divePathComplete = false;
    }

    private void FixedUpdate()
    {
        switch (state)
        {
            case EnemyState.Formation:
                body.MovePosition(formationRoot.TransformPoint(slotLocalPosition));
                break;
            case EnemyState.Diving:
                MoveWhileDiving();
                break;
            case EnemyState.Returning:
                MoveBackToFormation();
                break;
        }
    }

    private void MoveWhileDiving()
    {
        if (!divePathComplete)
        {
            diveProgress += diveSpeed * Time.fixedDeltaTime / Mathf.Max(divePathLength, 0.01f);
            float t = Mathf.Clamp01(diveProgress);
            float inverseT = 1f - t;
            Vector2 point = inverseT * inverseT * diveStart +
                2f * inverseT * t * diveControl + t * t * diveEnd;
            body.MovePosition(point);

            if (diveProgress >= 1f)
                divePathComplete = true;
            return;
        }

        Vector2 nextPosition = body.position + Vector2.down * diveSpeed * Time.fixedDeltaTime;
        body.MovePosition(nextPosition);
        if (gameplayCamera == null)
            return;

        Vector3 viewportPosition = gameplayCamera.WorldToViewportPoint(nextPosition);
        if (viewportPosition.z < 0f || viewportPosition.y >= 0f)
            return;

        if (UnityEngine.Random.value < 0.5f)
        {
            state = EnemyState.Returning;
            return;
        }

        ReturnToPool(EnemyReturnReason.MissedBottom);
    }

    private void MoveBackToFormation()
    {
        Vector2 targetPosition = formationRoot.TransformPoint(slotLocalPosition);
        Vector2 nextPosition = Vector2.MoveTowards(body.position, targetPosition, returnSpeed * Time.fixedDeltaTime);
        body.MovePosition(nextPosition);

        if ((nextPosition - targetPosition).sqrMagnitude > 0.0025f)
            return;

        state = EnemyState.Formation;
        rejoinedFormation?.Invoke(this);
    }

    public void Hit()
    {
        ReturnToPool(EnemyReturnReason.Killed);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (state != EnemyState.Diving)
            return;

        PlayerMovement hitPlayer = other.GetComponent<PlayerMovement>();
        if (hitPlayer == null)
            hitPlayer = other.GetComponentInParent<PlayerMovement>();
        if (hitPlayer == null)
            return;

        hitPlayer.TakeDamage(1);
        ReturnToPool(EnemyReturnReason.PlayerHit);
    }

    private void ReturnToPool(EnemyReturnReason reason)
    {
        if (isReturning)
            return;

        isReturning = true;
        enemyCollider.enabled = false;
        returnToPool?.Invoke(this, reason);
    }
}
