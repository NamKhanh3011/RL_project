using UnityEngine;

/// <summary>
/// Represents an individual dirt / dust node on the floor.
/// Spun continuously for visual flair and collected when the vacuum robot touches it.
/// </summary>
public class DirtItem : MonoBehaviour
{
    private RoomArea roomArea;
    public float spinSpeed = 90f;

    public void Init(RoomArea area)
    {
        roomArea = area;
    }

    void Update()
    {
        // Continuous slow rotation like in the original game
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<VacuumAgent>() != null)
        {
            if (roomArea != null)
            {
                roomArea.CollectDirt(this);
            }
            Destroy(gameObject);
        }
    }
}
