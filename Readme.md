# Intelligent Mesh Combiner for Unity

![IntelligentMeshCombiner](imc.png)

Intelligent Mesh Combiner is a powerful Unity Editor tool designed to optimize your scenes by intelligently grouping and combining meshes based on their proximity, materials, or cells. This tool offers a sophisticated approach to reducing draw calls while preserving the visual fidelity and spatial relationships of your Unity scenes.

## Features

- **Three** clustering algorithms: Proximity-based, **K-means**, and **Cell-Based**
- Adaptive cluster sizes (or fixed-size cells, in the case of Cell-Based)
- Automatic handling of multiple materials
- Creation of subgroups for clusters exceeding a specified triangle limit (Proximity/K-Means)
- Visualization of clusters with material-specific colors for main groups
- Adjustable gizmo sphere opacity and scale for better visualization (Proximity/K-Means)
- Saving of combined meshes as assets in an 'IMC_Meshes' folder
- **Filters** for selective combining: based on active state, static state, tags, layers, or name patterns
- Option to rebuild lightmap UVs for combined meshes
- Option to add mesh colliders to combined objects
- Marking of combined objects as static
- Option to destroy source objects after combining

## Quick Start and Installation

[Download Unity Package](https://github.com/roundyyy/intelligent_mesh_combiner/releases)

or

1. Clone or download this repository.
2. Copy the `IntelligentMeshCombiner.cs` file into your Unity project's `Editor` folder.
   - If you don't have an `Editor` folder, create one in your project's Assets directory.
3. The tool will appear in Unity under "Tools > Roundy > IntelligentMeshCombiner" in the top menu.

## Clustering Algorithms

### 1. Proximity-Based Clustering

- Groups objects based on their physical proximity within a specified radius
- Ideal for scenes with organically distributed objects
- Parameters:
  - **Grouping Radius**: Controls the maximum distance between objects in a cluster
  - **Subgroup Radius**: Defines the radius for creating smaller clusters within large groups
- **Respects Triangle Limit**: Large clusters get subdivided automatically

### 2. **K-Means Clustering**

- Groups objects using the K-means algorithm, which aims to partition n observations into k clusters
- Suitable for scenes where you want more control over the number of resulting clusters
- Parameters:
  - **Number of Clusters (K)**: Specifies the desired number of clusters
  - **Max Iterations**: Controls the maximum number of iterations for the K-means algorithm
- **Respects Triangle Limit**: Large clusters get subdivided automatically

### 3. **Cell-Based Clustering** (Recommended for Large Object Counts)

- Divides the scene into 3D cells of a specified size (e.g., 30×30×30 units)
- All objects that fall within the same cell (and share a material) are combined into a single cluster
- **No Triangle Subdivision**: Each cell is combined as-is, which simplifies the process for massive scenes
- Great for scenes with **100k+ objects** or when you want a predictable spatial partition (grid-based)

All algorithms respect material boundaries, ensuring that only objects with the same material are grouped together.

## Filters

You can fine-tune which objects are included in the clustering process using various filters:

- **Static Only**: Include only objects marked as static
- **Active Only**: Include only active objects in the scene
- **Active Mesh Renderers Only**: Filter out inactive mesh renderers
- **Tag Filtering**: Filter objects based on their tag
- **Layer Filtering**: Filter objects based on their layer
- **Name Contains**: Filter objects whose name contains a specific string

## How to Use

1. Open the IntelligentMeshCombiner window by selecting **Tools > IntelligentMeshCombiner** from the Unity menu.

2. In your scene, select a parent object (or multiple parents) containing the meshes you want to group and combine.

3. In the IntelligentMeshCombiner window:
   - Assign the selected parent object(s) to the **Parent Objects** list.
   - Choose between **Proximity-Based**, **K-Means**, or **Cell-Based** clustering algorithms.
   - Adjust the parameters for your chosen algorithm:
     - **Proximity-Based**: Set the *Grouping Radius* and *Subgroup Radius*.
     - **K-Means**: Set the *Number of Clusters (K)* and *Max Iterations*.
     - **Cell-Based**: Specify the *Cell Size* (e.g. 30×30×30).
   - Set the *Triangle Limit* (used only by Proximity/K-Means for automatic subdivision).
   - Configure additional options like rebuilding lightmap UVs, adding mesh colliders, etc.
   - Apply filters as needed (e.g., static objects, active only, tag/layer filtering).

4. Click **Rebuild Clusters** to analyze the objects and visualize the groupings in the scene view:
  
5. Review the cluster information and adjust settings if needed.

6. Choose an action:
   - **Group Objects Only**: Organize objects under new parent groups without combining meshes.
   - **Group and Combine Clusters**: Both group objects and create new combined mesh objects.
   - **Combine Clusters Only**: Combine into new meshes without grouping the sources (the source objects remain at their original hierarchy unless destroyed).

7. The tool will create new combined objects and save the combined meshes as assets in the **IMC_Meshes** folder within your project's Assets directory.

## LOD Handling

The Intelligent Mesh Combiner tool provides three flexible options for objects with (and without) LODGroups:

1. **Combine LODs Separately**  
   - Objects with LOD groups are processed separately from objects without LOD groups.  
   - Maintains existing LOD structures, combining them by LOD level.

2. **Combine All**  
   - Merges objects with no LODs into the same clusters as those with LODs, effectively unifying everything.  

3. **Keep Original LODs**  
   - Clusters each LODGroup as a single entity, preserving the original LODGroup boundaries entirely.

### How to Use
1. Select the parent objects containing the meshes.
2. Choose an LOD handling option in the IMC window:
   - **Combine LODs Separately**
   - **Combine All**
   - **Keep Original LODs**
3. Click **Rebuild Clusters** to analyze the objects based on your LOD handling selection.
4. Review the LOD clusters in the scene view to ensure that objects are grouped as expected.

## Tips

- For extremely large scenes (e.g., 100k+ objects), **Cell-Based Clustering** can often be faster to compute than Proximity or K-Means.
- Proximity-Based works well if you need adaptive group sizes that reflect actual distances.
- K-Means lets you explicitly control the number of clusters.
- Always make a backup of your scene before performing large-scale mesh combining operations.
- For large scenes, consider combining meshes in sections rather than everything at once.
- Pay attention to material-specific colors in the scene gizmos (Proximity/K-Means) to ensure objects are being grouped as expected.

## TO DO

= Speed it up. Too slow when combining lot of objects same time (due to clustering computations, especially on huge scenes)

## Limitations

- Objects with different materials cannot be combined into a single mesh.
- Skinned meshes are not supported for combination.

