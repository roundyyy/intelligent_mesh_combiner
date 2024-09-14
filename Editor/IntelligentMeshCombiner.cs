using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

public class IntelligentMeshCombiner : EditorWindow
{
    private List<Transform> parentObjects = new List<Transform>();
    private float groupingRadius = 5f;
    private float subgroupRadius = 2f;
    private int triangleLimit = 10000;
    private List<Cluster> clusters = new List<Cluster>();
    private bool clustersBuilt = false;
    private bool showClusterList = false;
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

    private enum ClusteringAlgorithm
    {
        ProximityBased,
        KMeans
    }

    private ClusteringAlgorithm selectedAlgorithm = ClusteringAlgorithm.KMeans;
    private int kClusters = 5;
    private int kMeansIterations = 10;

    private float gizmoSphereOpacity = 0.2f;
    private bool rebuildNormals = false;

    // Filter variables
    private bool onlyStatic = false;
    private bool onlyActive = true;
    private bool onlyActiveMeshRenderers = true;
    private bool useTag = false;
    private string tagToUse = "Untagged";
    private bool useLayer = false;
    private int layerToUse = 0;
    private bool useNameContains = false;
    private string nameContainsString = "";

    // Material listing variables
    private List<Material> foundMaterials = new List<Material>();
    private bool showMaterialList = false;
    private Vector2 materialScrollPosition;

    // Global scroll position for the entire GUI
    private Vector2 globalScrollPosition;

    // Separate foldout states
    private bool showOptions = false;
    private bool showFilters = false;
    private bool showVisualizationSettings = false;
    private bool showToolInformation = false;

    private bool drawLines = true;

    // LOD Group variables
    private enum LODHandlingOption
    {
        CombineLodsSeparately,
        CombineAll
    }

    private LODHandlingOption lodHandlingOption = LODHandlingOption.CombineLodsSeparately;
    private bool lodGroupsDetected = false;

    [MenuItem("Tools/Intelligent Mesh Combiner")]
    public static void ShowWindow()
    {
        GetWindow<IntelligentMeshCombiner>("Intelligent Mesh Combiner");
    }

    private void OnGUI()
    {
        // Begin global scroll view
        globalScrollPosition = EditorGUILayout.BeginScrollView(globalScrollPosition);

        // Custom styles
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter
        };

