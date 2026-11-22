using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the room environment, floor bounds, procedural dirt spawning,
/// episode stats, and < 60s speed challenge.
/// </summary>
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
    public int episodeNumber = 0;
    public float elapsedTime = 0f;
    public float bestTime = 0f;

    // Episode timeout set to 4.0 minutes (240 seconds: comfortable 3-4 minute window)
    public const float MAX_EPISODE_TIME = 240f;

    private List<DirtItem> activeDirts = new List<DirtItem>();
    private int initialDirtCount = 0;

    void Start()
    {
        if (activeDirts.Count == 0)
        {
            ResetEpisode();
        }
    }

    void Update()
    {
        elapsedTime += Time.deltaTime;

        // If time runs over 60 seconds without cleaning all dirt -> Timeout restart
        if (elapsedTime >= MAX_EPISODE_TIME && activeDirts.Count > 0)
        {
            if (agent != null)
            {
                agent.EndEpisode(); // Forces AI to learn that it MUST finish under 60 seconds!
            }
        }

        if (hud != null && agent != null)
        {
            hud.UpdateDisplay(
                episodeNumber,
                agent.StepCount,
                elapsedTime,
                bestTime,
                agent.GetCumulativeReward(),
                activeDirts.Count
            );
        }
    }

    public void ResetEpisode()
    {
        episodeNumber++;
        elapsedTime = 0f;

        // Clean up remaining dirt
        for (int i = activeDirts.Count - 1; i >= 0; i--)
        {
            if (activeDirts[i] != null)
            {
                Destroy(activeDirts[i].gameObject);
            }
        }
        activeDirts.Clear();

        // Spawn uniform grid of dirt
        SpawnDirtGrid();
        initialDirtCount = activeDirts.Count;
    }

    private void SpawnDirtGrid()
    {
        if (floorTransform == null || dirtPrefab == null) return;

        Renderer floorRenderer = floorTransform.GetComponent<Renderer>();
        Bounds bounds = floorRenderer.bounds;

        float startX = bounds.min.x + 0.65f;
        float endX = bounds.max.x - 0.65f;
        float startZ = bounds.min.z + 0.65f;
        float endZ = bounds.max.z - 0.65f;

        for (float x = startX; x <= endX; x += gridSpacing)
        {
            for (float z = startZ; z <= endZ; z += gridSpacing)
            {
                // Exclude narrow/inaccessible areas where dirt could get trapped:
                // 1. Under dining table, legs, and chairs (X: -1.0 to 2.6, Z >= 2.0)
                if (x >= -1.0f && x <= 2.6f && z >= 2.0f) continue;
                // 2. Behind and under sofa
                if (x >= 3.6f && z <= 1.2f) continue;
                // 3. Under coffee table
                if (x >= 1.8f && x <= 3.2f && z >= -1.7f && z <= 0.5f) continue;
                // 4. Back-left corner (Bookshelf & Floor lamp)
                if (x <= -1.8f && z >= 3.0f) continue;
                // 5. Back-right corner (Potted plant & Wall)
                if (x >= 3.0f && z >= 3.0f) continue;
                // 6. Left potted plant
                if (x <= -4.2f && z >= -0.2f && z <= 1.8f) continue;

                Vector3 spawnPos = new Vector3(x, bounds.max.y + dirtHeight, z);

                // Overlap check with safe 0.65m margin from all obstacles
                Collider[] colliders = Physics.OverlapSphere(spawnPos, 0.65f);
                bool isBlocked = false;

                foreach (var col in colliders)
                {
                    if (col.transform == floorTransform) continue; // Ignore the floor!
                    if (col.CompareTag("Dirt")) continue;

                    if (col.CompareTag("Obstacle") || col.CompareTag("Wall") ||
                        col.name.Contains("Chair") || col.name.Contains("Table") ||
                        col.name.Contains("Sofa") || col.name.Contains("Leg") ||
                        col.name.Contains("Wall") || col.name.Contains("Shelf") ||
                        col.name.Contains("Lamp") || col.name.Contains("Plant"))
                    {
                        isBlocked = true;
                        break;
                    }
                }

                if (!isBlocked)
                {
                    GameObject dirtObj = Instantiate(dirtPrefab, spawnPos, Quaternion.identity, transform);
                    dirtObj.SetActive(true);
                    DirtItem dirt = dirtObj.GetComponent<DirtItem>();
                    if (dirt == null) dirt = dirtObj.AddComponent<DirtItem>();
                    dirt.Init(this);
                    activeDirts.Add(dirt);
                }
            }
        }
    }

    public void CollectDirt(DirtItem dirt)
    {
        activeDirts.Remove(dirt);
        if (agent != null && initialDirtCount > 0)
        {
            // Exactly 15.0 points total divided across all dirt items
            float rewardPerDirt = 15.0f / initialDirtCount;
            agent.AddReward(rewardPerDirt);
        }

        // Win Condition: 100% of room cleaned!
        if (activeDirts.Count == 0)
        {
            if (bestTime == 0f || elapsedTime < bestTime)
            {
                bestTime = elapsedTime;
            }

            Debug.Log($"<color=green><b>[Roomba Simulation] 100% ROOM CLEANED!</b></color> Time: {elapsedTime:F1}s | Best: {bestTime:F1}s | Episode: {episodeNumber}");

            if (agent != null)
            {
                // Flat win bonus +5.0 points -> Total = Exactly 20.0 max!
                agent.AddReward(5.0f);
                agent.EndEpisode();
            }
        }
    }

    public float GetDirtRatio()
    {
        if (initialDirtCount == 0) return 0f;
        return (float)activeDirts.Count / initialDirtCount;
    }

    /// <summary>
    /// Finds the optimal next dirt item:
    /// - If nearby dirt exists, sweeps local dirts efficiently.
    /// - If current zone is cleaned (nearest dirt > 2.2m), enters Fast Transit to the richest cluster!
    /// </summary>
    public bool GetNearestDirtInfo(Transform agentTransform, out Vector3 localDir, out Vector3 worldDir, out float distance, out bool isTransitMode)
    {
        localDir = Vector3.forward;
        worldDir = agentTransform.forward;
        distance = 15f;
        isTransitMode = false;

        if (activeDirts.Count == 0) return false;

        Vector3 agentPos = agentTransform.position;
        Vector3 forward = agentTransform.forward;

        // 1. Find true closest dirt by Euclidean distance
        DirtItem closestDirt = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < activeDirts.Count; i++)
        {
            if (activeDirts[i] == null) continue;
            Vector3 diff = activeDirts[i].transform.position - agentPos;
            diff.y = 0f;
            float dist = diff.magnitude;

            if (dist < minDistance)
            {
                minDistance = dist;
                closestDirt = activeDirts[i];
            }
        }

        if (closestDirt == null) return false;

        DirtItem targetDirt = closestDirt;

        // 2. If current zone is cleaned out (nearest dirt > 2.2m), transit fast to the richest dirt cluster!
        if (minDistance > 2.2f && activeDirts.Count > 1)
        {
            isTransitMode = true;

            // Find dirt with the most neighbor dirts within 2.8m (richest pocket of reward)
            DirtItem bestClusterDirt = closestDirt;
            int maxNeighbors = -1;

            for (int i = 0; i < activeDirts.Count; i++)
            {
                if (activeDirts[i] == null) continue;
                Vector3 p1 = activeDirts[i].transform.position;
                int neighbors = 0;

                for (int j = 0; j < activeDirts.Count; j++)
                {
                    if (i == j || activeDirts[j] == null) continue;
                    Vector3 p2 = activeDirts[j].transform.position;
                    if ((p1 - p2).sqrMagnitude < 7.84f) // within 2.8m
                    {
                        neighbors++;
                    }
                }

                if (neighbors > maxNeighbors)
                {
                    maxNeighbors = neighbors;
                    bestClusterDirt = activeDirts[i];
                }
            }

            targetDirt = bestClusterDirt;
        }
        else
        {
            // Local sweeping: among nearby candidates (within minDistance + 0.9m), prefer forward heading
            float bestLocalScore = float.MaxValue;
            for (int i = 0; i < activeDirts.Count; i++)
            {
                if (activeDirts[i] == null) continue;
                Vector3 diff = activeDirts[i].transform.position - agentPos;
                diff.y = 0f;
                float d = diff.magnitude;

                if (d <= minDistance + 0.9f)
                {
                    float anglePenalty = (1.0f - Vector3.Dot(forward, diff.normalized)) * 0.4f;
                    float score = d + anglePenalty;
                    if (score < bestLocalScore)
                    {
                        bestLocalScore = score;
                        targetDirt = activeDirts[i];
                    }
                }
            }
        }

        Vector3 targetDiff = targetDirt.transform.position - agentPos;
        targetDiff.y = 0f;
        distance = targetDiff.magnitude;
        worldDir = targetDiff.normalized;
        localDir = agentTransform.InverseTransformDirection(worldDir);
        return true;
    }

    public bool GetNearestDirtInfo(Transform agentTransform, out Vector3 localDir, out Vector3 worldDir, out float distance)
    {
        return GetNearestDirtInfo(agentTransform, out localDir, out worldDir, out distance, out _);
    }
}
