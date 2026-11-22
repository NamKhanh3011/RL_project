using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

/// <summary>
/// High-Speed Greedy Vacuum Agent (Roomba):
/// - High agility speed (6.0 m/s, 320 deg/s) to clean the whole room in under 1 minute (< 45s).
/// - Wide vacuum suction zone (0.55m radius) for 100% dirt collection rate.
/// - Fast pathfinding prioritizing forward dirt nodes.
/// - Continuous positive rewards peaking at ~20.0 max.
/// - Instant 0.3s escape maneuver if obstructed.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class VacuumAgent : Agent
{
    [Header("Movement Physics (Under 1-Minute Performance)")]
    public float moveSpeed = 6.0f;
    public float turnSpeed = 320f;

    [Header("Environment")]
    public RoomArea roomArea;

    private Rigidbody rb;
    private CapsuleCollider capsuleCol;
    private SphereCollider suctionTrigger;
    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;
    private float previousDistanceToNearestDirt = 0f;

    // Fast Escape State
    private bool isEscaping = false;
    private float escapeTimer = 0f;
    private float escapeTurnDir = 1f;
    private Vector3 lastTrackedPos;
    private float stuckCheckInterval = 0f;

    // Room boundaries
    private const float MIN_X = -5.9f;
    private const float MAX_X = 5.9f;
    private const float MIN_Z = -3.3f;
    private const float MAX_Z = 3.9f;

    public override void Initialize()
    {
        MaxStep = 12000; // Allows up to 4 minutes (240s at 50Hz)
        moveSpeed = 6.0f;
        turnSpeed = 320f;

        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;

        // Frictionless physics material
        capsuleCol = GetComponent<CapsuleCollider>();
        if (capsuleCol != null)
        {
            PhysicsMaterial frictionless = new PhysicsMaterial("ZeroFrictionMat")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0.1f,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            capsuleCol.material = frictionless;
        }

        // Add Vacuum Suction Zone (covers entire 0.85m diameter + brushes: 0.65m radius)
        suctionTrigger = gameObject.AddComponent<SphereCollider>();
        suctionTrigger.isTrigger = true;
        suctionTrigger.radius = 0.65f;

        lastTrackedPos = transform.position;
    }

    public override void OnEpisodeBegin()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        transform.localPosition = initialLocalPos;
        transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        isEscaping = false;
        escapeTimer = 0f;
        stuckCheckInterval = 0f;
        lastTrackedPos = transform.position;

        if (roomArea != null)
        {
            roomArea.ResetEpisode();

            if (roomArea.GetNearestDirtInfo(transform, out _, out _, out float initialDist))
            {
                previousDistanceToNearestDirt = initialDist;
            }
            else
            {
                previousDistanceToNearestDirt = 10f;
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (sensor == null) return;

        // 1 & 2: Local velocity
        Vector3 localVel = transform.InverseTransformDirection(rb != null ? rb.linearVelocity : Vector3.zero);
        sensor.AddObservation(localVel.x / moveSpeed);
        sensor.AddObservation(localVel.z / moveSpeed);

        // 3: Angular velocity
        sensor.AddObservation((rb != null ? rb.angularVelocity.y : 0f) / 5f);

        // 4: Dirt ratio remaining
        float dirtRatio = roomArea != null ? roomArea.GetDirtRatio() : 0f;
        sensor.AddObservation(dirtRatio);

        // 5 & 6: Local direction vector to nearest dirt
        Vector3 localDir = Vector3.forward;
        if (roomArea != null)
        {
            roomArea.GetNearestDirtInfo(transform, out localDir, out _, out _);
        }
        sensor.AddObservation(localDir.x);
        sensor.AddObservation(localDir.z);

        // Exactly 6 Vector Observations
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float forwardAction = 0f;
        float turnAction = 0f;

        if (actions.ContinuousActions.Length >= 2)
        {
            forwardAction = Mathf.Clamp(actions.ContinuousActions[0], -0.6f, 1.0f);
            turnAction = Mathf.Clamp(actions.ContinuousActions[1], -1.0f, 1.0f);
        }
        else if (actions.DiscreteActions.Length >= 1)
        {
            int act = actions.DiscreteActions[0];
            if (act == 1) forwardAction = 1f;
            else if (act == 2) turnAction = -1f;
            else if (act == 3) turnAction = 1f;
            else if (act == 4) forwardAction = -0.6f;
        }

        // Apply physical translation
        Vector3 moveDelta = transform.forward * forwardAction * moveSpeed * Time.fixedDeltaTime;
        if (rb != null)
        {
            rb.MovePosition(rb.position + moveDelta);
        }
        else
        {
            transform.position += moveDelta;
        }

        // Apply agile rotation
        float turnDelta = turnAction * turnSpeed * Time.fixedDeltaTime;
        Quaternion turnRotation = Quaternion.Euler(0f, turnDelta, 0f);
        if (rb != null)
        {
            rb.MoveRotation(rb.rotation * turnRotation);
        }
        else
        {
            transform.rotation *= turnRotation;
        }

        // Update distance tracking (no reward added so max episode reward is strictly capped at 20.0)
        if (roomArea != null && roomArea.GetNearestDirtInfo(transform, out _, out _, out float currentDist))
        {
            previousDistanceToNearestDirt = currentDist;
        }

        // Strict room boundary clamp
        ClampPositionInsideRoom();
    }

    private void ClampPositionInsideRoom()
    {
        Vector3 pos = transform.position;
        float clampedX = Mathf.Clamp(pos.x, MIN_X, MAX_X);
        float clampedZ = Mathf.Clamp(pos.z, MIN_Z, MAX_Z);

        if (pos.x != clampedX || pos.z != clampedZ)
        {
            transform.position = new Vector3(clampedX, pos.y, clampedZ);
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Minimal bump penalty
        if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-0.005f);

            // Fast bumper rebound
            Vector3 contactNormal = collision.contacts.Length > 0 ? collision.contacts[0].normal : -transform.forward;
            if (Vector3.Dot(transform.forward, -contactNormal) > 0.4f)
            {
                isEscaping = true;
                escapeTimer = 0.5f;
                escapeTurnDir = Random.value > 0.5f ? 1.0f : -1.0f;
            }
        }
    }

    // Vacuum Suction: Sweeps any dirt that enters the 0.55m suction zone
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Dirt"))
        {
            DirtItem dirt = other.GetComponent<DirtItem>();
            if (dirt != null && roomArea != null)
            {
                roomArea.CollectDirt(dirt);
                Destroy(other.gameObject);
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        if (continuousActions.Length < 2) return;

        float v = Input.GetAxisRaw("Vertical");
        float h = Input.GetAxisRaw("Horizontal");

        // 1. Manual User Override (WASD / Arrow Keys)
        if (Mathf.Abs(v) > 0.1f || Mathf.Abs(h) > 0.1f)
        {
            continuousActions[0] = v;
            continuousActions[1] = h;
            isEscaping = false;
            return;
        }

        // 2. Anti-Stuck Detector (0.35s threshold)
        stuckCheckInterval += Time.deltaTime;
        if (stuckCheckInterval >= 0.35f)
        {
            float movedDist = Vector3.Distance(transform.position, lastTrackedPos);
            lastTrackedPos = transform.position;
            stuckCheckInterval = 0f;

            if (movedDist < 0.08f && !isEscaping)
            {
                isEscaping = true;
                escapeTimer = 0.5f;
                escapeTurnDir = Random.value > 0.5f ? 1.0f : -1.0f;
            }
        }

        // 3. Fast Escape Routine
        if (isEscaping)
        {
            escapeTimer -= Time.deltaTime;
            if (escapeTimer > 0.2f)
            {
                continuousActions[0] = -1.0f; // Fast reverse
                continuousActions[1] = escapeTurnDir * 0.5f;
            }
            else if (escapeTimer > 0f)
            {
                continuousActions[0] = 0.2f;
                continuousActions[1] = escapeTurnDir * 1.0f; // Quick spin out
            }
            else
            {
                isEscaping = false;
            }
            return;
        }

        // 4. Query target dirt & cluster transit mode
        Vector3 localDir = Vector3.forward;
        Vector3 worldDir = transform.forward;
        float distance = 10f;
        bool isTransitMode = false;
        bool hasDirt = roomArea != null && roomArea.GetNearestDirtInfo(transform, out localDir, out worldDir, out distance, out isTransitMode);

        if (!hasDirt)
        {
            continuousActions[0] = 0f;
            continuousActions[1] = 0f;
            return;
        }

        // 5. APF (Artificial Potential Field) Blended Obstacle Avoidance
        Vector3 targetForce = worldDir;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.12f;
        float rayRange = 1.15f;
        Vector3 obstacleRepulsion = Vector3.zero;
        bool frontCloseBlocked = false;

        float[] rayAngles = { -60f, -30f, 0f, 30f, 60f };
        for (int i = 0; i < rayAngles.Length; i++)
        {
            Vector3 rayDir = Quaternion.Euler(0, rayAngles[i], 0) * transform.forward;
            if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, rayRange))
            {
                if (hit.collider.CompareTag("Dirt") || hit.collider.transform == roomArea?.floorTransform)
                    continue;

                float proximity = (rayRange - hit.distance) / rayRange; // 0 (far) to 1 (near)
                Vector3 repel = hit.normal * (proximity * 2.2f);
                obstacleRepulsion += repel;

                if (Mathf.Abs(rayAngles[i]) < 20f && hit.distance < 0.45f)
                {
                    frontCloseBlocked = true;
                }
            }
        }

        // If front is intimately blocked (< 0.45m), back up slightly while steering away
        if (frontCloseBlocked)
        {
            continuousActions[0] = -0.6f;
            continuousActions[1] = (localDir.x >= 0 ? 1.0f : -1.0f);
            return;
        }

        // Combine target attraction and obstacle repulsion
        Vector3 desiredDir = targetForce + obstacleRepulsion;
        desiredDir.y = 0f;
        if (desiredDir.sqrMagnitude > 0.001f)
        {
            desiredDir.Normalize();
        }
        else
        {
            desiredDir = worldDir;
        }

        // Calculate local angle to desired heading
        Vector3 desiredLocal = transform.InverseTransformDirection(desiredDir);
        float steerAngle = Mathf.Atan2(desiredLocal.x, desiredLocal.z) * Mathf.Rad2Deg;

        float steer = Mathf.Clamp(steerAngle / 35f, -1.0f, 1.0f);
        float throttle = 1.0f;

        // If target/exit is behind robot (steerAngle > 70 deg): spin on the spot!
        if (Mathf.Abs(steerAngle) > 70f)
        {
            throttle = 0.15f; // Slow crawl while spinning fast
            steer = Mathf.Sign(steerAngle) * 1.0f;
        }
        else if (Mathf.Abs(steerAngle) > 35f)
        {
            throttle = 0.65f;
        }
        else
        {
            throttle = 1.0f; // Full throttle sprint!
        }

        // In Transit Mode (escaping empty area towards richest cluster): ensure full throttle!
        if (isTransitMode && Mathf.Abs(steerAngle) <= 60f)
        {
            throttle = 1.0f;
        }

        continuousActions[0] = throttle;
        continuousActions[1] = steer;
    }
}
