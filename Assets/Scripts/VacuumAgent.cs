using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(Rigidbody))]
public class VacuumAgent : Agent
{
    [Header("Movement Physics")]
    public float moveSpeed = 6f;
    public float turnSpeed = 320f;

    [Header("Environment")]
    public RoomArea roomArea;

    private const float DirtSuctionRadius = 0.65f;
    private const float CollisionPenalty = -0.005f;
    private const float ReverseLimit = -0.6f;
    private static readonly float[] AvoidanceAngles = { -60f, -30f, 0f, 30f, 60f };

    private Rigidbody rb;
    private Vector3 initialLocalPos;
    private bool isEscaping;
    private float escapeTimer;
    private float escapeTurnDir = 1f;
    private Vector3 lastTrackedPos;
    private float stuckCheckInterval;

    public override void Initialize()
    {
        MaxStep = 12000;
        moveSpeed = 6f;
        turnSpeed = 320f;

        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        initialLocalPos = transform.localPosition;
        lastTrackedPos = transform.position;

        if (TryGetComponent(out CapsuleCollider capsule))
        {
            capsule.material = new PhysicsMaterial("ZeroFrictionMat")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0.1f,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }

        SphereCollider suction = gameObject.AddComponent<SphereCollider>();
        suction.isTrigger = true;
        suction.radius = DirtSuctionRadius;
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
            roomArea.ResetEpisode();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (sensor == null) return;

        // Preserve the order of the model's six vector inputs.
        Vector3 localVelocity = transform.InverseTransformDirection(rb != null ? rb.linearVelocity : Vector3.zero);
        sensor.AddObservation(localVelocity.x / moveSpeed);
        sensor.AddObservation(localVelocity.z / moveSpeed);
        sensor.AddObservation((rb != null ? rb.angularVelocity.y : 0f) / 5f);
        sensor.AddObservation(roomArea != null ? roomArea.GetDirtRatio() : 0f);

        Vector3 localDir = Vector3.forward;
        if (roomArea != null)
            roomArea.GetNearestDirtInfo(transform, out localDir, out _, out _);
        sensor.AddObservation(localDir.x);
        sensor.AddObservation(localDir.z);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float throttle = 0f;
        float steer = 0f;

        if (actions.ContinuousActions.Length >= 2)
        {
            throttle = Mathf.Clamp(actions.ContinuousActions[0], ReverseLimit, 1f);
            steer = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        }
        else if (actions.DiscreteActions.Length >= 1)
        {
            int action = actions.DiscreteActions[0];
            if (action == 1) throttle = 1f;
            else if (action == 2) steer = -1f;
            else if (action == 3) steer = 1f;
            else if (action == 4) throttle = ReverseLimit;
        }

        Vector3 movement = transform.forward * throttle * moveSpeed * Time.fixedDeltaTime;
        Quaternion rotation = Quaternion.Euler(0f, steer * turnSpeed * Time.fixedDeltaTime, 0f);
        if (rb != null)
        {
            rb.MovePosition(rb.position + movement);
            rb.MoveRotation(rb.rotation * rotation);
        }
        else
        {
            transform.position += movement;
            transform.rotation *= rotation;
        }

        ClampPositionInsideRoom();
    }

    private void ClampPositionInsideRoom()
    {
        Vector3 pos = transform.position;
        float x = Mathf.Clamp(pos.x, -5.9f, 5.9f);
        float z = Mathf.Clamp(pos.z, -3.3f, 3.9f);
        if (pos.x == x && pos.z == z) return;

        transform.position = new Vector3(x, pos.y, z);
        if (rb != null)
            rb.linearVelocity = Vector3.zero;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Obstacle") && !collision.gameObject.CompareTag("Wall"))
            return;

        AddReward(CollisionPenalty);

        Vector3 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : -transform.forward;
        if (Vector3.Dot(transform.forward, -normal) > 0.4f)
            StartEscape();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (roomArea != null && other.CompareTag("Dirt") && other.TryGetComponent(out DirtItem dirt))
            roomArea.CollectDirt(dirt);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> actions = actionsOut.ContinuousActions;
        if (actions.Length < 2) return;

        float vertical = Input.GetAxisRaw("Vertical");
        float horizontal = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(vertical) > 0.1f || Mathf.Abs(horizontal) > 0.1f)
        {
            actions[0] = vertical;
            actions[1] = horizontal;
            isEscaping = false;
            return;
        }

        CheckForStuck();
        if (isEscaping)
        {
            escapeTimer -= Time.deltaTime;
            if (escapeTimer > 0.2f)
            {
                actions[0] = -1f;
                actions[1] = escapeTurnDir * 0.5f;
            }
            else if (escapeTimer > 0f)
            {
                actions[0] = 0.2f;
                actions[1] = escapeTurnDir;
            }
            else
            {
                isEscaping = false;
            }
            return;
        }

        if (roomArea == null ||
            !roomArea.GetNearestDirtInfo(transform, out Vector3 localDir, out Vector3 worldDir, out _, out bool isTransitMode))
        {
            actions[0] = 0f;
            actions[1] = 0f;
            return;
        }

        SteerTowardDirt(actions, localDir, worldDir, isTransitMode);
    }

    private void CheckForStuck()
    {
        stuckCheckInterval += Time.deltaTime;
        if (stuckCheckInterval < 0.35f) return;

        float movedDistance = Vector3.Distance(transform.position, lastTrackedPos);
        lastTrackedPos = transform.position;
        stuckCheckInterval = 0f;

        if (movedDistance < 0.08f && !isEscaping)
            StartEscape();
    }

    private void StartEscape()
    {
        isEscaping = true;
        escapeTimer = 0.5f;
        escapeTurnDir = Random.value > 0.5f ? 1f : -1f;
    }

    private void SteerTowardDirt(ActionSegment<float> actions, Vector3 localDir, Vector3 worldDir, bool isTransitMode)
    {
        const float rayRange = 1.15f;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.12f;
        Vector3 obstacleRepulsion = Vector3.zero;

        foreach (float angle in AvoidanceAngles)
        {
            Vector3 rayDir = Quaternion.Euler(0f, angle, 0f) * transform.forward;
            if (!Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, rayRange) ||
                hit.collider.CompareTag("Dirt") || hit.collider.transform == roomArea.floorTransform)
                continue;

            if (angle == 0f && hit.distance < 0.45f)
            {
                actions[0] = ReverseLimit;
                actions[1] = localDir.x >= 0f ? 1f : -1f;
                return;
            }

            float proximity = (rayRange - hit.distance) / rayRange;
            obstacleRepulsion += hit.normal * (proximity * 2.2f);
        }

        Vector3 desiredDir = worldDir + obstacleRepulsion;
        desiredDir.y = 0f;
        desiredDir = desiredDir.sqrMagnitude > 0.001f ? desiredDir.normalized : worldDir;

        Vector3 desiredLocal = transform.InverseTransformDirection(desiredDir);
        float steerAngle = Mathf.Atan2(desiredLocal.x, desiredLocal.z) * Mathf.Rad2Deg;
        float angleSize = Mathf.Abs(steerAngle);
        float throttle = 1f;

        if (angleSize > 70f)
            throttle = 0.15f;
        else if (angleSize > 35f)
            throttle = 0.65f;

        if (isTransitMode && angleSize <= 60f)
            throttle = 1f;

        actions[0] = throttle;
        actions[1] = Mathf.Clamp(steerAngle / 35f, -1f, 1f);
    }
}
