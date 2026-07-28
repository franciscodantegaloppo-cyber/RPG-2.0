using UnityEngine;

public class VillageLayout : MonoBehaviour
{
    [Header("House Prefabs")]
    [SerializeField] GameObject[] housePrefabs;

    [Header("Layout")]
    [SerializeField] int houseCount = 8;
    [SerializeField] float ringRadius = 15f;

    [ContextMenu("Place Houses In Ring")]
    public void PlaceHousesInRing()
    {
        // Remove existing children tagged as houses
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        if (housePrefabs == null || housePrefabs.Length == 0)
        {
            Debug.LogWarning("VillageLayout: No house prefabs assigned.");
            return;
        }

        for (int i = 0; i < houseCount; i++)
        {
            float angle = i * (360f / houseCount) * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(
                Mathf.Sin(angle) * ringRadius,
                0f,
                Mathf.Cos(angle) * ringRadius
            );

            GameObject prefab = housePrefabs[i % housePrefabs.Length];
            var house = Instantiate(prefab, transform.position + pos, Quaternion.identity, transform);

            // Face toward center
            Vector3 toCenter = (transform.position - house.transform.position).normalized;
            toCenter.y = 0;
            if (toCenter != Vector3.zero)
                house.transform.rotation = Quaternion.LookRotation(toCenter);

            house.name = $"House_{i + 1}";
        }

        Debug.Log($"VillageLayout: Placed {houseCount} houses.");
    }
}
