using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class FifthMissionPassageCollapse : MonoBehaviour
{
    static readonly HashSet<string> TargetNames = new HashSet<string>
    {
        "Camping_FCP_RockCluster_Type1_Color1",
        "Camping_FCP_RockCluster_Type1_Color1 (1)",
        "Camping_FCP_RockCluster_Type1_Color1 (2)"
    };

    static bool collapseStarted;

    public static void CollapseNow()
    {
        if (collapseStarted) return;
        collapseStarted = true;
        FifthMissionPassageCollapse runner =
            Object.FindAnyObjectByType<FifthMissionPassageCollapse>(FindObjectsInactive.Include);
        if (runner == null)
            runner = new GameObject("FifthMissionPassageCollapse").AddComponent<FifthMissionPassageCollapse>();
        runner.StartCoroutine(runner.Collapse());
    }

    IEnumerator Collapse()
    {
        List<GameObject> rocks = new List<GameObject>();
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (Transform candidate in all)
        {
            if (candidate == null || !TargetNames.Contains(candidate.name))
                continue;
            if (!candidate.gameObject.scene.IsValid())
                continue;
            rocks.Add(candidate.gameObject);
        }
        Vector3 bridgePosition = AveragePosition(rocks);

        foreach (GameObject rock in rocks)
        {
            rock.SetActive(true);
            foreach (Collider collider in rock.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            BoxCollider box = rock.GetComponent<BoxCollider>();
            if (box == null) box = rock.AddComponent<BoxCollider>();
            FitCollider(rock.transform, box);
            box.enabled = true;

            Rigidbody body = rock.GetComponent<Rigidbody>();
            if (body == null) body = rock.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            body.mass = 260f;
            body.linearDamping = .42f;
            body.angularDamping = .16f;
            body.constraints = RigidbodyConstraints.None;

            Vector3 outward = Vector3.ProjectOnPlane(
                rock.transform.position - AveragePosition(rocks), Vector3.up).normalized;
            if (outward.sqrMagnitude < .01f)
                outward = Random.insideUnitSphere;
            outward.y = -.3f;
            body.AddForce((outward.normalized * 4.5f + Vector3.down * 2f) * body.mass,
                ForceMode.Impulse);
            body.AddTorque(Random.onUnitSphere * body.mass * 1.7f, ForceMode.Impulse);
            DarkEnergyVfx.PlayImpact(rock.transform.position + Vector3.up * .5f);
        }

        yield return new WaitForSeconds(4.5f);
        foreach (GameObject rock in rocks)
            if (rock != null) Destroy(rock);
        if (rocks.Count > 0)
            FifthMissionBridgeThoughtTrigger.Create(bridgePosition);
        Destroy(gameObject);
    }

    static Vector3 AveragePosition(List<GameObject> rocks)
    {
        if (rocks.Count == 0) return Vector3.zero;
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (GameObject rock in rocks)
        {
            if (rock == null) continue;
            sum += rock.transform.position;
            count++;
        }
        return count > 0 ? sum / count : Vector3.zero;
    }

    static void FitCollider(Transform root, BoxCollider collider)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            collider.center = Vector3.zero;
            collider.size = Vector3.one;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        collider.center = root.InverseTransformPoint(bounds.center);
        Vector3 scale = root.lossyScale;
        collider.size = new Vector3(
            bounds.size.x / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(.001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(.001f, Mathf.Abs(scale.z)));
    }
}