        GUIStyle headerStyle = new GUIStyle(EditorStyles.foldout)
        {
            fontStyle = FontStyle.Bold
        };

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold
        };

        // Title Label
        GUILayout.Space(10);
        GUILayout.Label("Intelligent Mesh Combiner v0.4", titleStyle);

        GUILayout.Space(5);

        // Tool Information Section
        if (GUILayout.Button("Instructions", GUILayout.Width(100)))
        {
            ToolInstructionsWindow.ShowWindow();
        }
        GUILayout.Space(10);
        // Parent Objects Section
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Parent Objects", EditorStyles.boldLabel);

        for (int i = 0; i < parentObjects.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            parentObjects[i] = (Transform)EditorGUILayout.ObjectField($"Parent Object {i + 1}", parentObjects[i], typeof(Transform), true);
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                parentObjects.RemoveAt(i);
                i--;
                continue;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Parent", buttonStyle))
        {
            parentObjects.Add(null);
        }
        if (parentObjects.Count > 0 && GUILayout.Button("Clear Parent List", buttonStyle))
        {
            parentObjects.Clear();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // Detect LOD Groups
        DetectLODGroups();

        // LOD Handling Option
        if (lodGroupsDetected)
        {
            EditorGUILayout.HelpBox("LOD Groups detected in the selected objects.", MessageType.Warning);
            lodHandlingOption = (LODHandlingOption)EditorGUILayout.EnumPopup("LOD Handling Option", lodHandlingOption);
        }

        // Clustering Settings Section
        EditorGUI.BeginChangeCheck(); // Begin checking for changes
        selectedAlgorithm = (ClusteringAlgorithm)EditorGUILayout.EnumPopup("Clustering Algorithm", selectedAlgorithm);

        if (selectedAlgorithm == ClusteringAlgorithm.KMeans)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("K-Means Settings", EditorStyles.boldLabel);
            kClusters = EditorGUILayout.IntSlider("Number of Clusters (K)", kClusters, 2, 50);
            kMeansIterations = EditorGUILayout.IntSlider("Max Iterations", kMeansIterations, 5, 100);
            EditorGUILayout.EndVertical();
        }
        else if (selectedAlgorithm == ClusteringAlgorithm.ProximityBased)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Proximity-Based Settings", EditorStyles.boldLabel);
            groupingRadius = EditorGUILayout.Slider("Grouping Radius", groupingRadius, 0.1f, 200f);
            subgroupRadius = EditorGUILayout.Slider("Subgroup Radius", subgroupRadius, 0.1f, groupingRadius);
            EditorGUILayout.EndVertical();
        }

        triangleLimit = EditorGUILayout.IntSlider("Triangle Limit", triangleLimit, 1000, 200000);

        // Check if any changes were made and rebuild clusters if necessary
        if (EditorGUI.EndChangeCheck() && parentObjects != null && parentObjects.Count > 0)
        {
            BuildClusters();
        }

        GUILayout.Space(5);

        // Options Section
        showOptions = EditorGUILayout.BeginFoldoutHeaderGroup(showOptions, "Options");
        if (showOptions)
        {
            EditorGUILayout.BeginVertical("box");
            rebuildLightmapUV = EditorGUILayout.Toggle("Rebuild Lightmap UV", rebuildLightmapUV);
            rebuildNormals = EditorGUILayout.Toggle("Rebuild Normals", rebuildNormals);
            addMeshCollider = EditorGUILayout.Toggle("Add Mesh Collider", addMeshCollider);
            markCombinedStatic = EditorGUILayout.Toggle("Mark Combined Static", markCombinedStatic);
            destroySourceObjects = EditorGUILayout.Toggle("Destroy Source Objects", destroySourceObjects);
            newParentName = EditorGUILayout.TextField("New Parent Name", newParentName);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Filters Section
        showFilters = EditorGUILayout.BeginFoldoutHeaderGroup(showFilters, "Filters");
        if (showFilters)
        {
            EditorGUILayout.BeginVertical("box");
            onlyStatic = EditorGUILayout.Toggle("Only Static", onlyStatic);
            onlyActive = EditorGUILayout.Toggle("Only Active", onlyActive);
            onlyActiveMeshRenderers = EditorGUILayout.Toggle("Only Active Mesh Renderers", onlyActiveMeshRenderers);
            useTag = EditorGUILayout.Toggle("Use Tag", useTag);
            if (useTag)
            {
                tagToUse = EditorGUILayout.TagField("Tag to Use", tagToUse);
            }
            useLayer = EditorGUILayout.Toggle("Use Layer", useLayer);
            if (useLayer)
            {
                layerToUse = EditorGUILayout.LayerField("Layer to Use", layerToUse);
            }
            useNameContains = EditorGUILayout.Toggle("Use Name Contains", useNameContains);
            if (useNameContains)
            {
                nameContainsString = EditorGUILayout.TextField("Name Contains", nameContainsString);
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Visualization Settings Section
        showVisualizationSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showVisualizationSettings, "Visualization Settings");
        if (showVisualizationSettings)
        {
            EditorGUILayout.BeginVertical("box");
            gizmoSphereOpacity = EditorGUILayout.Slider("Gizmo Sphere Opacity", gizmoSphereOpacity, 0.00f, 1f);
            EditorGUILayout.HelpBox("Gizmos must be enabled in the Scene view to see cluster gizmos.", MessageType.Info);
            drawLines = EditorGUILayout.Toggle("Draw Lines", drawLines);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        GUILayout.Space(5);

        // Action Buttons

        // First line: Rebuild Clusters and List Materials
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(parentObjects == null || parentObjects.Count == 0);
        if (GUILayout.Button("Rebuild Clusters", buttonStyle))
        {
            BuildClusters();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(!clustersBuilt);
        if (GUILayout.Button("List Materials", buttonStyle))
        {
            ListMaterials();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();
        if (showMaterialList)
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Found Materials", EditorStyles.boldLabel);
            materialScrollPosition = EditorGUILayout.BeginScrollView(materialScrollPosition, GUILayout.Height(50));
            foreach (var material in foundMaterials)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(material, typeof(Material), false);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        // Second line: Group Objects Only
        EditorGUI.BeginDisabledGroup(!clustersBuilt);
        if (GUILayout.Button("Group Objects Only", buttonStyle))
        {
            GroupObjects();
        }

        // Third line: Group and Combine Clusters
        if (GUILayout.Button("Group and Combine Clusters", buttonStyle))
        {
            GroupAndCombineClusters();
        }
        EditorGUI.EndDisabledGroup();

        // Fourth line: Undo Last Operation
        if (GUILayout.Button("Undo Last Operation", buttonStyle))
        {
            Undo.PerformUndo();
        }

        GUILayout.Space(5);

        // Cluster Information Section
        DisplayClusterInfo();




        // End global scroll view
        EditorGUILayout.EndScrollView();
    }

    private void DisplayClusterInfo()
    {
        if (clustersBuilt)
        {
            EditorGUILayout.BeginVertical("box");
            int totalObjects = clusters.Sum(c => c.TotalRenderers);
            int totalTriangles = clusters.Sum(c => c.TotalTriangles);
            EditorGUILayout.LabelField($"Total Objects: {totalObjects}");
            EditorGUILayout.LabelField($"Total Triangles: {totalTriangles}");
            EditorGUILayout.LabelField($"Number of Clusters: {clusters.Count}");

            showClusterList = EditorGUILayout.Foldout(showClusterList, "Cluster List", true);
            if (showClusterList)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
                for (int i = 0; i < clusters.Count; i++)
                {
                    Cluster cluster = clusters[i];

                    EditorGUILayout.BeginHorizontal();

                    // Check if the cluster exceeds the triangle limit
                    bool overTriangleLimit = cluster.TotalTriangles > triangleLimit;

                    // Set color to red if over limit
                    if (overTriangleLimit)
                    {
                        GUI.color = Color.red;
                    }

                    EditorGUILayout.LabelField($"Cluster {i + 1}:", GUILayout.Width(70));
                    EditorGUILayout.LabelField($"Objects: {cluster.TotalRenderers}", GUILayout.Width(80));
                    EditorGUILayout.LabelField($"Triangles: {cluster.TotalTriangles}", GUILayout.Width(100));
                    EditorGUILayout.LabelField(cluster.IsSubdivided ? $"Sub (Level {cluster.SubdivisionLevel})" : "Main", GUILayout.Width(80));
                    EditorGUILayout.LabelField(cluster.HasLODGroups ? "With LODs" : "No LODs", GUILayout.Width(80));
                    EditorGUILayout.LabelField(cluster.Material.name, GUILayout.Width(200));

                    // Reset GUI color
                    GUI.color = Color.white;

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }
    }

    private void DetectLODGroups()
    {
        lodGroupsDetected = false;

        foreach (var parent in parentObjects)
        {
            if (parent != null)
            {
                var lodGroups = parent.GetComponentsInChildren<LODGroup>(true);
                if (lodGroups.Length > 0)
                {
                    lodGroupsDetected = true;
                    break;
                }
            }
        }
    }

    private void BuildClusters()
    {
        if (parentObjects == null || parentObjects.Count == 0)
        {
            clustersBuilt = false;
            clusters.Clear();
            SceneView.RepaintAll();
            return;
        }

        clusters.Clear();

        if (lodGroupsDetected && lodHandlingOption == LODHandlingOption.CombineLodsSeparately)
        {
            // Separate renderers with and without LOD groups
            List<RendererWithLODLevel> renderersWithLODGroups;
            List<RendererWithLODLevel> renderersWithoutLODGroups;
            int maxLODLevel;

            GetRenderersWithLODLevels(out renderersWithLODGroups, out renderersWithoutLODGroups, out maxLODLevel);

            // Build clusters for renderers with LOD groups
            if (renderersWithLODGroups.Count > 0)
            {
                BuildClustersForRenderers(renderersWithLODGroups, maxLODLevel, true);
            }

            // Build clusters for renderers without LOD groups
            if (renderersWithoutLODGroups.Count > 0)
            {
                BuildClustersForRenderers(renderersWithoutLODGroups, 0, false);
            }
        }
        else
        {
            // When "Combine All" is selected, treat all renderers together and create LOD levels
            List<RendererWithLODLevel> allRenderersWithLODLevels;
            int maxLODLevel;

            GetAllRenderersWithLODLevels(out allRenderersWithLODLevels, out maxLODLevel);

            BuildClustersForRenderers(allRenderersWithLODLevels, maxLODLevel, true);
        }

        clustersBuilt = true;
        SceneView.RepaintAll();
        Repaint();
    }

    private void ListMaterials()
    {
        if (parentObjects == null || parentObjects.Count == 0)
        {
            Debug.LogError("Please select at least one parent object first.");
            return;
        }

        materialGroups.Clear();
        foundMaterials.Clear();
        MeshRenderer[] renderers = GetFilteredRenderers().ToArray();

        foreach (MeshRenderer renderer in renderers)
        {
            Material mat = renderer.sharedMaterial;
            if (!materialGroups.ContainsKey(mat))
            {
                materialGroups[mat] = new List<MeshRenderer>();
                foundMaterials.Add(mat); // Add the material to the list
            }
            materialGroups[mat].Add(renderer);
        }

        showMaterialList = true; // Show the material list in the GUI
        Repaint(); // Repaint the GUI to show the materials
    }

    private void BuildClustersForRenderers(List<RendererWithLODLevel> renderersWithLOD, int maxLODLevel, bool hasLODGroups)
    {
        if (selectedAlgorithm == ClusteringAlgorithm.KMeans)
        {
            BuildClustersKMeans(renderersWithLOD, maxLODLevel, hasLODGroups);
        }
        else
        {
            BuildClustersProximity(renderersWithLOD, maxLODLevel, hasLODGroups);
        }
    }

    private void BuildClustersProximity(List<RendererWithLODLevel> renderersWithLOD, int maxLODLevel, bool hasLODGroups)
    {
        List<RendererWithLODLevel> remainingRenderers = new List<RendererWithLODLevel>(renderersWithLOD);

        while (remainingRenderers.Count > 0)
        {
            RendererWithLODLevel currentRenderer = remainingRenderers[0];
            Cluster newCluster = new Cluster(currentRenderer, hasLODGroups: hasLODGroups);
            remainingRenderers.RemoveAt(0);

            for (int i = remainingRenderers.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(currentRenderer.Renderer.transform.position, remainingRenderers[i].Renderer.transform.position) <= groupingRadius
                    && currentRenderer.Renderer.sharedMaterial == remainingRenderers[i].Renderer.sharedMaterial)
                {
                    newCluster.AddRenderer(remainingRenderers[i]);
                    remainingRenderers.RemoveAt(i);
                }
            }

            newCluster.CalculateTriangles();
            clusters.AddRange(SubdivideClusterRecursive(newCluster, 0, maxLODLevel));
        }
    }

    private void BuildClustersKMeans(List<RendererWithLODLevel> renderersWithLOD, int maxLODLevel, bool hasLODGroups)
    {
        Dictionary<Material, List<RendererWithLODLevel>> materialGroups = new Dictionary<Material, List<RendererWithLODLevel>>();

        foreach (RendererWithLODLevel renderer in renderersWithLOD)
        {
            if (!materialGroups.ContainsKey(renderer.Renderer.sharedMaterial))
            {
                materialGroups[renderer.Renderer.sharedMaterial] = new List<RendererWithLODLevel>();
            }
            materialGroups[renderer.Renderer.sharedMaterial].Add(renderer);
        }

        foreach (var materialGroup in materialGroups)
        {
            List<Vector3> centroids = InitializeRandomCentroids(materialGroup.Value, kClusters);

            for (int iteration = 0; iteration < kMeansIterations; iteration++)
            {
                Dictionary<int, List<RendererWithLODLevel>> clusterAssignments = new Dictionary<int, List<RendererWithLODLevel>>();
                for (int i = 0; i < kClusters; i++)
                {
                    clusterAssignments[i] = new List<RendererWithLODLevel>();
                }

                foreach (RendererWithLODLevel renderer in materialGroup.Value)
                {
                    int nearestCentroidIndex = FindNearestCentroidIndex(renderer.Renderer.transform.position, centroids);
                    clusterAssignments[nearestCentroidIndex].Add(renderer);
                }

                for (int i = 0; i < kClusters; i++)
                {
                    if (clusterAssignments[i].Count > 0)
                    {
                        Vector3 newCentroid = Vector3.zero;
                        foreach (RendererWithLODLevel renderer in clusterAssignments[i])
                        {
                            newCentroid += renderer.Renderer.transform.position;
                        }
                        centroids[i] = newCentroid / clusterAssignments[i].Count;
                    }
                }

                if (iteration == kMeansIterations - 1)
                {
                    for (int i = 0; i < kClusters; i++)
                    {
                        if (clusterAssignments[i].Count > 0)
                        {
                            Cluster newCluster = new Cluster(clusterAssignments[i][0], hasLODGroups: hasLODGroups);
                            for (int j = 1; j < clusterAssignments[i].Count; j++)
                            {
                                newCluster.AddRenderer(clusterAssignments[i][j]);
                            }
                            newCluster.CalculateTriangles();
                            clusters.AddRange(SubdivideClusterRecursive(newCluster, 0, maxLODLevel));
                        }
                    }
                }
            }
        }
    }

    private List<Vector3> InitializeRandomCentroids(List<RendererWithLODLevel> renderersWithLOD, int k)
    {
        List<Vector3> centroids = new List<Vector3>();
        Bounds sceneBounds = new Bounds(renderersWithLOD[0].Renderer.transform.position, Vector3.zero);

        foreach (RendererWithLODLevel renderer in renderersWithLOD)
        {
            sceneBounds.Encapsulate(renderer.Renderer.bounds);
        }

        for (int i = 0; i < k; i++)
        {
            Vector3 randomPoint = new Vector3(
                UnityEngine.Random.Range(sceneBounds.min.x, sceneBounds.max.x),
                UnityEngine.Random.Range(sceneBounds.min.y, sceneBounds.max.y),
                UnityEngine.Random.Range(sceneBounds.min.z, sceneBounds.max.z)
            );
            centroids.Add(randomPoint);
        }

        return centroids;
    }

    private int FindNearestCentroidIndex(Vector3 position, List<Vector3> centroids)
    {
        int nearestIndex = 0;
        float nearestDistanceSqr = float.MaxValue;

        for (int i = 0; i < centroids.Count; i++)
        {
            float distanceSqr = (centroids[i] - position).sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    private List<Cluster> SubdivideClusterRecursive(Cluster cluster, int level, int maxLODLevel)
    {
        if (level >= MaxRecursionDepth)
        {
            Debug.LogWarning($"Maximum recursion depth reached for cluster. Some objects may exceed the triangle limit.");
            return new List<Cluster> { cluster };
        }

        if (cluster.TotalTriangles <= triangleLimit)
        {
            return new List<Cluster> { cluster };
        }

        List<Cluster> subclusters = new List<Cluster>();
        List<RendererWithLODLevel> remainingRenderers = new List<RendererWithLODLevel>();
        foreach (var lodList in cluster.RenderersPerLODLevel.Values)
        {
            remainingRenderers.AddRange(lodList);
        }

        while (remainingRenderers.Count > 0)
        {
            RendererWithLODLevel currentRenderer = remainingRenderers[0];
            Cluster newSubcluster = new Cluster(currentRenderer, true, level + 1, cluster.HasLODGroups);
            remainingRenderers.RemoveAt(0);

            for (int i = remainingRenderers.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(currentRenderer.Renderer.transform.position, remainingRenderers[i].Renderer.transform.position) <= subgroupRadius)
                {
                    newSubcluster.AddRenderer(remainingRenderers[i]);
                    remainingRenderers.RemoveAt(i);
                }
            }

            newSubcluster.CalculateTriangles();
            subclusters.AddRange(SubdivideClusterRecursive(newSubcluster, level + 1, maxLODLevel));
        }

        return subclusters;
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

        foreach (Cluster cluster in clusters)
        {
            string groupName = $"{newParentName}_Group_{cluster.Material.name}";
            Debug.Log($"Grouping and combining objects into {groupName}");

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

            foreach (var lodList in cluster.RenderersPerLODLevel.Values)
            {
                foreach (RendererWithLODLevel renderer in lodList)
                {
                    Undo.SetTransformParent(renderer.Renderer.transform, sourceObjectsParent.transform, "Group Source Object");
                }
            }

            if (!CombineClusterMeshes(cluster, combinedObject))
            {
                Debug.LogWarning($"Failed to combine meshes for {groupName}. Objects are grouped but not combined.");
                continue;
            }

            foreach (var lodList in cluster.RenderersPerLODLevel.Values)
            {
                foreach (RendererWithLODLevel renderer in lodList)
                {
                    if (destroySourceObjects)
                    {
                        Undo.DestroyObjectImmediate(renderer.Renderer.gameObject);
                    }
                    else
                    {
                        Undo.RecordObject(renderer.Renderer.gameObject, "Disable Original Object");
                        renderer.Renderer.gameObject.SetActive(false);
                    }
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
        int maxLODLevel = cluster.GetMaxLODLevel();
        List<LOD> lods = new List<LOD>();

        MeshRenderer combinedRenderer = null;

        // Calculate common center offset
        Vector3 centerOffset = Vector3.zero;
        Bounds combinedBounds = new Bounds();
        bool boundsInitialized = false;

        // Collect all renderers across all LOD levels, including -1 for objects without LODs
        List<RendererWithLODLevel> allRenderers = new List<RendererWithLODLevel>();
        foreach (var lodRenderers in cluster.RenderersPerLODLevel.Values)
        {
            allRenderers.AddRange(lodRenderers);
        }

        // Calculate bounds and center offset
        foreach (RendererWithLODLevel rendererWithLOD in allRenderers)
        {
            MeshFilter meshFilter = rendererWithLOD.Renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }
            Mesh mesh = meshFilter.sharedMesh;
            Matrix4x4 matrix = meshFilter.transform.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            foreach (Vector3 vertex in vertices)
            {
                Vector3 worldPoint = matrix.MultiplyPoint3x4(vertex);
                if (!boundsInitialized)
                {
                    combinedBounds = new Bounds(worldPoint, Vector3.zero);
                    boundsInitialized = true;
                }
                else
                {
                    combinedBounds.Encapsulate(worldPoint);
                }
            }
        }
        centerOffset = combinedBounds.center;
        parent.transform.position = centerOffset;

        // Check if the cluster contains LOD groups or not
        if (!cluster.HasLODGroups)
        {
            // Process clusters without LOD groups
            if (cluster.RenderersPerLODLevel.TryGetValue(-1, out List<RendererWithLODLevel> renderersAtLOD))
            {
                List<CombineInstance> combineInstances = new List<CombineInstance>();
                Material sharedMaterial = null;

                // Combine meshes for the renderers without LOD groups
                foreach (RendererWithLODLevel rendererWithLOD in renderersAtLOD)
                {
                    MeshFilter meshFilter = rendererWithLOD.Renderer.GetComponent<MeshFilter>();
                    if (meshFilter == null || meshFilter.sharedMesh == null)
                    {
                        continue;
                    }

                    if (sharedMaterial == null)
                    {
                        sharedMaterial = rendererWithLOD.Renderer.sharedMaterial;
                    }
                    else if (sharedMaterial != rendererWithLOD.Renderer.sharedMaterial)
                    {
                        Debug.LogWarning($"Different materials found in cluster {parent.name}. Skipping combination.");
                        return false;
                    }

                    CombineInstance ci = new CombineInstance
                    {
                        mesh = meshFilter.sharedMesh,
                        transform = meshFilter.transform.localToWorldMatrix
                    };
                    combineInstances.Add(ci);
                }

                if (combineInstances.Count == 0)
                {
                    Debug.LogWarning($"No renderers found in cluster {parent.name} for combining.");
                    return false;
                }

                Mesh combinedMesh = new Mesh();

                // Check if the combined vertex count exceeds 65k
                int totalVertexCount = combineInstances.Sum(ci => ci.mesh.vertexCount);
                if (totalVertexCount > 65535)
                {
                    combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

                if (rebuildNormals)
                {
                    combinedMesh.RecalculateNormals();
                }

                if (rebuildLightmapUV)
                {
                    Unwrapping.GenerateSecondaryUVSet(combinedMesh);
                }

                // Adjust mesh vertices to center around the common center offset
                Vector3[] vertices = combinedMesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] -= centerOffset;
                }
                combinedMesh.vertices = vertices;
                combinedMesh.RecalculateBounds();

                // Save the combined mesh as an asset
                SaveMeshAsset(combinedMesh, $"{parent.name}_NoLOD");

                // Create a new game object for this combined mesh
                GameObject combinedObject = new GameObject($"{parent.name}_NoLOD_Combined");
                combinedObject.transform.SetParent(parent.transform);
                combinedObject.transform.localPosition = Vector3.zero;
                combinedObject.transform.localScale = Vector3.one;

                // Add components for the combined mesh
                MeshFilter newMeshFilter = combinedObject.AddComponent<MeshFilter>();
                newMeshFilter.sharedMesh = combinedMesh;

                MeshRenderer newRenderer = combinedObject.AddComponent<MeshRenderer>();
                newRenderer.sharedMaterial = sharedMaterial;

                if (addMeshCollider)
                {
                    MeshCollider collider = combinedObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = combinedMesh;
                }

                combinedRenderer = newRenderer;
            }
        }
        else
        {
            for (int lodLevel = 0; lodLevel <= maxLODLevel; lodLevel++)
            {
                List<RendererWithLODLevel> renderersAtLOD = new List<RendererWithLODLevel>();

                // Track the last valid LOD level for each renderer
                List<RendererWithLODLevel> lastValidLODRenderers = null;

                // Include renderers from clusters with LOD groups
                if (cluster.RenderersPerLODLevel.TryGetValue(lodLevel, out List<RendererWithLODLevel> renderers))
                {
                    renderersAtLOD.AddRange(renderers);
                    lastValidLODRenderers = renderersAtLOD; // Update the last valid LOD
                }
                else if (lastValidLODRenderers != null)
                {
                    // No renderers at this LOD level, reuse the last valid LOD level
                    renderersAtLOD.AddRange(lastValidLODRenderers);
                }

                // Include renderers without LOD groups (LODLevel = -1) in all LOD levels
                if (cluster.RenderersPerLODLevel.TryGetValue(-1, out List<RendererWithLODLevel> renderersWithoutLOD))
                {
                    renderersAtLOD.AddRange(renderersWithoutLOD); // Add to every LOD level
                }

                // Handle missing LOD levels by duplicating the last valid LOD level renderers
                if (renderersAtLOD.Count == 0 && lastValidLODRenderers != null)
                {
                    renderersAtLOD.AddRange(lastValidLODRenderers);
                }

                if (renderersAtLOD.Count == 0)
                {
                    continue; // Skip this LOD level if there are no renderers
                }

                List<CombineInstance> combineInstances = new List<CombineInstance>();
                Material sharedMaterial = null;

                // Combine meshes for the current LOD level
                foreach (RendererWithLODLevel rendererWithLOD in renderersAtLOD)
                {
                    MeshFilter meshFilter = rendererWithLOD.Renderer.GetComponent<MeshFilter>();
                    if (meshFilter == null || meshFilter.sharedMesh == null)
                    {
                        continue;
                    }

                    if (sharedMaterial == null)
                    {
                        sharedMaterial = rendererWithLOD.Renderer.sharedMaterial;
                    }
                    else if (sharedMaterial != rendererWithLOD.Renderer.sharedMaterial)
                    {
                        Debug.LogWarning($"Different materials found in cluster {parent.name}. Skipping combination.");
                        return false;
                    }

                    CombineInstance ci = new CombineInstance
                    {
                        mesh = meshFilter.sharedMesh,
                        transform = meshFilter.transform.localToWorldMatrix
                    };
                    combineInstances.Add(ci);
                }

                if (combineInstances.Count == 0)
                {
                    continue;
                }

                // Combine the mesh instances into a single mesh
                Mesh combinedMesh = new Mesh();

                // Check if the combined vertex count exceeds 65k
                int totalVertexCount = combineInstances.Sum(ci => ci.mesh.vertexCount);
                if (totalVertexCount > 65535)
                {
                    combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

                if (rebuildNormals)
                {
                    combinedMesh.RecalculateNormals();
                }

                if (rebuildLightmapUV)
                {
                    Unwrapping.GenerateSecondaryUVSet(combinedMesh);
                }

                // Adjust mesh vertices to center around the common center offset
                Vector3[] vertices = combinedMesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] -= centerOffset;
                }
                combinedMesh.vertices = vertices;
                combinedMesh.RecalculateBounds();

                // Save the combined mesh as an asset
                SaveMeshAsset(combinedMesh, $"{parent.name}_LOD{lodLevel}");

                // Create a new game object for this LOD level
                GameObject lodObject = new GameObject($"{parent.name}_LOD{lodLevel}");
                lodObject.transform.SetParent(parent.transform);
                lodObject.transform.localPosition = Vector3.zero;
                lodObject.transform.localScale = Vector3.one;

                // Add components for the combined mesh
                MeshFilter newMeshFilter = lodObject.AddComponent<MeshFilter>();
                newMeshFilter.sharedMesh = combinedMesh;

                MeshRenderer newRenderer = lodObject.AddComponent<MeshRenderer>();
                newRenderer.sharedMaterial = sharedMaterial;

                if (addMeshCollider)
                {
                    MeshCollider collider = lodObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = combinedMesh;
                }

                combinedRenderer = newRenderer;

                // Calculate the transition height for this LOD level
                float transitionHeight = GetLODScreenTransitionHeight(lodLevel, maxLODLevel);
                LOD lod = new LOD(transitionHeight, new Renderer[] { newRenderer });
                lods.Add(lod);
            }

            // Create the LOD group for the combined object
            if (lods.Count > 0)
            {
                LODGroup lodGroup = parent.AddComponent<LODGroup>();
                lodGroup.SetLODs(lods.ToArray());
                lodGroup.RecalculateBounds();
            }
        }

        return true;
    }

    private float GetLODScreenTransitionHeight(int lodLevel, int maxLODLevel)
    {
        // Define standard transition heights
        float[] defaultTransitionHeights = { 0.6f, 0.4f, 0.2f, 0.1f, 0.05f };
        if (lodLevel < defaultTransitionHeights.Length)
        {
            return defaultTransitionHeights[lodLevel];
        }
        else
        {
            return 0.01f; // Minimum screen height for higher LOD levels
        }
    }

    private void SaveMeshAsset(Mesh mesh, string objectName)
    {
        if (!Directory.Exists(MeshSavePath))
        {
            Directory.CreateDirectory(MeshSavePath);
        }

        string uniqueId = Guid.NewGuid().ToString("N");

        string assetPath = $"{MeshSavePath}/{objectName}_{uniqueId}.asset";
        AssetDatabase.CreateAsset(mesh, assetPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"Mesh saved as: {assetPath}");
    }

    private void GroupObjects()
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
            string groupName = $"{newParentName}_Group{i + 1}";

            GameObject groupParent = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(groupParent, "Create Group Parent");
            groupParent.transform.SetParent(newParent.transform);
            groupParent.transform.position = cluster.Center;

            foreach (var lodList in cluster.RenderersPerLODLevel.Values)
            {
                foreach (RendererWithLODLevel renderer in lodList)
                {
                    Undo.SetTransformParent(renderer.Renderer.transform, groupParent.transform, "Set Parent");
                }
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
            if (cluster.TotalTriangles > triangleLimit)
            {
                color = OverLimitColor;
            }
            else if (cluster.IsSubdivided)
            {
                color = SubGroupColor;
            }
            else
            {
                color = GetMaterialColor(cluster.Material);
            }

            color.a = gizmoSphereOpacity;
            Handles.color = color;

            float radius = cluster.GizmoRadius; // Use calculated GizmoRadius
            Handles.SphereHandleCap(0, cluster.Center, Quaternion.identity, radius * 2, EventType.Repaint);

            // Differentiate clusters with and without LODs
            if (cluster.HasLODGroups)
            {
                Handles.color = Color.blue;
            }
            else
            {
                Handles.color = Color.green;
            }

            if (drawLines)
            {
                foreach (var lodList in cluster.RenderersPerLODLevel.Values)
                {
                    foreach (RendererWithLODLevel renderer in lodList)
                    {
                        Handles.DrawLine(cluster.Center, renderer.Renderer.transform.position);
                    }
                }
            }
        }
    }

    private Color GetMaterialColor(Material material)
    {
        if (!materialColors.TryGetValue(material, out Color color))
        {
            Color.RGBToHSV(MainGroupColor, out float h, out float s, out float v);
            float hueOffset = (material.GetInstanceID() * 0.618034f) % 1f;
            hueOffset = (hueOffset - 0.5f) * 0.2f;
            h = Mathf.Repeat(h + hueOffset, 1f);
            s = Mathf.Clamp01(s + (hueOffset * 0.2f));
            v = Mathf.Clamp01(v - (hueOffset * 0.1f));
            color = Color.HSVToRGB(h, s, v);
            color.a = MainGroupColor.a;
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
        public Dictionary<int, List<RendererWithLODLevel>> RenderersPerLODLevel { get; private set; } = new Dictionary<int, List<RendererWithLODLevel>>();
        public Vector3 Center { get; private set; }
        public int TotalTriangles { get; private set; }
        public bool IsSubdivided { get; private set; }
        public int SubdivisionLevel { get; private set; }
        public Material Material { get; private set; }

        public float GizmoRadius { get; private set; } // New property for Gizmo radius

        public int TotalRenderers
        {
            get
            {
                return RenderersPerLODLevel.Values.Sum(list => list.Count);
            }
        }

        public Dictionary<MeshRenderer, int> RendererMaxLODLevel = new Dictionary<MeshRenderer, int>();
        public bool HasLODGroups { get; private set; }

        public Cluster(RendererWithLODLevel initialRenderer, bool isSubdivided = false, int subdivisionLevel = 0, bool hasLODGroups = false)
        {
            Material = initialRenderer.Renderer.sharedMaterial;
            IsSubdivided = isSubdivided;
            SubdivisionLevel = subdivisionLevel;
            HasLODGroups = hasLODGroups;
            AddRenderer(initialRenderer);
            CalculateTriangles();
            CalculateGizmoRadius();
        }

        public void AddRenderer(RendererWithLODLevel rendererWithLOD)
        {
            if (Material == null)
            {
                Material = rendererWithLOD.Renderer.sharedMaterial;
            }
            else if (Material != rendererWithLOD.Renderer.sharedMaterial)
            {
                Debug.LogWarning("Attempting to add a renderer with a different material to the cluster.");
                return;
            }

            int lodLevel = rendererWithLOD.LODLevel;
            if (!RenderersPerLODLevel.ContainsKey(lodLevel))
            {
                RenderersPerLODLevel[lodLevel] = new List<RendererWithLODLevel>();
            }
            RenderersPerLODLevel[lodLevel].Add(rendererWithLOD);

            if (RendererMaxLODLevel.ContainsKey(rendererWithLOD.Renderer))
            {
                RendererMaxLODLevel[rendererWithLOD.Renderer] = Mathf.Max(RendererMaxLODLevel[rendererWithLOD.Renderer], lodLevel);
            }
            else
            {
                RendererMaxLODLevel[rendererWithLOD.Renderer] = lodLevel;
            }

            RecalculateCenter();
            CalculateGizmoRadius(); // Recalculate Gizmo radius when a renderer is added
        }

        public RendererWithLODLevel GetRendererWithLODLevel(MeshRenderer renderer, int lodLevel)
        {
            if (RenderersPerLODLevel.TryGetValue(lodLevel, out List<RendererWithLODLevel> renderersAtLOD))
            {
                foreach (RendererWithLODLevel rendererWithLOD in renderersAtLOD)
                {
                    if (rendererWithLOD.Renderer == renderer)
                    {
                        return rendererWithLOD;
                    }
                }
            }
            return null;
        }

        private void RecalculateCenter()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var lodList in RenderersPerLODLevel.Values)
            {
                foreach (RendererWithLODLevel rendererWithLOD in lodList)
                {
                    sum += rendererWithLOD.Renderer.transform.position;
                    count++;
                }
            }
            Center = sum / count;
        }

        public void CalculateTriangles()
        {
            TotalTriangles = 0;
            foreach (var lodList in RenderersPerLODLevel.Values)
            {
                foreach (RendererWithLODLevel rendererWithLOD in lodList)
                {
                    MeshFilter meshFilter = rendererWithLOD.Renderer.GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        TotalTriangles += meshFilter.sharedMesh.triangles.Length / 3;
                    }
                }
            }
        }

        private void CalculateGizmoRadius()
        {
            GizmoRadius = 0f;
            foreach (var lodList in RenderersPerLODLevel.Values)
            {
                foreach (RendererWithLODLevel rendererWithLOD in lodList)
                {
                    float distance = Vector3.Distance(rendererWithLOD.Renderer.transform.position, Center);
                    if (distance > GizmoRadius)
                    {
                        GizmoRadius = distance;
                    }
                }
            }
            // Add some padding to ensure all objects are within the sphere
            GizmoRadius += 0.5f;
        }

        public int GetMaxLODLevel()
        {
            if (RenderersPerLODLevel.Keys.Count == 0) return 0;
            return RenderersPerLODLevel.Keys.Max();
        }
    }

    private class RendererWithLODLevel
    {
        public MeshRenderer Renderer;
        public int LODLevel;

        public RendererWithLODLevel(MeshRenderer renderer, int lodLevel)
        {
            Renderer = renderer;
            LODLevel = lodLevel;
        }
    }

    private void GetRenderersWithLODLevels(out List<RendererWithLODLevel> renderersWithLOD, out List<RendererWithLODLevel> renderersWithoutLOD, out int maxLODLevel)
    {
        renderersWithLOD = new List<RendererWithLODLevel>();
        renderersWithoutLOD = new List<RendererWithLODLevel>();
        maxLODLevel = 0;
        HashSet<MeshRenderer> processedRenderers = new HashSet<MeshRenderer>();

        foreach (var parent in parentObjects)
        {
            if (parent != null)
            {
                // Process LODGroups
                var lodGroups = parent.GetComponentsInChildren<LODGroup>(true);
                foreach (var lodGroup in lodGroups)
                {
                    LOD[] lods = lodGroup.GetLODs();
                    if (lods.Length - 1 > maxLODLevel)
                    {
                        maxLODLevel = lods.Length - 1;
                    }
                    for (int i = 0; i < lods.Length; i++)
                    {
                        foreach (var lodRenderer in lods[i].renderers)
                        {
                            MeshRenderer mr = lodRenderer as MeshRenderer;
                            if (mr != null && PassesFilters(mr) && !processedRenderers.Contains(mr))
                            {
                                renderersWithLOD.Add(new RendererWithLODLevel(mr, i));
                                processedRenderers.Add(mr);
                            }
                        }
                    }
                }

                // Process renderers not in LODGroups
                var allRenderers = parent.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var renderer in allRenderers)
                {
                    if (!processedRenderers.Contains(renderer) && PassesFilters(renderer))
                    {
                        // Renderer not in LODGroup
                        renderersWithoutLOD.Add(new RendererWithLODLevel(renderer, -1));
                    }
                }
            }
        }
    }

    private void GetAllRenderersWithLODLevels(out List<RendererWithLODLevel> allRenderersWithLODLevels, out int maxLODLevel)
    {
        allRenderersWithLODLevels = new List<RendererWithLODLevel>();
        maxLODLevel = 0;
        HashSet<MeshRenderer> processedRenderers = new HashSet<MeshRenderer>();

        foreach (var parent in parentObjects)
        {
            if (parent != null)
            {
                // Process LODGroups
                var lodGroups = parent.GetComponentsInChildren<LODGroup>(true);
                foreach (var lodGroup in lodGroups)
                {
                    LOD[] lods = lodGroup.GetLODs();
                    if (lods.Length - 1 > maxLODLevel)
                    {
                        maxLODLevel = lods.Length - 1;
                    }
                    for (int i = 0; i < lods.Length; i++)
                    {
                        foreach (var lodRenderer in lods[i].renderers)
                        {
                            MeshRenderer mr = lodRenderer as MeshRenderer;
                            if (mr != null && PassesFilters(mr) && !processedRenderers.Contains(mr))
                            {
                                allRenderersWithLODLevels.Add(new RendererWithLODLevel(mr, i));
                                processedRenderers.Add(mr);
                            }
                        }
                    }
                }

                // Process renderers not in LODGroups
                var allRenderers = parent.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var renderer in allRenderers)
                {
                    if (!processedRenderers.Contains(renderer) && PassesFilters(renderer))
                    {
                        // Renderer not in LODGroup
                        allRenderersWithLODLevels.Add(new RendererWithLODLevel(renderer, -1));
                    }
                }
            }
        }
    }

    // Filtered renderers without LOD levels
    private List<MeshRenderer> GetFilteredRenderers()
    {
        List<MeshRenderer> renderers = new List<MeshRenderer>();

        foreach (var parent in parentObjects)
        {
            if (parent != null)
            {
                renderers.AddRange(parent.GetComponentsInChildren<MeshRenderer>());
            }
        }

        List<MeshRenderer> filteredRenderers = new List<MeshRenderer>();

        foreach (MeshRenderer renderer in renderers)
        {
            if (PassesFilters(renderer))
            {
                filteredRenderers.Add(renderer);
            }
        }

        return filteredRenderers;
    }

    private bool PassesFilters(MeshRenderer renderer)
    {
        if (onlyActive && !renderer.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (onlyActiveMeshRenderers && !renderer.enabled)
        {
            return false;
        }

        if (onlyStatic && !renderer.gameObject.isStatic)
        {
            return false;
        }

        if (useTag && !string.IsNullOrEmpty(tagToUse) && renderer.tag != tagToUse)
        {
            return false;
        }

        if (useLayer && renderer.gameObject.layer != layerToUse)
        {
            return false;
        }

        if (useNameContains && !string.IsNullOrEmpty(nameContainsString) && !renderer.name.ToLower().Contains(nameContainsString.ToLower()))
        {
            return false;
        }

        return true;
    }
}
public class ToolInstructionsWindow : EditorWindow
{
    private Vector2 scrollPosition;

