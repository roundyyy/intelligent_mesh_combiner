using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

public class IntelligentMeshCombiner : EditorWindow
{
    // ---------------------------------------------
    //   New fields for requested functionalities
    // ---------------------------------------------
    private bool debugMode = true;  // <--- Toggle to enable/disable debug logs
    private bool disableSourceRenderers = false;  // <--- Option to disable source MeshRenderers (instead of destroying)

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
        KMeans,
        CellBased  // <--- NEW ALGORITHM
    }

    private ClusteringAlgorithm selectedAlgorithm = ClusteringAlgorithm.KMeans;
    private int kClusters = 5;
    private int kMeansIterations = 10;

    private float gizmoSphereOpacity = 0.2f;
    private bool rebuildNormals = false;

    // Cell-based size
    private Vector3 cellSize = new Vector3(30, 30, 30); // <--- default

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

    private bool drawLines = false;

    // ----------------------------------------------------
    // LOD Group variables
    // ----------------------------------------------------
    private enum LODHandlingOption
    {
        CombineLodsSeparately,
        CombineAll,
        KeepOriginalLODs // <--- NEW
    }

    private LODHandlingOption lodHandlingOption = LODHandlingOption.CombineLodsSeparately;
    private bool lodGroupsDetected = false;

    [MenuItem("Tools/Intelligent Mesh Combiner")]
    public static void ShowWindow()
    {
        GetWindow<IntelligentMeshCombiner>("Intelligent Mesh Combiner");
    }

    // ----------------------------------------------------------
    // Helper: Only logs if debugMode is true
    // ----------------------------------------------------------
    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log(message);
        }
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
        GUILayout.Label("Intelligent Mesh Combiner v0.7", titleStyle);

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
        //default algortithm is ProximityBased
        selectedAlgorithm = ClusteringAlgorithm.CellBased;
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
        else if (selectedAlgorithm == ClusteringAlgorithm.CellBased)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Cell-Based Settings", EditorStyles.boldLabel);
            cellSize = EditorGUILayout.Vector3Field("Cell Size (XYZ)", cellSize);
            EditorGUILayout.HelpBox("Objects are grouped by occupying the same 3D cell. No triangle limit, no subdividing.", MessageType.Info);
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
            debugMode = EditorGUILayout.Toggle("Debug Mode", debugMode);
            disableSourceRenderers = EditorGUILayout.Toggle("Disable Source Renderers", disableSourceRenderers);

            rebuildLightmapUV = EditorGUILayout.Toggle("Rebuild Lightmap UV", rebuildLightmapUV);
            rebuildNormals = EditorGUILayout.Toggle("Rebuild Normals", rebuildNormals);
            addMeshCollider = EditorGUILayout.Toggle("Add Mesh Collider (LOD0 Only)", addMeshCollider);
            markCombinedStatic = EditorGUILayout.Toggle("Mark Static", markCombinedStatic);
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
            onlyActiveMeshRenderers = EditorGUILayout.Toggle("Only Active MeshRenderers", onlyActiveMeshRenderers);
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

        // Third line: Combine Clusters Only
        if (GUILayout.Button("Combine Clusters Only", buttonStyle))
        {
            CombineClustersOnly();
        }

        // Fourth line: Group and Combine Clusters
        if (GUILayout.Button("Group and Combine Clusters", buttonStyle))
        {
            GroupAndCombineClusters();
        }
        EditorGUI.EndDisabledGroup();

        // Fifth line: Undo Last Operation
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

                    // The cluster might contain multiple materials if we forcibly combined them, but we store a "primary" material:
                    EditorGUILayout.LabelField(cluster.Material ? cluster.Material.name : "(multi-mat?)", GUILayout.Width(200));

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
        Log("Starting BuildClusters process...");

        if (parentObjects == null || parentObjects.Count == 0)
        {
            Debug.LogWarning("No parent objects specified. Clusters will be cleared.");
            clustersBuilt = false;
            clusters.Clear();
            SceneView.RepaintAll();
            return;
        }

        // Clear out any old clusters
        clusters.Clear();

        // If we truly detected at least one LODGroup in the hierarchy
        if (lodGroupsDetected)
        {
            // Depending on the user's LOD handling choice:
            if (lodHandlingOption == LODHandlingOption.CombineLodsSeparately)
            {
                Log("LOD handling is set to CombineLodsSeparately.");

                // Separate renderers with and without LOD groups
                List<RendererWithLODLevel> renderersWithLODGroups;
                List<RendererWithLODLevel> renderersWithoutLODGroups;
                int maxLODLevel;
                GetRenderersWithLODLevels(out renderersWithLODGroups, out renderersWithoutLODGroups, out maxLODLevel);

                // Build clusters for renderers WITH LOD groups
                if (renderersWithLODGroups.Count > 0)
                {
                    Log($"Found {renderersWithLODGroups.Count} renderers with LOD groups. Building clusters...");
                    if (selectedAlgorithm == ClusteringAlgorithm.CellBased)
                    {
                        BuildClustersCellBased(renderersWithLODGroups, maxLODLevel, true);
                    }
                    else
                    {
                        BuildClustersForRenderers(renderersWithLODGroups, maxLODLevel, true);
                    }
                }

                // Build clusters for renderers WITHOUT LOD groups
                if (renderersWithoutLODGroups.Count > 0)
                {
                    Log($"Found {renderersWithoutLODGroups.Count} renderers without LOD groups. Building clusters...");
                    if (selectedAlgorithm == ClusteringAlgorithm.CellBased)
                    {
                        BuildClustersCellBased(renderersWithoutLODGroups, 0, false);
                    }
                    else
                    {
                        BuildClustersForRenderers(renderersWithoutLODGroups, 0, false);
                    }
                }
            }
            else if (lodHandlingOption == LODHandlingOption.CombineAll)
            {
                Log("LOD handling is set to CombineAll. Combining all renderers as if they belong to LOD groups.");

                List<RendererWithLODLevel> allRenderersWithLODLevels;
                int maxLODLevel;
                GetAllRenderersWithLODLevels(out allRenderersWithLODLevels, out maxLODLevel);

                Log($"Combining all renderers into clusters with LOD groups. Total: {allRenderersWithLODLevels.Count}");
                if (selectedAlgorithm == ClusteringAlgorithm.CellBased)
                {
                    BuildClustersCellBased(allRenderersWithLODLevels, maxLODLevel, true);
                }
                else
                {
                    BuildClustersForRenderers(allRenderersWithLODLevels, maxLODLevel, true);
                }
            }
            else if (lodHandlingOption == LODHandlingOption.KeepOriginalLODs)
            {
                Log("LOD handling is set to KeepOriginalLODs. We'll cluster entire LODGroups, not single objects.");

                List<LODGroupInfo> allLODGroupInfos = GetLODGroupInfos();
                if (selectedAlgorithm == ClusteringAlgorithm.CellBased)
                {
                    BuildClustersCellBased_KeepOriginalLODGroups(allLODGroupInfos);
                }
                else if (selectedAlgorithm == ClusteringAlgorithm.KMeans)
                {
                    BuildClustersKMeans_KeepOriginalLODs(allLODGroupInfos);
                }
                else
                {
                    BuildClustersProximity_KeepOriginalLODs(allLODGroupInfos);
                }
            }
        }
        else
        {
            // No LOD groups detected => build clusters as non-LOD
            Log("No LOD groups detected in the hierarchy. Building clusters without LOD...");

            List<RendererWithLODLevel> allRenderersWithLODLevels;
            int maxLODLevel;
            GetAllRenderersWithLODLevels(out allRenderersWithLODLevels, out maxLODLevel);

            Log($"Found {allRenderersWithLODLevels.Count} total renderers. Building clusters as non-LOD groups.");

            if (selectedAlgorithm == ClusteringAlgorithm.CellBased)
            {
                BuildClustersCellBased(allRenderersWithLODLevels, 0, false);
            }
            else
            {
                BuildClustersForRenderers(allRenderersWithLODLevels, 0, false);
            }
        }

        // Done building clusters
        clustersBuilt = true;
        Log($"BuildClusters complete. Total clusters built: {clusters.Count}");
        SceneView.RepaintAll();
        Repaint();
    }

    // -----------------------------------------------------------------------
    // LODGroup-based data structure for KeepOriginalLODs
    // -----------------------------------------------------------------------
    private class LODGroupInfo
    {
        public LODGroup LODGroupComponent;
        public Vector3 Center;   // bounding center for the entire group
        public List<RendererWithLODLevel> Renderers;  // all LOD-level objects from this group

        public LODGroupInfo(LODGroup group, Vector3 center, List<RendererWithLODLevel> renderers)
        {
            LODGroupComponent = group;
            Center = center;
            Renderers = renderers;
        }
    }

    private List<LODGroupInfo> GetLODGroupInfos()
    {
        List<LODGroupInfo> results = new List<LODGroupInfo>();

        foreach (var parent in parentObjects)
        {
            if (parent == null) continue;
            var lodGroups = parent.GetComponentsInChildren<LODGroup>(true);
            foreach (var lodGroup in lodGroups)
            {
                LOD[] lodArray = lodGroup.GetLODs();
                List<RendererWithLODLevel> rwlodList = new List<RendererWithLODLevel>();

                for (int i = 0; i < lodArray.Length; i++)
                {
                    foreach (var rend in lodArray[i].renderers)
                    {
                        MeshRenderer mr = rend as MeshRenderer;
                        if (mr == null) continue;
                        if (!PassesFilters(mr)) continue;

                        rwlodList.Add(new RendererWithLODLevel(mr, i));
                    }
                }

                if (rwlodList.Count == 0) continue;

                // compute bounding center for entire group
                Bounds b = new Bounds(rwlodList[0].Renderer.bounds.center, Vector3.zero);
                for (int i = 1; i < rwlodList.Count; i++)
                {
                    b.Encapsulate(rwlodList[i].Renderer.bounds);
                }

                LODGroupInfo info = new LODGroupInfo(lodGroup, b.center, rwlodList);
                results.Add(info);
            }
        }

        return results;
    }

    // --------------------------------------------
    // KeepOriginalLODs => KMeans
    // --------------------------------------------
    private void BuildClustersKMeans_KeepOriginalLODs(List<LODGroupInfo> groupInfos)
    {
        if (groupInfos.Count == 0) return;

        List<Vector3> centroids = InitializeRandomCentroids_KeepOriginalLODGroups(groupInfos, kClusters);

        for (int iteration = 0; iteration < kMeansIterations; iteration++)
        {
            // cluster assignments => each cluster is a list of LODGroupInfos
            Dictionary<int, List<LODGroupInfo>> clusterAssignments = new Dictionary<int, List<LODGroupInfo>>();
            for (int i = 0; i < kClusters; i++)
            {
                clusterAssignments[i] = new List<LODGroupInfo>();
            }

            foreach (var info in groupInfos)
            {
                int nearestIndex = FindNearestCentroidIndex(info.Center, centroids);
                clusterAssignments[nearestIndex].Add(info);
            }

            // Recompute centroids
            for (int i = 0; i < kClusters; i++)
            {
                if (clusterAssignments[i].Count > 0)
                {
                    Vector3 newCentroid = Vector3.zero;
                    foreach (var gi in clusterAssignments[i])
                    {
                        newCentroid += gi.Center;
                    }
                    centroids[i] = newCentroid / clusterAssignments[i].Count;
                }
            }

            // final iteration => build actual Clusters
            if (iteration == kMeansIterations - 1)
            {
                foreach (var kvp in clusterAssignments)
                {
                    var list = kvp.Value;
                    if (list.Count == 0) continue;
                    BuildSingleClusterFromLODGroupInfos(list);
                }
            }
        }
    }

    // --------------------------------------------
    // KeepOriginalLODs => Proximity
    // --------------------------------------------
    private void BuildClustersProximity_KeepOriginalLODs(List<LODGroupInfo> groupInfos)
    {
        List<LODGroupInfo> remaining = new List<LODGroupInfo>(groupInfos);

        while (remaining.Count > 0)
        {
            LODGroupInfo current = remaining[0];
            List<LODGroupInfo> clusterGroupInfos = new List<LODGroupInfo>();
            clusterGroupInfos.Add(current);
            remaining.RemoveAt(0);

            for (int i = remaining.Count - 1; i >= 0; i--)
            {
                float dist = Vector3.Distance(current.Center, remaining[i].Center);
                if (dist <= groupingRadius)
                {
                    clusterGroupInfos.Add(remaining[i]);
                    remaining.RemoveAt(i);
                }
            }

            BuildSingleClusterFromLODGroupInfos(clusterGroupInfos);
        }
    }

    // --------------------------------------------
    // KeepOriginalLODs => CellBased
    // --------------------------------------------
    private void BuildClustersCellBased_KeepOriginalLODGroups(List<LODGroupInfo> groupInfos)
    {
        // Group entire LODGroups by which cell their bounding center falls into
        if (groupInfos.Count == 0) return;

        Dictionary<Vector3Int, List<LODGroupInfo>> cellDict = new Dictionary<Vector3Int, List<LODGroupInfo>>();

        foreach (var info in groupInfos)
        {
            // Cell coordinate based on center
            Vector3 c = info.Center;
            Vector3Int cellCoord = new Vector3Int(
                Mathf.FloorToInt(c.x / cellSize.x),
                Mathf.FloorToInt(c.y / cellSize.y),
                Mathf.FloorToInt(c.z / cellSize.z)
            );

            if (!cellDict.ContainsKey(cellCoord))
            {
                cellDict[cellCoord] = new List<LODGroupInfo>();
            }
            cellDict[cellCoord].Add(info);
        }

        // Build one cluster per cell
        foreach (var kvp in cellDict)
        {
            List<LODGroupInfo> list = kvp.Value;
            if (list.Count == 0) continue;
            BuildSingleClusterFromLODGroupInfos(list);
        }
    }

    private void BuildSingleClusterFromLODGroupInfos(List<LODGroupInfo> groupInfos)
    {
        var firstGroup = groupInfos[0];
        var initialRenderer = firstGroup.Renderers[0];

        Cluster newCluster = new Cluster(initialRenderer, hasLODGroups: true);

        // add all from first group
        for (int i = 1; i < firstGroup.Renderers.Count; i++)
        {
            newCluster.AddRenderer(firstGroup.Renderers[i]);
        }

        // add all other groups
        for (int g = 1; g < groupInfos.Count; g++)
        {
            var LODGroupRenders = groupInfos[g].Renderers;
            for (int r = 0; r < LODGroupRenders.Count; r++)
            {
                newCluster.AddRenderer(LODGroupRenders[r]);
            }
        }

        newCluster.CalculateTriangles();
        clusters.Add(newCluster);
    }

    private List<Vector3> InitializeRandomCentroids_KeepOriginalLODGroups(List<LODGroupInfo> groupInfos, int k)
    {
        if (groupInfos.Count == 0)
            return new List<Vector3>();

        Bounds b = new Bounds(groupInfos[0].Center, Vector3.zero);
        foreach (var gi in groupInfos)
            b.Encapsulate(gi.Center);

        List<Vector3> centroids = new List<Vector3>();
        for (int i = 0; i < k; i++)
        {
            float x = UnityEngine.Random.Range(b.min.x, b.max.x);
            float y = UnityEngine.Random.Range(b.min.y, b.max.y);
            float z = UnityEngine.Random.Range(b.min.z, b.max.z);
            centroids.Add(new Vector3(x, y, z));
        }
        return centroids;
    }

    // --------------------------------------------
    // Build Clusters for standard LOD-based or no-LOD renderers
    // --------------------------------------------
    private void BuildClustersForRenderers(List<RendererWithLODLevel> renderersWithLOD, int maxLODLevel, bool hasLODGroups)
    {
        Log($"Building clusters for {renderersWithLOD.Count} renderers. hasLODGroups = {hasLODGroups}, maxLODLevel = {maxLODLevel}");

        if (selectedAlgorithm == ClusteringAlgorithm.KMeans)
        {
            BuildClustersKMeans(renderersWithLOD, maxLODLevel, hasLODGroups);
        }
        else
        {
            // Proximity-based
            BuildClustersProximity(renderersWithLOD, maxLODLevel, hasLODGroups);
        }
    }

    private void BuildClustersProximity(List<RendererWithLODLevel> renderersWithLOD, int maxLODLevel, bool hasLODGroups)
    {
        Log("Using Proximity-Based clustering...");
        List<RendererWithLODLevel> remainingRenderers = new List<RendererWithLODLevel>(renderersWithLOD);

        while (remainingRenderers.Count > 0)
        {
            RendererWithLODLevel currentRenderer = remainingRenderers[0];
            Cluster newCluster = new Cluster(currentRenderer, hasLODGroups: hasLODGroups);
            remainingRenderers.RemoveAt(0);

            for (int i = remainingRenderers.Count - 1; i >= 0; i--)
            {
                // must have same material to combine
                if (Vector3.Distance(currentRenderer.Renderer.transform.position, remainingRenderers[i].Renderer.transform.position) <= groupingRadius
                    && currentRenderer.Renderer.sharedMaterial == remainingRenderers[i].Renderer.sharedMaterial)
                {
                    newCluster.AddRenderer(remainingRenderers[i]);
                    remainingRenderers.RemoveAt(i);
                }
            }

            newCluster.CalculateTriangles();
            // apply subdiv for large clusters:
            clusters.AddRange(SubdivideClusterRecursive(newCluster, 0, maxLODLevel));
        }
    }

    private void BuildClustersKMeans(List<RendererWithLODLevel> renderersWithLOD, int maxLODLevel, bool hasLODGroups)
    {
        Log("Using K-Means clustering...");
        // group by material so we don't combine different materials in one cluster
        Dictionary<Material, List<RendererWithLODLevel>> matGroups = new Dictionary<Material, List<RendererWithLODLevel>>();

        foreach (RendererWithLODLevel renderer in renderersWithLOD)
        {
            Material mat = renderer.Renderer.sharedMaterial;
            if (!matGroups.ContainsKey(mat))
            {
                matGroups[mat] = new List<RendererWithLODLevel>();
            }
            matGroups[mat].Add(renderer);
        }

        foreach (var materialGroup in matGroups)
        {
            Material groupMat = materialGroup.Key;
            List<RendererWithLODLevel> groupRenderers = materialGroup.Value;

            Log($"K-Means on material: {groupMat.name} with {groupRenderers.Count} renderers");
            List<Vector3> centroids = InitializeRandomCentroids(groupRenderers, kClusters);

            for (int iteration = 0; iteration < kMeansIterations; iteration++)
            {
                Dictionary<int, List<RendererWithLODLevel>> clusterAssignments = new Dictionary<int, List<RendererWithLODLevel>>();
                for (int i = 0; i < kClusters; i++)
                {
                    clusterAssignments[i] = new List<RendererWithLODLevel>();
                }

                foreach (RendererWithLODLevel rend in groupRenderers)
                {
                    int nearestCentroidIndex = FindNearestCentroidIndex(rend.Renderer.transform.position, centroids);
                    clusterAssignments[nearestCentroidIndex].Add(rend);
                }

                // recalc centroid
                for (int i = 0; i < kClusters; i++)
                {
                    if (clusterAssignments[i].Count > 0)
                    {
                        Vector3 newCentroid = Vector3.zero;
                        foreach (RendererWithLODLevel rend in clusterAssignments[i])
                        {
                            newCentroid += rend.Renderer.transform.position;
                        }
                        centroids[i] = newCentroid / clusterAssignments[i].Count;
                    }
                }

                if (iteration == kMeansIterations - 1)
                {
                    // final => build cluster objects
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

    // --------------------------------------------
    // NEW: Cell-based for normal (non-KeepOriginalLODs)
    // --------------------------------------------
    private void BuildClustersCellBased(List<RendererWithLODLevel> renderers, int maxLODLevel, bool hasLODGroups)
    {
        Log("Using Cell-Based clustering...");
        // We'll skip subdividing by triangle limit here
        // We'll also group by (material, cellCoord) so we don't combine different materials in one cluster.

        Dictionary<(Material, int, Vector3Int), Cluster> cellClusters = new Dictionary<(Material, int, Vector3Int), Cluster>();

        foreach (var rw in renderers)
        {
            var mr = rw.Renderer;
            if (!mr) continue;

            Material mat = mr.sharedMaterial;
            int lod = rw.LODLevel;
            Vector3 pos = mr.transform.position;

            Vector3Int cellCoord = new Vector3Int(
                Mathf.FloorToInt(pos.x / cellSize.x),
                Mathf.FloorToInt(pos.y / cellSize.y),
                Mathf.FloorToInt(pos.z / cellSize.z)
            );

            var key = (mat, lod, cellCoord);
            if (!cellClusters.ContainsKey(key))
            {
                // create brand new cluster
                Cluster newC = new Cluster(rw, hasLODGroups);
                cellClusters[key] = newC;
            }
            else
            {
                cellClusters[key].AddRenderer(rw);
            }
        }

        // finalize
        foreach (var kvp in cellClusters)
        {
            var cluster = kvp.Value;
            cluster.CalculateTriangles();
            clusters.Add(cluster); // no subdiv call
        }
    }

    private List<Vector3> InitializeRandomCentroids(List<RendererWithLODLevel> renderersWithLOD, int k)
    {
        List<Vector3> centroids = new List<Vector3>();
        if (renderersWithLOD.Count == 0) return centroids;

        Bounds sceneBounds = new Bounds(renderersWithLOD[0].Renderer.transform.position, Vector3.zero);

        foreach (RendererWithLODLevel r in renderersWithLOD)
        {
            sceneBounds.Encapsulate(r.Renderer.bounds);
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
        // If user is using "CellBased," we skip subdivision entirely. 
        // But let's guard if we get here anyway.
        if (selectedAlgorithm == ClusteringAlgorithm.CellBased)
        {
            // no subdiv for cell-based
            return new List<Cluster> { cluster };
        }

        Log($"Checking cluster for subdivision at level {level}. Cluster Triangles = {cluster.TotalTriangles}, Limit = {triangleLimit}");
        if (level >= MaxRecursionDepth)
        {
            Debug.LogWarning($"Maximum recursion depth reached for cluster. Some objects may exceed the triangle limit.");
            return new List<Cluster> { cluster };
        }

        if (cluster.TotalTriangles <= triangleLimit)
        {
            Log("No subdivision needed for this cluster.");
            return new List<Cluster> { cluster };
        }

        Log("Subdividing cluster...");
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

    // ----------------------------------------------------
    // Combine Clusters Only
    // ----------------------------------------------------
    private void CombineClustersOnly()
    {
        Log("Starting CombineClustersOnly...");
        if (!clustersBuilt)
        {
            Debug.LogError("Please build clusters first.");
            return;
        }

        GameObject combineOnlyParent = new GameObject(newParentName + "_CombineOnly");
        Undo.RegisterCreatedObjectUndo(combineOnlyParent, "Create CombineOnly Parent");
        combineOnlyParent.transform.position = Vector3.zero;
        combineOnlyParent.transform.localScale = Vector3.one;

        if (markCombinedStatic) combineOnlyParent.isStatic = true;

        Log($"Created combine-only parent GameObject: {combineOnlyParent.name}");

        foreach (Cluster cluster in clusters)
        {
            string combinedName = $"{newParentName}_CombOnly_{(cluster.Material ? cluster.Material.name : "MultiMat")}";
            Log($"Combining objects into {combinedName} without grouping sources.");

            GameObject combinedObject = new GameObject(combinedName);
            Undo.RegisterCreatedObjectUndo(combinedObject, "Create Combined Object");
            combinedObject.transform.SetParent(combineOnlyParent.transform);
            combinedObject.transform.localPosition = Vector3.zero;
            combinedObject.transform.localScale = Vector3.one;

            if (markCombinedStatic) combinedObject.isStatic = true;

            if (!CombineClusterMeshes(cluster, combinedObject))
            {
                Debug.LogWarning($"Failed to combine meshes for {combinedName}. Objects not combined.");
                continue;
            }

            // Destroy or disable original
            foreach (var lodList in cluster.RenderersPerLODLevel.Values)
            {
                foreach (RendererWithLODLevel renderer in lodList)
                {
                    if (destroySourceObjects)
                    {
                        Log($"Destroying source object {renderer.Renderer.name}");
                        Undo.DestroyObjectImmediate(renderer.Renderer.gameObject);
                    }
                    else
                    {
                        if (disableSourceRenderers)
                        {
                            Log($"Disabling source renderer {renderer.Renderer.name}");
                            Undo.RecordObject(renderer.Renderer, "Disable MeshRenderer");
                            renderer.Renderer.enabled = false;
                        }
                    }
                }
            }
        }

        Log("CombineClustersOnly complete. Clearing clusters.");
        clustersBuilt = false;
        clusters.Clear();
        SceneView.RepaintAll();
    }

    private void GroupAndCombineClusters()
    {
        Log("Starting GroupAndCombineClusters...");
        if (!clustersBuilt)
        {
            Debug.LogError("Please build clusters first.");
            return;
        }

        GameObject newParent = new GameObject(newParentName);
        Undo.RegisterCreatedObjectUndo(newParent, "Create New Parent");
        newParent.transform.position = Vector3.zero;
        newParent.transform.localScale = Vector3.one;

        if (markCombinedStatic) newParent.isStatic = true;

        Log($"Created new parent GameObject: {newParent.name}");

        foreach (Cluster cluster in clusters)
        {
            string groupName = $"{newParentName}_Group_{(cluster.Material ? cluster.Material.name : "MultiMat")}";
            Log($"Grouping and combining objects into {groupName}");

            GameObject groupParent = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(groupParent, "Create Group Parent");
            groupParent.transform.SetParent(newParent.transform);
            groupParent.transform.localPosition = Vector3.zero;
            groupParent.transform.localScale = Vector3.one;

            if (markCombinedStatic) groupParent.isStatic = true;

            GameObject combinedObject = new GameObject($"{groupName}_combined");
            Undo.RegisterCreatedObjectUndo(combinedObject, "Create Combined Object");
            combinedObject.transform.SetParent(groupParent.transform);
            combinedObject.transform.localPosition = Vector3.zero;
            combinedObject.transform.localScale = Vector3.one;

            if (markCombinedStatic) combinedObject.isStatic = true;

            Log($"Created combined GameObject: {combinedObject.name}");

            GameObject sourceObjectsParent = new GameObject($"{groupName}_sources");
            Undo.RegisterCreatedObjectUndo(sourceObjectsParent, "Create Source Objects Parent");
            sourceObjectsParent.transform.SetParent(groupParent.transform);
            sourceObjectsParent.transform.localPosition = Vector3.zero;
            sourceObjectsParent.transform.localScale = Vector3.one;

            if (markCombinedStatic) sourceObjectsParent.isStatic = true;

            foreach (var lodList in cluster.RenderersPerLODLevel.Values)
            {
                foreach (RendererWithLODLevel renderer in lodList)
                {
                    Log($"Moving renderer {renderer.Renderer.name} under {sourceObjectsParent.name}");
                    Undo.SetTransformParent(renderer.Renderer.transform, sourceObjectsParent.transform, "Group Source Object");
                }
            }

            if (!CombineClusterMeshes(cluster, combinedObject))
            {
                Debug.LogWarning($"Failed to combine meshes for {groupName}. Objects are grouped but not combined.");
                continue;
            }

            // either destroy or disable
            foreach (var lodList in cluster.RenderersPerLODLevel.Values)
            {
                foreach (RendererWithLODLevel renderer in lodList)
                {
                    if (destroySourceObjects)
                    {
                        Log($"Destroying source object {renderer.Renderer.name}");
                        Undo.DestroyObjectImmediate(renderer.Renderer.gameObject);
                    }
                    else
                    {
                        Log($"Disabling source object {renderer.Renderer.name}");
                        Undo.RecordObject(renderer.Renderer.gameObject, "Disable Original Object");
                        renderer.Renderer.gameObject.SetActive(false);
                    }
                }
            }

            if (destroySourceObjects)
            {
                Log($"Destroying source objects parent {sourceObjectsParent.name}");
                Undo.DestroyObjectImmediate(sourceObjectsParent);
            }
        }

        Log("GroupAndCombineClusters complete. Clearing clusters.");
        clustersBuilt = false;
        clusters.Clear();
        SceneView.RepaintAll();
    }

    private bool CombineClusterMeshes(Cluster cluster, GameObject parent)
    {
        if (cluster == null || cluster.TotalRenderers == 0) return false;

        Log($"Starting mesh combination for cluster. Parent object: {parent.name}");

        int maxLODLevel = cluster.GetMaxLODLevel();
        List<LOD> lods = new List<LOD>();

        // Calculate overall bounding center so we can recenter if needed
        Vector3 centerOffset = Vector3.zero;
        Bounds combinedBounds = new Bounds();
        bool boundsInitialized = false;

        // Collect all renderers
        List<RendererWithLODLevel> allRenderers = new List<RendererWithLODLevel>();
        foreach (var lodList in cluster.RenderersPerLODLevel.Values)
        {
            allRenderers.AddRange(lodList);
        }

        // Calculate bounding
        foreach (RendererWithLODLevel rwlod in allRenderers)
        {
            MeshFilter mf = rwlod.Renderer.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Mesh mesh = mf.sharedMesh;
            foreach (var v in mesh.vertices)
            {
                Vector3 worldPt = mf.transform.localToWorldMatrix.MultiplyPoint3x4(v);
                if (!boundsInitialized)
                {
                    combinedBounds = new Bounds(worldPt, Vector3.zero);
                    boundsInitialized = true;
                }
                else
                {
                    combinedBounds.Encapsulate(worldPt);
                }
            }
        }
        centerOffset = combinedBounds.center;
        parent.transform.position = centerOffset;

        // If cluster has no LODGroups, do single LOD
        if (!cluster.HasLODGroups)
        {
            // We effectively treat everything as LOD0 if LOD == -1
            cluster.RenderersPerLODLevel.TryGetValue(-1, out List<RendererWithLODLevel> noLODList);
            if (noLODList == null || noLODList.Count == 0)
            {
                // Possibly the user forced LOD0, let's check that as well
                cluster.RenderersPerLODLevel.TryGetValue(0, out noLODList);
            }

            if (noLODList == null || noLODList.Count == 0)
            {
                // nothing to combine
                return false;
            }

            // We'll combine them by material
            Dictionary<Material, List<CombineInstance>> matToCombines = new Dictionary<Material, List<CombineInstance>>();

            foreach (var rwlod in noLODList)
            {
                MeshFilter mf = rwlod.Renderer.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                Material mat = rwlod.Renderer.sharedMaterial;
                if (!matToCombines.ContainsKey(mat))
                {
                    matToCombines[mat] = new List<CombineInstance>();
                }
                matToCombines[mat].Add(new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    transform = mf.transform.localToWorldMatrix
                });
            }

            // Create child object(s)
            List<Renderer> subRenderers = new List<Renderer>();
            foreach (var kvp in matToCombines)
            {
                Material mat = kvp.Key;
                List<CombineInstance> cList = kvp.Value;
                if (cList.Count == 0) continue;

                Mesh combinedMesh = new Mesh();
                int totalVerts = cList.Sum(ci => ci.mesh.vertexCount);
                if (totalVerts > 65535)
                    combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                combinedMesh.CombineMeshes(cList.ToArray(), true, true);
                if (rebuildNormals) combinedMesh.RecalculateNormals();
                if (rebuildLightmapUV) Unwrapping.GenerateSecondaryUVSet(combinedMesh);

                // Recenter vertices
                Vector3[] verts = combinedMesh.vertices;
                for (int i = 0; i < verts.Length; i++)
                {
                    verts[i] -= centerOffset;
                }
                combinedMesh.vertices = verts;
                combinedMesh.RecalculateBounds();

                // Save mesh asset
                SaveMeshAsset(combinedMesh, $"{parent.name}_NoLOD_{mat.name}");

                // Create child
                GameObject matChild = new GameObject($"{parent.name}_NoLOD_{mat.name}_Combined");
                matChild.transform.SetParent(parent.transform);
                matChild.transform.localPosition = Vector3.zero;
                matChild.transform.localScale = Vector3.one;

                if (markCombinedStatic) matChild.isStatic = true;

                MeshFilter newMF = matChild.AddComponent<MeshFilter>();
                newMF.sharedMesh = combinedMesh;

                MeshRenderer newMR = matChild.AddComponent<MeshRenderer>();
                newMR.sharedMaterial = mat;

                // Only add MeshCollider if addMeshCollider is true AND this is LOD0 or no LOD
                // (since we are effectively at LOD0 here)
                if (addMeshCollider)
                {
                    MeshCollider coll = matChild.AddComponent<MeshCollider>();
                    coll.sharedMesh = combinedMesh;
                }

                subRenderers.Add(newMR);
            }

            // We only have one LOD => full detail
            if (subRenderers.Count > 0)
            {
                LODGroup lodGroup = parent.AddComponent<LODGroup>();
                LOD[] singleLOD = new LOD[1];
                // typically LOD0 transition is around 0.6
                singleLOD[0] = new LOD(0.6f, subRenderers.ToArray());
                lodGroup.SetLODs(singleLOD);
                lodGroup.RecalculateBounds();
            }
        }
        else
        {
            // We have real LOD groups => combine per LOD, possibly including the -1 if present
            for (int lodLevel = 0; lodLevel <= maxLODLevel; lodLevel++)
            {
                cluster.RenderersPerLODLevel.TryGetValue(lodLevel, out List<RendererWithLODLevel> lodObjects);
                // incorporate any no-lod objects into LOD0
                if (lodLevel == 0 && cluster.RenderersPerLODLevel.TryGetValue(-1, out List<RendererWithLODLevel> noLOD))
                {
                    if (lodObjects == null) lodObjects = new List<RendererWithLODLevel>();
                    lodObjects.AddRange(noLOD);
                }

                if (lodObjects == null || lodObjects.Count == 0)
                {
                    Log($"No objects at LOD {lodLevel}, skip...");
                    continue;
                }

                // We'll combine them per material
                Dictionary<Material, List<CombineInstance>> matToCombines = new Dictionary<Material, List<CombineInstance>>();

                foreach (RendererWithLODLevel rwlod in lodObjects)
                {
                    MeshFilter mf = rwlod.Renderer.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null)
                        continue;

                    Material mat = rwlod.Renderer.sharedMaterial;
                    if (!matToCombines.ContainsKey(mat))
                        matToCombines[mat] = new List<CombineInstance>();

                    matToCombines[mat].Add(new CombineInstance
                    {
                        mesh = mf.sharedMesh,
                        transform = mf.transform.localToWorldMatrix
                    });
                }

                List<Renderer> lodRenderers = new List<Renderer>();
                foreach (var kvp in matToCombines)
                {
                    Material mat = kvp.Key;
                    List<CombineInstance> cList = kvp.Value;
                    if (cList.Count == 0) continue;

                    Mesh combinedMesh = new Mesh();
                    int totalVerts = cList.Sum(ci => ci.mesh.vertexCount);
                    if (totalVerts > 65535)
                        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                    combinedMesh.CombineMeshes(cList.ToArray(), true, true);
                    if (rebuildNormals) combinedMesh.RecalculateNormals();
                    if (rebuildLightmapUV) Unwrapping.GenerateSecondaryUVSet(combinedMesh);

                    // recenter
                    Vector3[] verts = combinedMesh.vertices;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        verts[i] -= centerOffset;
                    }
                    combinedMesh.vertices = verts;
                    combinedMesh.RecalculateBounds();

                    // Save mesh asset
                    SaveMeshAsset(combinedMesh, $"{parent.name}_LOD{lodLevel}_{mat.name}");

                    GameObject matChild = new GameObject($"{parent.name}_LOD{lodLevel}_{mat.name}_Combined");
                    matChild.transform.SetParent(parent.transform);
                    matChild.transform.localPosition = Vector3.zero;
                    matChild.transform.localScale = Vector3.one;

                    if (markCombinedStatic) matChild.isStatic = true;

                    MeshFilter newMF = matChild.AddComponent<MeshFilter>();
                    newMF.sharedMesh = combinedMesh;

                    MeshRenderer newMR = matChild.AddComponent<MeshRenderer>();
                    newMR.sharedMaterial = mat;

                    // **Only** add MeshCollider if this is LOD0
                    if (addMeshCollider && lodLevel == 0)
                    {
                        MeshCollider coll = matChild.AddComponent<MeshCollider>();
                        coll.sharedMesh = combinedMesh;
                    }

                    lodRenderers.Add(newMR);
                }

                if (lodRenderers.Count > 0)
                {
                    float t = GetLODScreenTransitionHeight(lodLevel, maxLODLevel);
                    lods.Add(new LOD(t, lodRenderers.ToArray()));
                }
            }

            if (lods.Count > 0)
            {
                LODGroup lodGroup = parent.AddComponent<LODGroup>();
                lodGroup.SetLODs(lods.ToArray());
                lodGroup.RecalculateBounds();
            }
        }

        Log($"Mesh combination complete for cluster. Parent: {parent.name}");
        return true;
    }

    private float GetLODScreenTransitionHeight(int lodLevel, int maxLODLevel)
    {
        float[] defaultTransitionHeights = { 0.6f, 0.4f, 0.2f, 0.1f, 0.05f };
        if (lodLevel < defaultTransitionHeights.Length)
        {
            return defaultTransitionHeights[lodLevel];
        }
        else
        {
            return 0.01f;
        }
    }

    private void SaveMeshAsset(Mesh mesh, string objectName)
    {
        Log($"Saving mesh asset for {objectName}...");
        if (!Directory.Exists(MeshSavePath))
        {
            Directory.CreateDirectory(MeshSavePath);
        }

        string uniqueId = Guid.NewGuid().ToString("N");
        string assetPath = $"{MeshSavePath}/{objectName}_{uniqueId}.asset";
        AssetDatabase.CreateAsset(mesh, assetPath);
        AssetDatabase.SaveAssets();

        Log($"Mesh saved as: {assetPath}");
    }

    private void GroupObjects()
    {
        Log("Starting GroupObjects...");
        if (!clustersBuilt)
        {
            Debug.LogError("Please build clusters first.");
            return;
        }

        GameObject newParent = new GameObject(newParentName);
        Undo.RegisterCreatedObjectUndo(newParent, "Create New Parent");
        newParent.transform.position = Vector3.zero;
        newParent.transform.localScale = Vector3.one;

        if (markCombinedStatic) newParent.isStatic = true;

        Log($"Created group-only parent GameObject: {newParent.name}");

        for (int i = 0; i < clusters.Count; i++)
        {
            Cluster cluster = clusters[i];
            string groupName = $"{newParentName}_Group{i + 1}";

            GameObject groupParent = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(groupParent, "Create Group Parent");
            groupParent.transform.SetParent(newParent.transform);
            groupParent.transform.position = cluster.Center;

            if (markCombinedStatic) groupParent.isStatic = true;

            Log($"Created group parent: {groupParent.name} at position {cluster.Center}");

            foreach (var lodList in cluster.RenderersPerLODLevel.Values)
            {
                foreach (RendererWithLODLevel renderer in lodList)
                {
                    Log($"Moving renderer {renderer.Renderer.name} under {groupParent.name}");
                    Undo.SetTransformParent(renderer.Renderer.transform, groupParent.transform, "Set Parent");
                }
            }
        }

        Log("GroupObjects complete. Clearing clusters.");
        clustersBuilt = false;
        clusters.Clear();
        SceneView.RepaintAll();
    }

    private void ListMaterials()
    {
        Log("Listing materials...");
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
        Log($"Found {foundMaterials.Count} unique materials.");
        Repaint(); // Repaint the GUI to show the materials
    }

    private MeshRenderer[] GetAllRelevantRenderers()
    {
        List<MeshRenderer> result = new List<MeshRenderer>();
        foreach (var p in parentObjects)
        {
            if (p)
            {
                result.AddRange(p.GetComponentsInChildren<MeshRenderer>(true));
            }
        }
        return result.ToArray();
    }

    private MeshRenderer[] GetFilteredRenderersArray()
    {
        List<MeshRenderer> finalList = new List<MeshRenderer>();
        foreach (var r in GetAllRelevantRenderers())
        {
            if (PassesFilters(r)) finalList.Add(r);
        }
        return finalList.ToArray();
    }

    private List<MeshRenderer> GetFilteredRenderers()
    {
        return GetFilteredRenderersArray().ToList();
    }

    private bool PassesFilters(MeshRenderer renderer)
    {
        if (onlyActive && !renderer.gameObject.activeInHierarchy) return false;
        if (onlyActiveMeshRenderers && !renderer.enabled) return false;
        if (onlyStatic && !renderer.gameObject.isStatic) return false;
        if (useTag && !string.IsNullOrEmpty(tagToUse) && renderer.tag != tagToUse) return false;
        if (useLayer && renderer.gameObject.layer != layerToUse) return false;
        if (useNameContains && !string.IsNullOrEmpty(nameContainsString)
            && !renderer.name.ToLower().Contains(nameContainsString.ToLower())) return false;

        return true;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!clustersBuilt) return;

        foreach (Cluster cluster in clusters)
        {
            Color color;
            if (cluster.TotalTriangles > triangleLimit && selectedAlgorithm != ClusteringAlgorithm.CellBased)
            {
                // only highlight if over limit in Proximity/KMeans
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

            if (cluster.HasLODGroups) Handles.color = Color.blue;
            else Handles.color = Color.green;

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
        if (!material)
        {
            // fallback color
            return MainGroupColor;
        }

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
        Log("IntelligentMeshCombiner OnEnable called, subscribing to SceneView.duringSceneGui.");
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        Log("IntelligentMeshCombiner OnDisable called, unsubscribing from SceneView.duringSceneGui.");
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private class Cluster
    {
        public Dictionary<int, List<RendererWithLODLevel>> RenderersPerLODLevel { get; private set; }
            = new Dictionary<int, List<RendererWithLODLevel>>();
        public Vector3 Center { get; private set; }
        public int TotalTriangles { get; private set; }
        public bool IsSubdivided { get; private set; }
        public int SubdivisionLevel { get; private set; }
        public Material Material { get; private set; }

        public float GizmoRadius { get; private set; } // For visualization

        public int TotalRenderers
        {
            get
            {
                return RenderersPerLODLevel.Values.Sum(list => list.Count);
            }
        }

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
            if (!Material && rendererWithLOD.Renderer.sharedMaterial)
            {
                Material = rendererWithLOD.Renderer.sharedMaterial;
            }
            else if (Material != rendererWithLOD.Renderer.sharedMaterial)
            {
                // We might have multi-material scenario. We'll keep a reference to the first,
                // but final combine code splits by actual material.
            }

            int lodLevel = rendererWithLOD.LODLevel;
            if (!RenderersPerLODLevel.ContainsKey(lodLevel))
            {
                RenderersPerLODLevel[lodLevel] = new List<RendererWithLODLevel>();
            }
            RenderersPerLODLevel[lodLevel].Add(rendererWithLOD);

            RecalculateCenter();
            CalculateGizmoRadius();
        }

        private void RecalculateCenter()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var lodList in RenderersPerLODLevel.Values)
            {
                foreach (RendererWithLODLevel rwlod in lodList)
                {
                    sum += rwlod.Renderer.transform.position;
                    count++;
                }
            }
            Center = (count > 0) ? sum / count : Vector3.zero;
        }

        public void CalculateTriangles()
        {
            TotalTriangles = 0;
            foreach (var lodList in RenderersPerLODLevel.Values)
            {
                foreach (RendererWithLODLevel rwlod in lodList)
                {
                    MeshFilter mf = rwlod.Renderer.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        TotalTriangles += mf.sharedMesh.triangles.Length / 3;
                    }
                }
            }
        }

        private void CalculateGizmoRadius()
        {
            // A simple approach: max distance from center among renderers
            GizmoRadius = 0f;
            foreach (var lodList in RenderersPerLODLevel.Values)
            {
                foreach (RendererWithLODLevel rwlod in lodList)
                {
                    float d = Vector3.Distance(rwlod.Renderer.transform.position, Center);
                    if (d > GizmoRadius) GizmoRadius = d;
                }
            }
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

    // Gathers LOD-enabled and non-LOD renderers
    private void GetRenderersWithLODLevels(
        out List<RendererWithLODLevel> renderersWithLOD,
        out List<RendererWithLODLevel> renderersWithoutLOD,
        out int maxLODLevel)
    {
        renderersWithLOD = new List<RendererWithLODLevel>();
        renderersWithoutLOD = new List<RendererWithLODLevel>();
        maxLODLevel = 0;
        HashSet<MeshRenderer> processed = new HashSet<MeshRenderer>();

        foreach (var parent in parentObjects)
        {
            if (!parent) continue;

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
                        if (mr != null && PassesFilters(mr) && !processed.Contains(mr))
                        {
                            renderersWithLOD.Add(new RendererWithLODLevel(mr, i));
                            processed.Add(mr);
                        }
                    }
                }
            }

            // also process any MeshRenderers not in a LODGroup
            var allRenderers = parent.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in allRenderers)
            {
                if (!processed.Contains(r) && PassesFilters(r))
                {
                    renderersWithoutLOD.Add(new RendererWithLODLevel(r, -1));
                    processed.Add(r);
                }
            }
        }
    }

    private void GetAllRenderersWithLODLevels(
        out List<RendererWithLODLevel> allRenderersWithLODLevels,
        out int maxLODLevel)
    {
        allRenderersWithLODLevels = new List<RendererWithLODLevel>();
        maxLODLevel = 0;
        HashSet<MeshRenderer> processed = new HashSet<MeshRenderer>();

        foreach (var parent in parentObjects)
        {
            if (parent == null) continue;

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
                        if (mr != null && PassesFilters(mr) && !processed.Contains(mr))
                        {
                            allRenderersWithLODLevels.Add(new RendererWithLODLevel(mr, i));
                            processed.Add(mr);
                        }
                    }
                }
            }

            var everyRenderer = parent.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in everyRenderer)
            {
                if (!processed.Contains(r) && PassesFilters(r))
                {
                    allRenderersWithLODLevels.Add(new RendererWithLODLevel(r, -1));
                    processed.Add(r);
                }
            }
        }
    }
}

// Simple instructions window
public class ToolInstructionsWindow : EditorWindow
{
    private Vector2 scrollPosition;

    public static void ShowWindow()
    {
        var window = GetWindow<ToolInstructionsWindow>("Tool Instructions");
        window.minSize = new Vector2(400, 300);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Intelligent Mesh Combiner - Instructions", EditorStyles.boldLabel);

        // Begin scroll view for the instructions
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField(
            "The Intelligent Mesh Combiner tool allows you to group and combine meshes based on different clustering algorithms:\n\n" +
            "1) K-Means\n" +
            "2) Proximity-based\n" +
            "3) Cell-based (grid) for handling large object counts.\n\n" +
            "It also supports different LOD handling modes:\n" +
            " - Combine LODs Separately\n" +
            " - Combine All\n" +
            " - Keep Original LODs.\n\n" +
            "You can also specify filters (static, active, tag, layer, etc.), " +
            "rebuild lightmap UVs, mark combined objects as static, and more.\n\n" +
            "Use the 'Rebuild Clusters' button whenever you change settings, then " +
            "choose how you want to group/combine.\n\n" +
            "Enjoy!",
            EditorStyles.wordWrappedLabel
        );

        // End scroll view
        EditorGUILayout.EndScrollView();
    }
}
