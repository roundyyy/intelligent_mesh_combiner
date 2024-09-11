using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class IntelligentMeshCombiner : EditorWindow
{
    private Transform parentObject;
    private float groupingRadius = 5f;
    private float subgroupRadius = 2f;
    private int triangleLimit = 10000;
    private List<Cluster> clusters = new List<Cluster>();
    private bool clustersBuilt = false;
    private bool showClusterList = false;
    private bool showInfoSection = false;
    private Vector2 scrollPosition;
    private bool rebuildLightmapUV = false;
    private bool addMeshCollider = false;
    private string newParentName = "ExampleGroup";
    private bool markCombinedStatic = true;
    private bool destroySourceObjects = false;

    private static readonly Color MainGroupColor = new Color(1f, 0.5f, 0f, 0.2f);
    private static readonly Color SubGroupColor = new Color(0f, 1f, 0.5f, 0.2f);
    private static readonly Color OverLimitColor = new Color(1f, 0f, 0f, 0.4f);

    private const string MeshSavePath = "Assets/IMC_Meshes";
    private Dictionary<Material, List<MeshRenderer>> materialGroups = new Dictionary<Material, List<MeshRenderer>>();

    private Dictionary<Material, Color> materialColors = new Dictionary<Material, Color>();

    private const int MaxRecursionDepth = 10;

    [MenuItem("Tools/IntelligentMeshCombiner")]
    public static void ShowWindow()
    {
        GetWindow<IntelligentMeshCombiner>("IntelligentMeshCombiner");
    }

    private void OnGUI()
    {
        GUILayout.Label("IntelligentMeshCombiner v0.1", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        parentObject = (Transform)EditorGUILayout.ObjectField("Parent Object", parentObject, typeof(Transform), true);
        if (EditorGUI.EndChangeCheck() && parentObject != null)
        {
            BuildClusters();
        }

        EditorGUI.BeginChangeCheck();
        groupingRadius = EditorGUILayout.Slider("Grouping Radius", groupingRadius, 0.1f, 100f);
        subgroupRadius = EditorGUILayout.Slider("Subgroup Radius", subgroupRadius, 0.1f, groupingRadius);
        triangleLimit = EditorGUILayout.IntSlider("Triangle Limit", triangleLimit, 1000, 100000);
        if (EditorGUI.EndChangeCheck())
        {
            BuildClusters();
        }

        rebuildLightmapUV = EditorGUILayout.Toggle("Rebuild Lightmap UV", rebuildLightmapUV);
        addMeshCollider = EditorGUILayout.Toggle("Add Mesh Collider", addMeshCollider);
        markCombinedStatic = EditorGUILayout.Toggle("Mark Combined Static", markCombinedStatic);
        destroySourceObjects = EditorGUILayout.Toggle("Destroy Source Objects", destroySourceObjects);
        newParentName = EditorGUILayout.TextField("New Parent Name", newParentName);

        EditorGUI.BeginDisabledGroup(parentObject == null);
        if (GUILayout.Button("Rebuild Clusters"))
        {
            BuildClusters();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(!clustersBuilt);
        if (GUILayout.Button("List Materials"))
        {
            ListMaterials();
        }
        if (GUILayout.Button("Group Objects Only"))
        {
            GroupObjects();
        }
        if (GUILayout.Button("Group and Combine Clusters"))
        {
            GroupAndCombineClusters();
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Undo Last Operation"))
        {
            Undo.PerformUndo();
        }


        GUILayout.Space(10);
        DisplayClusterInfo();

        GUILayout.Space(10);
        DisplayExpandedInfo();
    }

    private void DisplayClusterInfo()
    {
        if (clustersBuilt)
        {
            int totalObjects = clusters.Sum(c => c.Renderers.Count);
            int totalTriangles = clusters.Sum(c => c.TriangleCount);
            EditorGUILayout.LabelField($"Total Objects: {totalObjects}");
            EditorGUILayout.LabelField($"Total Triangles: {totalTriangles}");
            EditorGUILayout.LabelField($"Number of Clusters: {clusters.Count}");

            showClusterList = EditorGUILayout.Foldout(showClusterList, "Cluster List");
            if (showClusterList)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                for (int i = 0; i < clusters.Count; i++)
                {
                    Cluster cluster = clusters[i];
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Cluster {i + 1}:", GUILayout.Width(70));
                    EditorGUILayout.LabelField($"Objects: {cluster.Renderers.Count}", GUILayout.Width(80));

                    if (cluster.TriangleCount > triangleLimit)
                    {
                        GUI.color = Color.red;
                    }
                    EditorGUILayout.LabelField($"Triangles: {cluster.TriangleCount}", GUILayout.Width(100));
                    GUI.color = Color.white;

                    EditorGUILayout.LabelField(cluster.IsSubdivided ? $"Sub (Level {cluster.SubdivisionLevel})" : "Main", GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
        }
    }


    private void DisplayExpandedInfo()
    {
        showInfoSection = EditorGUILayout.Foldout(showInfoSection, "Tool Information");
        if (showInfoSection)
        {
            EditorGUILayout.HelpBox(
                "The Intelligent Mesh Combiner tool allows you to group and combine meshes based on their proximity and materials.\n\n" +
                "Features:\n" +
                "- Group objects within a specified radius\n" +
                "- Combine meshes while maintaining their original positions\n" +
                "- Automatically handle multiple materials\n" +
                "- Visualize clusters with material-specific colors\n" +
                "- Save combined meshes as assets in 'IMC_Meshes' folder\n" +
                "- Option to rebuild lightmap UVs for combined meshes\n" +
                "- Option to add mesh colliders to combined objects\n" +
                "- Mark combined objects as static\n" +
                "- Option to destroy source objects after combining\n" +
                  "Usage:\n" +
                "1. Select a parent object containing the meshes you want to group\n" +
                "2. Adjust the grouping radius and other parameters as needed\n" +
                "3. Click 'Rebuild Clusters' to analyze the objects\n" +
                "4. Use 'Group Objects' to group without combining, or 'Group and Combine Clusters' to both group and combine meshes\n" +
                "5. Review the generated clusters in the scene view, where different materials are represented by subtle color variations\n\n" +
                "Limitations:\n" +
                "- Skinned meshes are not supported for combination\n" +
                "- Particle systems and other non-mesh renderers are ignored\n\n" +
                "Note: Combined meshes are saved in the 'IMC_Meshes' folder in your project's Assets directory.",
                MessageType.Info
            );
        }
    }

    private void ListMaterials()
    {
        if (parentObject == null)
        {
            Debug.LogError("Please select a parent object first.");
            return;
        }

        materialGroups.Clear();
        MeshRenderer[] renderers = parentObject.GetComponentsInChildren<MeshRenderer>();

        foreach (MeshRenderer renderer in renderers)
        {
            Material mat = renderer.sharedMaterial;
            if (!materialGroups.ContainsKey(mat))
            {
                materialGroups[mat] = new List<MeshRenderer>();
            }
            materialGroups[mat].Add(renderer);
        }

        Debug.Log($"Found {materialGroups.Count} different materials:");
        foreach (var kvp in materialGroups)
        {
            Debug.Log($"- {kvp.Key.name}: {kvp.Value.Count} objects");
        }
    }

    private void BuildClusters()
    {
        if (parentObject == null)
        {
            clustersBuilt = false;
            clusters.Clear();
            SceneView.RepaintAll();
            return;
        }

        clusters.Clear();
        List<MeshRenderer> renderers = parentObject.GetComponentsInChildren<MeshRenderer>().ToList();

        while (renderers.Count > 0)
        {
            MeshRenderer currentRenderer = renderers[0];
            Cluster newCluster = new Cluster(currentRenderer);
            renderers.RemoveAt(0);

            for (int i = renderers.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(currentRenderer.transform.position, renderers[i].transform.position) <= groupingRadius
                    && currentRenderer.sharedMaterial == renderers[i].sharedMaterial)
                {
                    newCluster.AddRenderer(renderers[i]);
                    renderers.RemoveAt(i);
                }
            }

            newCluster.CalculateTriangles();
            clusters.AddRange(SubdivideClusterRecursive(newCluster, 0));
        }

        clustersBuilt = true;
        SceneView.RepaintAll();
        Repaint();
    }

    private void GroupAndCombineClusters()
    {
        if (!clustersBuilt)
        {
            Debug.LogError("Please build clusters first.");
            return;
        }

        GameObject newParent = new GameObject(newParentName);
        Undo.RegisterCreatedObjectUndo(newParent, "Create New Parent");
        newParent.transform.position = Vector3.zero;
        newParent.transform.localScale = Vector3.one;

        for (int i = 0; i < clusters.Count; i++)
        {
            Cluster cluster = clusters[i];
            string groupName = $"{newParentName}_Group{i + 1}_{cluster.Material.name}";
            Debug.Log($"Grouping and combining {cluster.Renderers.Count} objects into {groupName}");

            GameObject groupParent = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(groupParent, "Create Group Parent");
            groupParent.transform.SetParent(newParent.transform);
            groupParent.transform.localPosition = Vector3.zero;
            groupParent.transform.localScale = Vector3.one;

            GameObject combinedObject = new GameObject($"{groupName}_combined");
            Undo.RegisterCreatedObjectUndo(combinedObject, "Create Combined Object");
            combinedObject.transform.SetParent(groupParent.transform);
            combinedObject.transform.localPosition = Vector3.zero;
            combinedObject.transform.localScale = Vector3.one;

            if (markCombinedStatic)
            {
                combinedObject.isStatic = true;
            }

            GameObject sourceObjectsParent = new GameObject($"{groupName}_sources");
            Undo.RegisterCreatedObjectUndo(sourceObjectsParent, "Create Source Objects Parent");
            sourceObjectsParent.transform.SetParent(groupParent.transform);
            sourceObjectsParent.transform.localPosition = Vector3.zero;
            sourceObjectsParent.transform.localScale = Vector3.one;

            // Group source objects
            foreach (MeshRenderer renderer in cluster.Renderers)
            {
                Undo.SetTransformParent(renderer.transform, sourceObjectsParent.transform, "Group Source Object");
            }

            if (!CombineClusterMeshes(cluster, combinedObject))
            {
                Debug.LogWarning($"Failed to combine meshes for {groupName}. Objects are grouped but not combined.");
                continue;
            }

            // Handle source objects after successful combination
            foreach (MeshRenderer renderer in cluster.Renderers)
            {
                if (destroySourceObjects)
                {
                    Undo.DestroyObjectImmediate(renderer.gameObject);
                }
                else
                {
                    Undo.RecordObject(renderer.gameObject, "Disable Original Object");
                    renderer.gameObject.SetActive(false);
                }
            }

            if (destroySourceObjects)
            {
                Undo.DestroyObjectImmediate(sourceObjectsParent);
            }
        }

        clustersBuilt = false;
        clusters.Clear();
        SceneView.RepaintAll();
    }


    private bool CombineClusterMeshes(Cluster cluster, GameObject parent)
    {
        List<CombineInstance> combineInstances = new List<CombineInstance>();
        Material sharedMaterial = null;

        foreach (MeshRenderer renderer in cluster.Renderers)
        {
            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            if (sharedMaterial == null)
            {
                sharedMaterial = renderer.sharedMaterial;
            }
            else if (sharedMaterial != renderer.sharedMaterial)
            {
                Debug.LogWarning($"Different materials found in cluster {parent.name}. Skipping combination.");
                return false;
            }

            CombineInstance ci = new CombineInstance();
            ci.mesh = meshFilter.sharedMesh;
            ci.transform = meshFilter.transform.localToWorldMatrix;
            combineInstances.Add(ci);
        }

        if (combineInstances.Count == 0)
        {
            return false;
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

        if (rebuildLightmapUV)
        {
            Unwrapping.GenerateSecondaryUVSet(combinedMesh);
        }

        // Adjust vertices to set the pivot at the center of the bounds
        Vector3 centerOffset = combinedMesh.bounds.center;
        Vector3[] vertices = combinedMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] -= centerOffset;
        }
        combinedMesh.vertices = vertices;
        combinedMesh.RecalculateBounds();

        // Set the parent's position to the center offset
        parent.transform.position = centerOffset;

        // Save the combined mesh as an asset
        SaveMeshAsset(combinedMesh, parent.name);

        // Add components to the parent object
        MeshFilter newMeshFilter = parent.AddComponent<MeshFilter>();
        newMeshFilter.sharedMesh = combinedMesh;

        MeshRenderer newRenderer = parent.AddComponent<MeshRenderer>();
        newRenderer.sharedMaterial = sharedMaterial;

        if (addMeshCollider)
        {
            MeshCollider collider = parent.AddComponent<MeshCollider>();
            collider.sharedMesh = combinedMesh;
        }

        return true;
    }


    private void SaveMeshAsset(Mesh mesh, string objectName)
    {
        if (!Directory.Exists(MeshSavePath))
        {
            Directory.CreateDirectory(MeshSavePath);
        }

        string assetPath = $"{MeshSavePath}/{objectName}_combined.asset";
        AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(assetPath));
        AssetDatabase.SaveAssets();
    }
    private List<Cluster> SubdivideClusterRecursive(Cluster cluster, int level)
    {
        if (level >= MaxRecursionDepth)
        {
            Debug.LogWarning($"Maximum recursion depth reached for cluster. Some objects may exceed the triangle limit.");
            return new List<Cluster> { cluster };
        }

        if (cluster.TriangleCount <= triangleLimit)
        {
            return new List<Cluster> { cluster };
        }

        List<Cluster> subclusters = new List<Cluster>();
        List<MeshRenderer> remainingRenderers = new List<MeshRenderer>(cluster.Renderers);

        while (remainingRenderers.Count > 0)
        {
            MeshRenderer currentRenderer = remainingRenderers[0];
            Cluster newSubcluster = new Cluster(currentRenderer, true, level + 1);
            remainingRenderers.RemoveAt(0);

            for (int i = remainingRenderers.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(currentRenderer.transform.position, remainingRenderers[i].transform.position) <= subgroupRadius)
                {
                    newSubcluster.AddRenderer(remainingRenderers[i]);
                    remainingRenderers.RemoveAt(i);
                }
            }

            newSubcluster.CalculateTriangles();
            subclusters.AddRange(SubdivideClusterRecursive(newSubcluster, level + 1));
        }

        return subclusters;
    }

    private void GroupObjects()
    {
        if (!clustersBuilt)
        {
            Debug.LogError("Please build clusters first.");
            return;
        }

        // Create the new parent at the root level
        GameObject newParent = new GameObject(newParentName);
        Undo.RegisterCreatedObjectUndo(newParent, "Create New Parent");
        newParent.transform.position = Vector3.zero;
        newParent.transform.localScale = Vector3.one;

        for (int i = 0; i < clusters.Count; i++)
        {
            Cluster cluster = clusters[i];
            string groupName = $"{newParentName}_Group{i + 1}";

            GameObject groupParent = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(groupParent, "Create Group Parent");
            groupParent.transform.SetParent(newParent.transform);
            groupParent.transform.position = cluster.Center;

            foreach (MeshRenderer renderer in cluster.Renderers)
            {
                Undo.SetTransformParent(renderer.transform, groupParent.transform, "Set Parent");
            }
        }

        clustersBuilt = false;
        clusters.Clear();
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!clustersBuilt) return;

        foreach (Cluster cluster in clusters)
        {
            Color color;
            if (cluster.TriangleCount > triangleLimit)
            {
                color = OverLimitColor;
            }
            else if (cluster.IsSubdivided)
            {
                color = SubGroupColor;
            }
            else
            {
                // For main groups, use the material-specific color
                color = GetMaterialColor(cluster.Material);
            }

            Handles.color = color;

            float radius = cluster.IsSubdivided ? subgroupRadius : groupingRadius;
            Handles.SphereHandleCap(0, cluster.Center, Quaternion.identity, radius * 2, EventType.Repaint);

            Handles.color = cluster.TriangleCount > triangleLimit ? Color.red : (cluster.IsSubdivided ? Color.green : Color.yellow);
            foreach (MeshRenderer renderer in cluster.Renderers)
            {
                Handles.DrawLine(cluster.Center, renderer.transform.position);
            }
        }
    }

    private Color GetMaterialColor(Material material)
    {
        if (!materialColors.TryGetValue(material, out Color color))
        {
            // Generate a new color based on the MainGroupColor
            Color.RGBToHSV(MainGroupColor, out float h, out float s, out float v);

            // Use the material's instance ID to generate a unique but consistent hue offset
            float hueOffset = (material.GetInstanceID() * 0.618034f) % 1f; // Golden ratio for even distribution
            hueOffset = (hueOffset - 0.5f) * 0.2f; // Limit the hue change to ±0.1

            // Adjust the hue
            h = Mathf.Repeat(h + hueOffset, 1f);

            // Slightly vary saturation and value
            s = Mathf.Clamp01(s + (hueOffset * 0.2f));
            v = Mathf.Clamp01(v - (hueOffset * 0.1f));

            color = Color.HSVToRGB(h, s, v);
            color.a = MainGroupColor.a; // Keep the original alpha

            materialColors[material] = color;
        }
        return color;
    }


    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private class Cluster
    {
        public List<MeshRenderer> Renderers { get; private set; } = new List<MeshRenderer>();
        public Vector3 Center { get; private set; }
        public int TriangleCount { get; private set; }
        public bool IsSubdivided { get; private set; }
        public int SubdivisionLevel { get; private set; }
        public Material Material { get; private set; }

        public Cluster(MeshRenderer initialRenderer, bool isSubdivided = false, int subdivisionLevel = 0)
        {
            AddRenderer(initialRenderer);
            IsSubdivided = isSubdivided;
            SubdivisionLevel = subdivisionLevel;
            Material = initialRenderer.sharedMaterial;
        }

        public void AddRenderer(MeshRenderer renderer)
        {
            if (Material == null)
            {
                Material = renderer.sharedMaterial;
            }
            else if (Material != renderer.sharedMaterial)
            {
                Debug.LogWarning("Attempting to add a renderer with a different material to the cluster.");
                return;
            }

            Renderers.Add(renderer);
            RecalculateCenter();
        }

        private void RecalculateCenter()
        {
            Vector3 sum = Vector3.zero;
            foreach (MeshRenderer renderer in Renderers)
            {
                sum += renderer.transform.position;
            }
            Center = sum / Renderers.Count;
        }

        public void CalculateTriangles()
        {
            TriangleCount = 0;
            foreach (MeshRenderer renderer in Renderers)
            {
                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    TriangleCount += meshFilter.sharedMesh.triangles.Length / 3;
                }
            }
        }
    }
}