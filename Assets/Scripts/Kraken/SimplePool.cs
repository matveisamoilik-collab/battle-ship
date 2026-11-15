using System.Collections.Generic;
using UnityEngine;

// Minimal GameObject pool: prewarmed, objects are deactivated while free. No allocations after
// Prewarm unless the pool runs dry (then it grows by one).
public class SimplePool
{
    readonly GameObject        prefab;
    readonly Transform         parent;
    readonly Stack<GameObject> free = new Stack<GameObject>();

    public SimplePool(GameObject prefab, Transform parent, int prewarm)
    {
        this.prefab = prefab;
        this.parent = parent;
        for (int i = 0; i < prewarm; i++) free.Push(Create());
    }

    GameObject Create()
    {
        var go = Object.Instantiate(prefab, parent);
        go.SetActive(false);
        return go;
    }

    public GameObject Get(Vector3 pos, Quaternion rot)
    {
        var go = free.Count > 0 ? free.Pop() : Create();
        if (go == null) go = Create();   // destroyed externally
        go.transform.SetPositionAndRotation(pos, rot);
        go.SetActive(true);
        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null || !go.activeSelf) return;
        go.SetActive(false);
        free.Push(go);
    }
}
