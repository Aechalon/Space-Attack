using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBullet : MonoBehaviour
{
    [SerializeField, Min(0f)] private float lifetime = 6f;

    private Rigidbody2D body;
    private Camera gameplayCamera;
    private Action<EnemyBullet> returnToPool;
    private Vector2 direction;
    private float speed;
    private float remainingLifetime;
    private bool isReturning;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.useFullKinematicContacts = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        Collider2D bulletCollider = GetComponent<Collider2D>();
        if (bulletCollider == null)
            bulletCollider = gameObject.AddComponent<BoxCollider2D>();
        bulletCollider.isTrigger = true;
    }

    public void Initialize(Vector2 travelDirection, float launchSpeed, Camera cameraToUse, Action<EnemyBullet> poolReturn)
    {
        direction = travelDirection.sqrMagnitude > 0f ? travelDirection.normalized : Vector2.down;
        speed = Mathf.Max(0f, launchSpeed);
        gameplayCamera = cameraToUse;
        returnToPool = poolReturn;
        remainingLifetime = lifetime;
        isReturning = false;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void FixedUpdate()
    {
        remainingLifetime -= Time.fixedDeltaTime;
        if (remainingLifetime <= 0f)
        {
            ReturnToPool();
            return;
        }

        Vector2 nextPosition = body.position + direction * speed * Time.fixedDeltaTime;
        body.MovePosition(nextPosition);
        if (gameplayCamera == null)
            return;

        Vector3 viewportPosition = gameplayCamera.WorldToViewportPoint(nextPosition);
        if (viewportPosition.z < 0f || viewportPosition.x < 0f || viewportPosition.x > 1f ||
            viewportPosition.y < 0f || viewportPosition.y > 1f)
        {
            ReturnToPool();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isReturning)
            return;

        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player == null)
            player = other.GetComponentInParent<PlayerMovement>();
        if (player == null)
            return;

        player.TakeDamage(1);
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (isReturning)
            return;

        isReturning = true;
        returnToPool?.Invoke(this);
    }
}
