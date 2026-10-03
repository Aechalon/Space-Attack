using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody2D))]
public class LaserBullet : MonoBehaviour
{
    [SerializeField, Min(0f)] private float defaultSpeed = 14f;
    [SerializeField, Min(0f)] private float lifetime = 5f;

    private Rigidbody2D body;
    private float speed;
    private float remainingLifetime;
    private Camera gameplayCamera;
    private Action<LaserBullet> returnToPool;
    private bool isReturning;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.useFullKinematicContacts = true;
        speed = defaultSpeed;

        Collider2D bulletCollider = GetComponent<Collider2D>();
        if (bulletCollider == null)
            bulletCollider = gameObject.AddComponent<BoxCollider2D>();
        bulletCollider.isTrigger = true;
    }

    public void Initialize(float launchSpeed, Camera cameraToUse, Action<LaserBullet> poolReturn)
    {
        speed = launchSpeed;
        remainingLifetime = lifetime;
        gameplayCamera = cameraToUse;
        returnToPool = poolReturn;
        isReturning = false;
        body.linearVelocity = Vector2.zero;
    }

    private void FixedUpdate()
    {
        remainingLifetime -= Time.fixedDeltaTime;
        if (remainingLifetime <= 0f)
        {
            ReturnToPool();
            return;
        }

        Vector2 nextPosition = body.position + Vector2.up * speed * Time.fixedDeltaTime;
        body.MovePosition(nextPosition);

        if (gameplayCamera != null)
        {
            Vector3 viewportPosition = gameplayCamera.WorldToViewportPoint(nextPosition);
            if (viewportPosition.z < 0f || viewportPosition.x < 0f || viewportPosition.x > 1f ||
                viewportPosition.y < 0f || viewportPosition.y > 1f)
            {
                ReturnToPool();
            }
        }
    }

    private void ReturnToPool()
    {
        if (isReturning)
            return;

        isReturning = true;
        returnToPool?.Invoke(this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isReturning)
            return;

        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null)
            enemy = other.GetComponentInParent<Enemy>();

        if (enemy == null)
            return;

        enemy.Hit();
        ReturnToPool();
    }
}
