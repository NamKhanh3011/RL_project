using UnityEngine;

public class DirtItem : MonoBehaviour
{
    private RoomArea roomArea;
    public float spinSpeed = 90f;

    public void Init(RoomArea area)
    {
        roomArea = area;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") && !other.TryGetComponent<VacuumAgent>(out _))
            return;

        if (roomArea != null)
            roomArea.CollectDirt(this);
        else
            Destroy(gameObject);
    }
}
