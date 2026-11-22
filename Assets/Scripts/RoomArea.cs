using System.Collections.Generic;
using UnityEngine;

public class RoomArea : MonoBehaviour
{
    [Header("Environment Dimensions")]
    public Transform floorTransform;

    [Header("Dirt Settings")]
    public GameObject dirtPrefab;
    public float gridSpacing = 0.95f;
    public float dirtHeight = 0.08f;
    public float obstacleCheckRadius = 0.35f;

    [Header("Connected Components")]
    public VacuumAgent agent;
    public SimulationHUD hud;

    [Header("Stats Tracking")]
    public int episodeNumber;
    public float elapsedTime;
    public float bestTime;

    public const float MAX_EPISODE_TIME = 240f;
    private const float DirtRewardTotal = 15f;
    private const float CleanRoomBonus = 5f;
    private const float SpawnClearance = 0.65f;
    private readonly List<DirtItem> activeDirts = new List<DirtItem>();
    private int initialDirtCount;

    private void Start()
    {
        if (activeDirts.Count == 0)
            ResetEpisode();
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;
        if (agent == null) return;

        if (elapsedTime >= MAX_EPISODE_TIME && activeDirts.Count > 0)
            agent.EndEpisode();

        if (hud != null)
        {
            hud.UpdateDisplay(episodeNumber, agent.StepCount, elapsedTime, bestTime,
                agent.GetCumulativeReward(), activeDirts.Count);
        }
    }

    public void ResetEpisode()
    {
        episodeNumber++;
        elapsedTime = 0f;

        foreach (DirtItem dirt in activeDirts)
        {
            if (dirt != null)
                Destroy(dirt.gameObject);
        }
        activeDirts.Clear();

        SpawnDirtGrid();
        initialDirtCount = activeDirts.Count;
    }

    private void SpawnDirtGrid()
    {
        if (floorTransform == null || dirtPrefab == null) return;

        Bounds bounds = floorTransform.GetComponent<Renderer>().bounds;
        float y = bounds.max.y + dirtHeight;

        for (float x = bounds.min.x + SpawnClearance; x <= bounds.max.x - SpawnClearance; x += gridSpacing)
        {
            for (float z = bounds.min.z + SpawnClearance; z <= bounds.max.z - SpawnClearance; z += gridSpacing)
            {
                Vector3 position = new Vector3(x, y, z);
                if (IsInaccessible(x, z) || HasNearbyObstacle(position))
                    continue;

                GameObject dirtObject = Instantiate(dirtPrefab, position, Quaternion.identity, transform);
                dirtObject.SetActive(true);
                if (!dirtObject.TryGetComponent(out DirtItem dirt))
                    dirt = dirtObject.AddComponent<DirtItem>();

                dirt.Init(this);
                activeDirts.Add(dirt);
            }
        }
    }

    private static bool IsInaccessible(float x, float z)
    {
        return (x >= -1f && x <= 2.6f && z >= 2f) || // Dining table and chairs
               (x >= 3.6f && z <= 1.2f) ||           // Sofa
               (x >= 1.8f && x <= 3.2f && z >= -1.7f && z <= 0.5f) || // Coffee table
               (x <= -1.8f && z >= 3f) ||           // Bookshelf and lamp
               (x >= 3f && z >= 3f) ||              // Back-right plant
               (x <= -4.2f && z >= -0.2f && z <= 1.8f); // Left plant
    }

    private bool HasNearbyObstacle(Vector3 position)
    {
        foreach (Collider collider in Physics.OverlapSphere(position, SpawnClearance))
        {
            if (collider.transform == floorTransform || collider.CompareTag("Dirt"))
                continue;

            if (collider.CompareTag("Obstacle") || collider.CompareTag("Wall"))
                return true;

            string name = collider.name;
            if (name.Contains("Chair") || name.Contains("Table") || name.Contains("Sofa") ||
                name.Contains("Leg") || name.Contains("Wall") || name.Contains("Shelf") ||
                name.Contains("Lamp") || name.Contains("Plant"))
                return true;
        }

        return false;
    }