    public static void ShowWindow()
    {
        // Open the window
        var window = GetWindow<ToolInstructionsWindow>("Tool Instructions");
        window.minSize = new Vector2(400, 300);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Intelligent Mesh Combiner - Instructions", EditorStyles.boldLabel);

        // Begin scroll view for the instructions
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField(
            "The Intelligent Mesh Combiner tool allows you to group and combine meshes based on their proximity and materials.\n\n" +
            "Features:\n" +
            "- Group objects within a specified radius or using K-means clustering\n" +
            "- Combine meshes while maintaining their original positions\n" +
            "- Automatically handle multiple materials\n" +
            "- Visualize clusters with material-specific colors\n" +
            "- Save combined meshes as assets in 'IMC_Meshes' folder\n" +
            "- Option to rebuild lightmap UVs for combined meshes\n" +
            "- Option to add mesh colliders to combined objects\n" +
            "- Mark combined objects as static\n" +
            "- Option to destroy source objects after combining\n\n" +
            "Usage:\n" +
            "1. Select one or multiple parent objects containing the meshes you want to group\n" +
            "2. Choose between proximity-based or K-means clustering\n" +
            "3. Adjust the grouping parameters as needed\n" +
            "4. Set the desired filters under the 'Filters' section\n" +
            "5. Click 'Rebuild Clusters' to analyze the objects\n" +
            "6. Use 'Group Objects' to group without combining, or 'Group and Combine Clusters' to both group and combine meshes\n" +
            "7. Review the generated clusters in the scene view, where different materials are represented by subtle color variations\n\n" +
            "Limitations:\n" +
            "- Objects with different materials cannot be combined into a single mesh\n" +
            "- Skinned meshes are not supported for combination\n" +
            "- Particle systems and other non-mesh renderers are ignored.",
            EditorStyles.wordWrappedLabel
        );

        EditorGUILayout.LabelField(
            "\nDetailed Feature Breakdown:\n" +
            "------------------------------------------\n\n" +
            "1. Parent Objects Section:\n" +
            "This section allows users to add parent objects that contain the meshes for combination and grouping.\n" +
            "- Add Parent: Adds a new parent object to the list.\n" +
            "- Clear Parent List: Clears the list of parent objects.\n" +
            "- Remove (button next to each parent): Removes a parent object from the list.\n\n" +

            "2. LOD Group Detection:\n" +
            "LOD Handling Option:\n" +
            "- Combine LODs Separately: Objects with LOD groups are combined separately from objects without LOD groups.\n" +
            "- Combine All: Objects without LOD groups are added to LOD groups and processed together.\n" +
            "This feature ensures that objects with and without LOD groups are combined appropriately.\n\n" +

            "3. Clustering Algorithm:\n" +
            "- Proximity-Based: Groups objects physically close to each other.\n" +
            "- K-Means Clustering: Groups objects into a specified number of clusters (K), irrespective of their proximity.\n" +
            "- Number of Clusters (K): Number of clusters to form.\n" +
            "- Max Iterations: Maximum iterations for optimizing clusters.\n" +
            "- Grouping Radius: Defines the radius for proximity-based clustering.\n" +
            "- Subgroup Radius: Secondary radius for creating subgroups.\n\n" +

            "4. Triangle Limit:\n" +
            "Defines the maximum number of triangles a cluster can have. Clusters exceeding this limit will be subdivided.\n\n" +

            "5. Options Section:\n" +
            "- Rebuild Lightmap UV: Rebuilds UV for lightmapping.\n" +
            "- Rebuild Normals: Recalculates normals for the combined mesh.\n" +
            "- Add Mesh Collider: Adds a collider to the combined object.\n" +
            "- Mark Combined Static: Marks the object as static.\n" +
            "- Destroy Source Objects: Destroys the original objects after combination.\n" +
            "- New Parent Name: Specify a custom name for the parent object.\n\n" +

            "6. Filters Section:\n" +
            "- Only Static: Only static objects are processed.\n" +
            "- Only Active: Only active objects are processed.\n" +
            "- Only Active Mesh Renderers: Only active Mesh Renderers are processed.\n" +
            "- Use Tag: Filter objects by tag.\n" +
            "- Use Layer: Filter objects by layer.\n" +
            "- Use Name Contains: Filter objects by name.\n\n" +

            "7. Visualization Settings:\n" +
            "- Gizmo Sphere Opacity: Adjusts the opacity of cluster gizmos.\n" +
            "- Draw Lines: Draws lines connecting objects to their cluster centers.\n\n" +

            "8. Action Buttons:\n" +
            "- Rebuild Clusters: Analyzes and clusters objects.\n" +
            "- List Materials: Lists materials in the selected objects.\n" +
            "- Group Objects Only: Groups objects without combining.\n" +
            "- Group and Combine Clusters: Groups and combines meshes.\n" +
            "- Undo Last Operation: Reverts the last action.\n\n" +

            "9. Material List Section:\n" +
            "Displays materials used by the selected objects.\n\n" +

            "10. Cluster Information:\n" +
            "- Total Objects: Total number of objects clustered.\n" +
            "- Total Triangles: Total number of triangles across clusters.\n" +
            "- Cluster List: Details each cluster with the number of objects, triangles, and LOD status.\n\n" +

            "Limitations:\n" +
            "- Different Materials: Objects with different materials can't be combined into a single mesh.\n" +
            "- Skinned Meshes: Skinned meshes are not supported.\n" +
            "- Particle Systems and Non-Mesh Renderers: These are ignored by the tool.",
            EditorStyles.wordWrappedLabel
        );

        // End scroll view
        EditorGUILayout.EndScrollView();
    }
}

