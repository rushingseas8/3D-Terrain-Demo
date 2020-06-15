using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Profiling;

/// <summary>
/// A quick and dirty live viewer for the generator. 
/// TODO: make the generation fast enough that this tool is more useful.
/// - Perlin noise can be replaced with a faster (C native) implementation, 
///     possibly Simplex noise. Investigate if 2D or 3D is needed (or both).
/// - Marching cubes can be made into a coroutine, and/or done via multithreading.
/// - Additionally, MC can be optimized by computing a plane at a time (vertex reuse),
///     and avoiding GC work as much as possible.
/// - Data parsing (Generator#Generate2D) can similarly be multithreaded.
/// - "Mesh assigning", specifically colliders; normals; uvs; and copying channels
///     must be on the main thread. Coroutines don't quite work, since they're 
///     blocking and will try to run at once. Look into a work queue system for
///     async doing Unity mesh related work, possibly with different states?
///     E.g., colliders could be approximated with a box at the boundaries, and
///     deferred until player or other interactables are within the boundary.
///     This could also be fully disabled for the view-only rendering we're doing.
/// 
/// </summary>
[ExecuteInEditMode]
public class EditorGenerator : MonoBehaviour
{
    private Generator generator;
    private float counter = 0;

    [Range(0.05f, 2f)]
    [Tooltip("How long to wait between auto-updates")]
    public float Delay = 0.1f;

    [Tooltip("Should the simulation auto-update?")]
    public bool ShouldAutoUpdate = true;

    [Tooltip("The simulated player's position. The world will be rendered around this point.")]
    public Vector3Int SimulationPosition = new Vector3Int(0, 0, 0);

    private float[,,][] dataStorage;

    private void Init()
    {
        // Initialize our cached data storage
        Debug.Log("Initializing cached storage");
        int numPoints = (int)(Generator.Size * Generator.Precision);
        int sp1 = numPoints + 1;
        dataStorage = new float[Generator.RenderDiameter, Generator.RenderDiameter, Generator.RenderDiameter][];
        for (int i = 0; i < Generator.RenderDiameter; i++)
        {
            for (int j = 0; j < Generator.RenderDiameter; j++)
            {
                for (int k = 0; k < Generator.RenderDiameter; k++)
                {
                    dataStorage[i, j, k] = new float[sp1 * sp1 * sp1];
                }
            }
        }
    }


    // Update is called once per frame
    [ExecuteInEditMode]
    void Update()
    {
        // Don't update
        if (!ShouldAutoUpdate) 
        {
            return;
        }

        if (dataStorage == null)
        {
            Init();
        }

        // Update the counter
        counter += Time.deltaTime;

        // Initialize the Generator if need be
        if (Generator.Instance == null)
        {
            generator = GameObject.FindObjectOfType<Generator>();
            if (generator != null)
            {
                generator.Awake();
            }
            else
            {
                Debug.Log("Failed to find Generator object");
            }
        }
        else
        {
            if (counter > Delay)
            {
                counter -= Delay;
                Debug.Log("Deleting!");
                foreach (Chunk chunk in Generator.Chunks)
                {
                    if (chunk != null)
                    {
                        GameObject.DestroyImmediate(chunk.obj);
                    }
                }
                Generator.Chunks.Clear();

                Debug.Log("Updating!");
                //Generator.Generate(Generator.Generate2D);

                Profiler.BeginSample("Generate");
                for (int i = -Generator.RenderRadius; i <= Generator.RenderRadius; i++)
                {
                    for (int j = -Generator.RenderRadius; j <= Generator.RenderRadius; j++)
                    {
                        for (int k = -Generator.RenderRadius; k <= Generator.RenderRadius; k++)
                        {
                            //GameObject newObj = generateObj (new Vector3 (i, j, k), generator);
                            //Chunk newChunk = Generator.GenerateChunk(new Vector3(i, j, k), Generator.Generate2D);
                            //Chunk newChunk = Generator.GenerateChunk(SimulationPosition + new Vector3(i, j, k), Generator.Generate2D);

                            Vector3 position = SimulationPosition + new Vector3(i, j, k);

                            Profiler.BeginSample("Vertex generation");
                            //float[] data = Generator.Generate2D(position);
                            Generator.Generate2D(position, ref dataStorage[i + Generator.RenderRadius, j + Generator.RenderRadius, k + Generator.RenderRadius], out bool isEmpty);
                            Profiler.EndSample();

                            Chunk newChunk = new Chunk(position, 
                                                       Generator.generateObj(position, 
                                                                             dataStorage[i + Generator.RenderRadius, j + Generator.RenderRadius, k + Generator.RenderRadius]),
                                                       dataStorage[i + Generator.RenderRadius, j + Generator.RenderRadius, k + Generator.RenderRadius]);
                            if (!isEmpty)
                            {
                                //newChunk.obj.transform.position = new Vector3(i, j, k);
                                newChunk.obj.transform.position = new Vector3(
                                    k * Generator.Size, j * Generator.Size, i * Generator.Size) - Generator.MeshOffset;
                            }

                            Generator.Chunks[k + Generator.RenderRadius, j + Generator.RenderRadius, i + Generator.RenderRadius] = newChunk;
                            Generator.ChunkCache[new Vector3Int(i, j, k)] = newChunk;
                        }
                    }
                }
                Profiler.EndSample();
            }

        }
    }
}