    public void CollectDirt(DirtItem dirt)
    {
        // Both colliders can report the same pickup before Unity destroys the object.
        if (!activeDirts.Remove(dirt)) return;
        Destroy(dirt.gameObject);

        if (agent != null && initialDirtCount > 0)
            agent.AddReward(DirtRewardTotal / initialDirtCount);

        if (activeDirts.Count > 0) return;

        if (bestTime == 0f || elapsedTime < bestTime)
            bestTime = elapsedTime;

        Debug.Log($"<color=green><b>[Roomba Simulation] 100% ROOM CLEANED!</b></color> Time: {elapsedTime:F1}s | Best: {bestTime:F1}s | Episode: {episodeNumber}");

        if (agent != null)
        {
            agent.AddReward(CleanRoomBonus);
            agent.EndEpisode();
        }
    }

    public float GetDirtRatio()
    {
        return initialDirtCount > 0 ? (float)activeDirts.Count / initialDirtCount : 0f;
    }

    public bool GetNearestDirtInfo(Transform agentTransform, out Vector3 localDir, out Vector3 worldDir, out float distance)
    {
        return GetNearestDirtInfo(agentTransform, out localDir, out worldDir, out distance, out _);
    }

    public bool GetNearestDirtInfo(Transform agentTransform, out Vector3 localDir, out Vector3 worldDir, out float distance, out bool isTransitMode)
    {
        localDir = Vector3.forward;
        worldDir = agentTransform.forward;
        distance = 15f;
        isTransitMode = false;

        Vector3 agentPos = agentTransform.position;
        DirtItem closestDirt = null;
        float minDistance = float.MaxValue;

        foreach (DirtItem dirt in activeDirts)
        {
            if (dirt == null) continue;

            float candidateDistance = OffsetToDirt(dirt, agentPos).magnitude;
            if (candidateDistance >= minDistance) continue;

            minDistance = candidateDistance;
            closestDirt = dirt;
        }

        if (closestDirt == null) return false;

        isTransitMode = minDistance > 2.2f && activeDirts.Count > 1;
        DirtItem target = isTransitMode
            ? FindDensestCluster(closestDirt)
            : FindLocalTarget(closestDirt, agentPos, worldDir, minDistance + 0.9f);

        Vector3 offset = OffsetToDirt(target, agentPos);
        distance = offset.magnitude;
        worldDir = offset.normalized;
        localDir = agentTransform.InverseTransformDirection(worldDir);
        return true;
    }

    private DirtItem FindLocalTarget(DirtItem target, Vector3 agentPos, Vector3 forward, float maxDistance)
    {
        float bestScore = float.MaxValue;
        foreach (DirtItem dirt in activeDirts)
        {
            if (dirt == null) continue;

            Vector3 offset = OffsetToDirt(dirt, agentPos);
            float distance = offset.magnitude;
            if (distance > maxDistance) continue;

            float anglePenalty = (1f - Vector3.Dot(forward, offset.normalized)) * 0.4f;
            float score = distance + anglePenalty;
            if (score >= bestScore) continue;

            bestScore = score;
            target = dirt;
        }

        return target;
    }

    private DirtItem FindDensestCluster(DirtItem target)
    {
        const float neighborRadiusSquared = 7.84f; // 2.8 m squared
        int maxNeighbors = -1;
        foreach (DirtItem dirt in activeDirts)
        {
            if (dirt == null) continue;

            Vector3 position = dirt.transform.position;
            int neighbors = 0;
            foreach (DirtItem other in activeDirts)
            {
                if (other != null && other != dirt && (position - other.transform.position).sqrMagnitude < neighborRadiusSquared)
                    neighbors++;
            }

            if (neighbors <= maxNeighbors) continue;

            maxNeighbors = neighbors;
            target = dirt;
        }

        return target;
    }

    private static Vector3 OffsetToDirt(DirtItem dirt, Vector3 from)
    {
        Vector3 offset = dirt.transform.position - from;
        offset.y = 0f;
        return offset;
    }
}
