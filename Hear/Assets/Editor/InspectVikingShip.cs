using UnityEditor;
using UnityEngine;

public static class InspectVikingShip
{
    [MenuItem("Tools/Inspect Viking Ship")]
    public static void Run()
    {
        var asset = Resources.Load<GameObject>("Worlds/VikingBoat/Models/VikingShip");
        if (asset == null)
        {
            Debug.LogError("[InspectVikingShip] Could not load VikingShip asset.");
            return;
        }

        Dump(asset.transform, 0);
    }

    private static void Dump(Transform t, int depth)
    {
        string indent = new string(' ', depth * 2);
        var mf = t.GetComponent<MeshFilter>();
        string meshInfo = "";
        if (mf != null && mf.sharedMesh != null)
        {
            var mesh = mf.sharedMesh;
            meshInfo = $" mesh='{mesh.name}' bounds={mesh.bounds} verts={mesh.vertexCount}";
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var matNames = System.Array.ConvertAll(mr.sharedMaterials, m => m != null ? m.name : "null");
                meshInfo += $" mats=[{string.Join(",", matNames)}]";
            }
        }
        Debug.Log($"[InspectVikingShip] {indent}{t.name} localPos={t.localPosition} localRot={t.localRotation.eulerAngles} localScale={t.localScale}{meshInfo}");

        for (int i = 0; i < t.childCount; i++)
            Dump(t.GetChild(i), depth + 1);
    }
}
