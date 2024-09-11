# Intelligent Mesh Combiner for Unity

![IntelligentMeshCombiner](imc.png)

Intelligent Mesh Combiner is a powerful Unity Editor tool designed to optimize your scenes by intelligently grouping and combining meshes based on their proximity and materials. This tool offers a sophisticated approach to reducing draw calls while preserving the visual fidelity and spatial relationships of your Unity scenes.

## Features

- Two clustering algorithms: Proximity-based and K-means
- Adaptive cluster sizes that respect scene layout
- Automatic handling of multiple materials
- Creation of subgroups for clusters exceeding a specified triangle limit
- Visualization of clusters with material-specific colors for main groups
- Adjustable gizmo sphere opacity and scale for better visualization
- Saving of combined meshes as assets in an 'IMC_Meshes' folder
- Option to rebuild lightmap UVs for combined meshes
- Option to add mesh colliders to combined objects
- Marking of combined objects as static
- Option to destroy source objects after combining



## Quick Start and Installation

[Download Unity Package](https://github.com/roundyyy/intelligent_mesh_combiner/releases/download/v02/IntelligentMEshCombiner_02.unitypackage)

or

1. Clone or download this repository.
2. Copy the `IntelligentMeshCombiner.cs` file into your Unity project's `Editor` folder.
   - If you don't have an `Editor` folder, create one in your project's Assets directory.
3. The tool will appear in Unity under "Tools > IntelligentMeshCombiner" in the top menu.

## Clustering Algorithms

### 1. Proximity-Based Clustering

- Groups objects based on their physical proximity within a specified radius
- Ideal for scenes with organically distributed objects
- Parameters:
  - Grouping Radius: Controls the maximum distance between objects in a cluster
  - Subgroup Radius: Defines the radius for creating smaller clusters within large groups

### 2. K-Means Clustering

- Groups objects using the K-means algorithm, which aims to partition n observations into k clusters
- Suitable for scenes where you want more control over the number of resulting clusters
- Parameters:
  - Number of Clusters (K): Specifies the desired number of clusters
  - Max Iterations: Controls the maximum number of iterations for the K-means algorithm

Both algorithms respect material boundaries, ensuring that only objects with the same material are grouped together.

## How to Use

1. Open the IntelligentMeshCombiner window by selecting "Tools > IntelligentMeshCombiner" from the Unity menu.

2. In your scene, select a parent object containing the meshes you want to group and combine.

3. In the IntelligentMeshCombiner window:
   - Assign the selected parent object to the "Parent Object" field.
   - Choose between Proximity-Based and K-Means clustering algorithms.
   - Adjust the parameters for your chosen algorithm:
     - For Proximity-Based: Set the "Grouping Radius" and "Subgroup Radius"
     - For K-Means: Set the "Number of Clusters (K)" and "Max Iterations"
   - Set the "Triangle Limit" to control when subgroups are created.
   - Configure additional options like rebuilding lightmap UVs, adding mesh colliders, etc.
   - Adjust the gizmo sphere opacity and scale for better visualization in the scene view.

4. Click "Rebuild Clusters" to analyze the objects and visualize the groupings in the scene view.
   - Main groups will be displayed as colored spheres, with subtle variations for different materials.
   - Subgroups will appear as green spheres.
   - Groups exceeding the triangle limit will be shown in red.

5. Review the cluster information and adjust settings if needed.

6. Choose an action:
   - Click "Group Objects Only" to organize objects without combining meshes.
   - Click "Group and Combine Clusters" to both group and combine meshes.

7. The tool will create new combined objects and save the combined meshes as assets in the 'IMC_Meshes' folder within your project's Assets directory.

## Advantages Over Cell-Based Grouping

IntelligentMeshCombiner offers several advantages over traditional cell-based grouping methods:

1. **Adaptive Clustering**: Unlike fixed-size cells, our algorithms adapt to the natural distribution of objects in your scene. This results in more logical and visually coherent groupings.

2. **Material Awareness**: The tool respects material boundaries, ensuring that only objects with the same material are combined. This preserves the visual integrity of your scene while still optimizing performance.

3. **Flexible Grouping Sizes**: With the proximity-based algorithm, you can easily adjust the grouping radius to suit different areas of your scene. Dense areas can have smaller radii, while sparse areas can use larger radii.

4. **Controlled Cluster Count**: The K-means algorithm allows you to specify exactly how many clusters you want, giving you precise control over the level of optimization.

5. **No Arbitrary Boundaries**: Cell-based methods can create arbitrary splits at cell boundaries, potentially separating objects that should logically be grouped. Our algorithms avoid this issue by considering the actual spatial relationships between objects.

6. **Hierarchical Subgrouping**: The tool can create subgroups within larger clusters, allowing for more nuanced optimization that respects both large-scale and small-scale object relationships.

7. **Visual Feedback**: The gizmo visualization allows you to see and fine-tune your groupings before committing to changes, something not typically available with cell-based methods.

## Tips

- Experiment with both clustering algorithms to see which works best for your specific scene layout.
- Use the gizmo visualization options to fine-tune your clustering results before combining.
- For scenes with varied object density, the Proximity-Based algorithm might yield more natural groupings.
- If you have a specific number of groups in mind, the K-Means algorithm allows you to set this directly.
- Always make a backup of your scene before performing large-scale mesh combining operations.
- For large scenes, consider combining meshes in sections rather than all at once.
- Pay attention to the material-specific colors in main groups to ensure objects are being grouped as expected.

## Limitations

- Objects with different materials cannot be combined into a single mesh.
- Skinned meshes are not supported for combination.
- Particle systems and other non-mesh renderers are ignored.

## Contributing

Contributions to improve IntelligentMeshCombiner are welcome. Please feel free to submit pull requests or create issues for bugs and feature requests. When contributing, please:

- Clearly describe the problem you're solving or the feature you're adding.
- Include steps to reproduce for bug reports.
- Include or update tests and documentation for new or changed functionality.
- Follow the existing code style and structure.




