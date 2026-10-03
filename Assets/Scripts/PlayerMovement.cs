using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;
using UnityEngine.UI;
using System;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementSpeed = 8f;
    [SerializeField] private Camera gameplayCamera;

    [Header("Laser")]
    [SerializeField] private LaserBullet laserPrefab;
    [SerializeField, Min(1)] private int bulletPoolSize = 5;
    [SerializeField, Min(1)] private int bulletPoolMaxSize = 20;
    [SerializeField] private EnemyBullet enemyBulletPrefab;
    [SerializeField, Min(1)] private int enemyBulletPoolSize = 8;
    [SerializeField, Min(1)] private int enemyBulletPoolMaxSize = 30;
    [SerializeField] private Transform firePoint;
    [SerializeField, Min(0.01f)] private float fireRate = 0.25f;
    [SerializeField, Min(0f)] private float bulletSpeed = 14f;

    [Header("Enemies")]
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField, Min(1)] private int enemyPoolSize = 5;
    [SerializeField, Min(1)] private int enemyPoolMaxSize = 30;

    [Header("Health")]
    [SerializeField, Range(1, 10)] private int maxHealth = 10;
    [SerializeField, Min(0f)] private float hitInvulnerabilityDuration = 2f;
    [SerializeField] private Slider healthSlider;

    public float FireRate
    {
        get => fireRate;
        set => fireRate = Mathf.Max(0.01f, value);
    }

    public event Action<int, int> HpChanged;
    public event Action Died;

    public Camera GameplayCamera => gameplayCamera;
    public Transform PlayerTransform => transform;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private Rigidbody2D body;
    private Collider2D playerCollider;
    private float horizontalInput;
    private float nextFireTime;
    private float halfPlayerWidth;
    private int currentHealth;
    private float invulnerableUntil;
    private Transform bulletPoolRoot;
    private ObjectPool<LaserBullet> bulletPool;
    private Transform enemyPoolRoot;
    private ObjectPool<Enemy> enemyPool;
    private Transform enemyBulletPoolRoot;
    private ObjectPool<EnemyBullet> enemyBulletPool;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        if (playerCollider == null)
            playerCollider = gameObject.AddComponent<BoxCollider2D>();
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.useFullKinematicContacts = true;
        body.constraints |= RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;
        currentHealth = maxHealth;
        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = maxHealth;
            healthSlider.SetValueWithoutNotify(currentHealth);
        }

        if (laserPrefab != null)
        {
            bulletPoolRoot = new GameObject("Laser Bullet Pool").transform;
            bulletPoolRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            int maxSize = Mathf.Max(1, bulletPoolMaxSize);
            int prewarmCount = Mathf.Clamp(bulletPoolSize, 1, maxSize);
            bulletPool = new ObjectPool<LaserBullet>(
                CreateBullet,
                actionOnRelease: ReleaseBullet,
                actionOnDestroy: DestroyBullet,
                collectionCheck: true,
                defaultCapacity: prewarmCount,
                maxSize: maxSize);

            for (int i = 0; i < prewarmCount; i++)
            {
                LaserBullet bullet = bulletPool.Get();
                bulletPool.Release(bullet);
            }
        }

        if (enemyPrefab != null)
        {
            enemyPoolRoot = new GameObject("Enemy Pool").transform;
            enemyPoolRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            int maxSize = Mathf.Max(1, enemyPoolMaxSize);
            int prewarmCount = Mathf.Clamp(enemyPoolSize, 1, maxSize);
            enemyPool = new ObjectPool<Enemy>(
                CreateEnemy,
                actionOnRelease: ReleaseEnemy,
                actionOnDestroy: DestroyEnemy,
                collectionCheck: true,
                defaultCapacity: prewarmCount,
                maxSize: maxSize);

            for (int i = 0; i < prewarmCount; i++)
            {
                Enemy enemy = enemyPool.Get();
                enemyPool.Release(enemy);
            }
        }
        else
            Debug.LogError("Enemy prefab is missing on PlayerMovement; assign an Enemy prefab to enable formation spawning.", this);

        if (enemyBulletPrefab != null)
        {
            enemyBulletPoolRoot = new GameObject("Enemy Bullet Pool").transform;
            enemyBulletPoolRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            int maxSize = Mathf.Max(1, enemyBulletPoolMaxSize);
            int prewarmCount = Mathf.Clamp(enemyBulletPoolSize, 1, maxSize);
            enemyBulletPool = new ObjectPool<EnemyBullet>(
                CreateEnemyBullet,
                actionOnRelease: ReleaseEnemyBullet,
                actionOnDestroy: DestroyEnemyBullet,
                collectionCheck: true,
                defaultCapacity: prewarmCount,
                maxSize: maxSize);

            for (int i = 0; i < prewarmCount; i++)
            {
                EnemyBullet bullet = enemyBulletPool.Get();
                enemyBulletPool.Release(bullet);
            }
        }

        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        if (playerCollider != null)
            halfPlayerWidth = playerCollider.bounds.extents.x;
    }

    private void Update()
    {
        if (currentHealth <= 0)
        {
            horizontalInput = 0f;
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            horizontalInput = 0f;
            return;
        }

        horizontalInput = 0f;
        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed)
            horizontalInput -= 1f;
        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
            horizontalInput += 1f;

        if (keyboard.spaceKey.isPressed && Time.time >= nextFireTime)
            Fire();
    }

    private void FixedUpdate()
    {
        if (currentHealth <= 0)
            return;

        float targetX = body.position.x + horizontalInput * movementSpeed * Time.fixedDeltaTime;
        targetX = Mathf.Clamp(targetX, ScreenLeft + halfPlayerWidth, ScreenRight - halfPlayerWidth);
        body.MovePosition(new Vector2(targetX, body.position.y));
    }

    private void Fire()
    {
        if (laserPrefab == null)
            return;

        nextFireTime = Time.time + fireRate;
        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
        LaserBullet bullet = bulletPool.Get();
        bullet.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
        bullet.Initialize(bulletSpeed, gameplayCamera, bulletPool.Release);
        bullet.gameObject.SetActive(true);
    }

    private LaserBullet CreateBullet()
    {
        LaserBullet bullet = Instantiate(laserPrefab, bulletPoolRoot);
        bullet.gameObject.SetActive(false);
        return bullet;
    }

    private void ReleaseBullet(LaserBullet bullet)
    {
        bullet.gameObject.SetActive(false);
        bullet.transform.SetParent(bulletPoolRoot, true);
    }

    private void DestroyBullet(LaserBullet bullet)
    {
        Destroy(bullet.gameObject);
    }

    public Enemy GetPooledEnemy()
    {
        return enemyPool != null ? enemyPool.Get() : null;
    }

    public void FireEnemyBullet(Vector3 spawnPosition, Vector2 direction, float speed)
    {
        if (enemyBulletPool == null)
            return;

        EnemyBullet bullet = enemyBulletPool.Get();
        bullet.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
        bullet.Initialize(direction, speed, gameplayCamera, enemyBulletPool.Release);
        bullet.gameObject.SetActive(true);
    }

    private EnemyBullet CreateEnemyBullet()
    {
        EnemyBullet bullet = Instantiate(enemyBulletPrefab, enemyBulletPoolRoot);
        bullet.gameObject.SetActive(false);
        return bullet;
    }

    private void ReleaseEnemyBullet(EnemyBullet bullet)
    {
        bullet.gameObject.SetActive(false);
        bullet.transform.SetParent(enemyBulletPoolRoot, true);
    }

    private void DestroyEnemyBullet(EnemyBullet bullet)
    {
        Destroy(bullet.gameObject);
    }

    private Enemy CreateEnemy()
    {
        Enemy enemy = Instantiate(enemyPrefab, enemyPoolRoot);
        enemy.gameObject.SetActive(false);
        return enemy;
    }

    private void ReleaseEnemy(Enemy enemy)
    {
        enemy.gameObject.SetActive(false);
        enemy.transform.SetParent(enemyPoolRoot, true);
    }

    private void DestroyEnemy(Enemy enemy)
    {
        Destroy(enemy.gameObject);
    }

    public void ReturnEnemyToPool(Enemy enemy)
    {
        if (enemyPool != null && enemy != null)
            enemyPool.Release(enemy);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || currentHealth <= 0 || Time.time < invulnerableUntil)
            return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        invulnerableUntil = Time.time + hitInvulnerabilityDuration;
        HpChanged?.Invoke(currentHealth, maxHealth);
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        if (currentHealth == 0)
            Died?.Invoke();
    }

    private void OnDestroy()
    {
        bulletPool?.Clear();
        enemyPool?.Clear();
        enemyBulletPool?.Clear();
        if (bulletPoolRoot != null)
            Destroy(bulletPoolRoot.gameObject);
        if (enemyPoolRoot != null)
            Destroy(enemyPoolRoot.gameObject);
        if (enemyBulletPoolRoot != null)
            Destroy(enemyBulletPoolRoot.gameObject);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Clamp(maxHealth, 1, 10);
        hitInvulnerabilityDuration = Mathf.Max(0f, hitInvulnerabilityDuration);
        fireRate = Mathf.Max(0.01f, fireRate);
    }

    private float ScreenLeft => gameplayCamera != null
        ? gameplayCamera.ViewportToWorldPoint(new Vector3(0f, 0f, -gameplayCamera.transform.position.z)).x
        : float.NegativeInfinity;

    private float ScreenRight => gameplayCamera != null
        ? gameplayCamera.ViewportToWorldPoint(new Vector3(1f, 0f, -gameplayCamera.transform.position.z)).x
        : float.PositiveInfinity;
}
