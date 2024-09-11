# IntelligentMeshCombiner

IntelligentMeshCombiner is a powerful Unity Editor tool designed to optimize your scenes by intelligently grouping and combining meshes based on their proximity and materials. This tool offers a sophisticated approach to reducing draw calls while preserving the visual fidelity and spatial relationships of your Unity scenes.

## Features

- Proximity-based object grouping within a specified radius
- Adaptive cluster sizes that respect scene layout
- Automatic handling of multiple materials
- Creation of subgroups for clusters exceeding a specified triangle limit
- Visualization of clusters with material-specific colors for main groups
- Saving of combined meshes as assets in an 'IMC_Meshes' folder
- Option to rebuild lightmap UVs for combined meshes
- Option to add mesh colliders to combined objects
- Marking of combined objects as static
- Option to destroy source objects after combining

## Installation

1. Clone or download this repository.
2. Copy the `IntelligentMeshCombiner.cs` file into your Unity project's `Editor` folder.
   - If you don't have an `Editor` folder, create one in your project's Assets directory.
3. The tool will appear in Unity under "Tools > IntelligentMeshCombiner" in the top menu.

## How to Use

1. Open the IntelligentMeshCombiner window by selecting "Tools > IntelligentMeshCombiner" from the Unity menu.

2. In your scene, select a parent object containing the meshes you want to group and combine.

3. In the IntelligentMeshCombiner window:
   - Assign the selected parent object to the "Parent Object" field.
   - Adjust the "Grouping Radius" to control how close objects need to be to form a group.
   - Set the "Subgroup Radius" for creating smaller clusters within large groups.
   - Adjust the "Triangle Limit" to control when subgroups are created.
   - Configure additional options like rebuilding lightmap UVs, adding mesh colliders, etc.

4. Click "Rebuild Clusters" to analyze the objects and visualize the groupings in the scene view.
   - Main groups will be displayed as orange spheres, with subtle color variations for different materials.
   - Subgroups will appear as green spheres.
   - Groups exceeding the triangle limit will be shown in red.

5. Review the cluster information and adjust settings if needed.

6. Choose an action:
   - Click "Group Objects Only" to organize objects without combining meshes.
   - Click "Group and Combine Clusters" to both group and combine meshes.

7. The tool will create new combined objects and save the combined meshes as assets in the 'IMC_Meshes' folder within your project's Assets directory.

## Intelligent Grouping Mechanism

The core strength of IntelligentMeshCombiner lies in its adaptive grouping algorithm, which offers several advantages over traditional cell-based combining methods:

1. **Proximity-Based Grouping**: 
   - Objects are grouped based on their actual proximity to each other, rather than their position within predetermined grid cells.
   - This approach preserves the logical and visual relationships between objects in your scene.

2. **Adaptive Cluster Sizes**: 
   - Unlike fixed-size cells, our grouping mechanism adapts to the natural clustering of objects in your scene.
   - Dense areas form larger groups, while sparse areas maintain smaller, more appropriate groupings.

3. **Material-Aware Combining**: 
   - The tool intelligently handles multiple materials, ensuring that only compatible objects are combined.
   - This preserves material integrity while still optimizing draw calls wherever possible.

4. **Triangle Count Management**:
   - Large clusters are automatically subdivided based on a customizable triangle count limit.
   - This prevents the creation of overly complex meshes that could impact performance or exceed engine limitations.

5. **Spatial Relationship Preservation**:
   - By grouping nearby objects, the tool maintains the spatial relationships and layout of your original scene.
   - This is particularly beneficial for large, organically structured environments.

6. **Flexible Grouping Parameters**:
   - The grouping radius and subgroup radius can be adjusted to fine-tune the grouping process for different types of scenes or specific areas.

7. **Visual Feedback**:
   - The tool provides immediate visual feedback in the scene view, allowing you to see and adjust groupings before committing to mesh combination.

8. **Hierarchical Grouping**:
   - Main groups and subgroups are created hierarchically, allowing for organized management of combined objects.

This intelligent grouping approach offers several benefits over cell-based combining:
- It's more adaptive to the actual layout of your scene.
- It avoids arbitrary splits that can occur at cell boundaries in grid-based systems.
- It provides more control over the granularity of optimization.
- It better preserves the intended structure and relationships of objects in the scene.

## Limitations

- Objects with different materials cannot be combined into a single mesh.
- Skinned meshes are not supported for combination.
- Particle systems and other non-mesh renderers are ignored.

## Tips

- Use the scene view gizmos to visualize how objects will be grouped before combining.
- Experiment with different radius and triangle limit settings to find the optimal balance between performance and visual quality.
- Always make a backup of your scene before performing large-scale mesh combining operations.
- For large scenes, consider combining meshes in sections rather than all at once.
- Pay attention to the material-specific colors in main groups to ensure objects are being grouped as expected.

## Contributing

Contributions to improve IntelligentMeshCombiner are welcome. Please feel free to submit pull requests or create issues for bugs and feature requests. When contributing, please:

- Clearly describe the problem you're solving or the feature you're adding.
- Include steps to reproduce for bug reports.
- Include or update tests and documentation for new or changed functionality.
- Follow the existing code style and structure.



