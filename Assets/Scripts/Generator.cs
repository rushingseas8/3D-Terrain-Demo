using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

// DLL support
using System.Runtime.InteropServices;

using LibNoise.Generator;
using MarchingCubesProject;

/// <summary>
/// The Generator class handles initializing and creating terrain (or caves).
/// </summary>
public class Generator : MonoBehaviour {

    #region Public static getters/setters
    // Main Instance
    public static Generator Instance 
    {
        get; private set;
    }

    // On awake, initialize the Generator Instance
    public void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // Initialize some derived variables
            renderDiameter = (renderRadius * 2) + 1;
            meshOffset = new Vector3(size / 2, size / 2, size / 2);

            // Set up the default material
            if (GameManager.twoDMode)
            {
                defaultMaterial = new Material(Resources.Load("Materials/Grass") as Material);
            }
            else
            {
                defaultMaterial = new Material(Resources.Load("Materials/Rock") as Material);
            }

            // Physics for default material
            defaultPhysics = new PhysicMaterial();
            defaultPhysics.bounciness = 0.0f;
            defaultPhysics.dynamicFriction = 1.0f;
            defaultPhysics.staticFriction = 1.0f;

            // Initialize the chunk cache
            chunkCache = new Dictionary<Vector3Int, Chunk>();

            // Init the CubeBuffer
            chunks = new CubeBuffer<Chunk>(renderDiameter);
            for (int i = 0; i < renderDiameter * renderDiameter * renderDiameter; i++)
            {
                chunks[i] = null;
            }

            // Set the fog strength for the underground scene.
            // Found by experimental values and fitting a curve to them
            fogStrength = Mathf.Exp(-0.055f * (size * renderRadius + 16));

            // Queue for async assigning mesh colliders to GameObjects
            meshColliderAssigningQueue = new Queue<(GameObject, Mesh)>();
        }
        else
        {
            Debug.Log("Extra Generator in scene on \"" + gameObject.name + "\"");
#if UNITY_EDITOR
            UnityEditor.EditorGUIUtility.PingObject(gameObject);
#endif
        }
    }

    private void Update() 
    {
        // If there's a mesh collider we need to assign, then we should do one!
        if (Instance.meshColliderAssigningQueue.Count > 0)
        {
            Profiler.BeginSample("Async mesh collider assignment");
            (GameObject unfinishedObj, Mesh mesh) = meshColliderAssigningQueue.Dequeue();
            unfinishedObj.GetComponent<MeshCollider>().sharedMesh = mesh;
            Profiler.EndSample();
        }
    }

    /// <summary>
    /// How large is each mesh, in sample points/vertices?
    /// </summary>
    public static int Size
    {
        get => Instance.size; set => Instance.size = value;
    }

    /// <summary>
    /// The scale multiplier on the perlin noise. Larger = more zoomed in, so less detailed features.
    /// Note that this shouldn't ever be 1.0 because of gradient noise being 0 at integer boundaries.
    /// If you want a scale of 1.0, try using 1.1 instead.
    /// </summary>
    public static float Scale 
    {
        get => Instance.scale; set => Instance.scale = value;
    }

    /// <summary>
    /// The scale multiplier on height. Default 1.0f makes generated height 0-1 units.
    /// </summary>
    public static float HeightScale
    {
        get => Instance.heightScale; set => Instance.heightScale = value;
    }

    /// <summary>
    /// The number of vertices per unit length. 1.0 is default; 2.0 means you get a point every 0.5 units.
    /// </summary>
    public static float Precision
    {
        get => Instance.precision; set => Instance.precision = value;
    }

    /// <summary>
    /// The value used for the isosurface for marching cubes.
    /// </summary>
    public static float MarchingSurface
    {
        get => Instance.marchingSurface; set => Instance.marchingSurface = value;
    }

    // Enum for SmoothValue field
    public enum Smoothness {
        BLOCKY,
        SMOOTH,
        SMOOTHER
    }

    /// <summary>
    /// How smooth should the terrain be rendered?
    /// BLOCKY = only 0 and 1 values are used to determine terrain
    /// SMOOTH = points are smoothly interpolated along the y axis
    /// SMOOTHER = like SMOOTH, but nearby vertices are joined. Not implemented.
    /// </summary>
    // TODO add an even smoother mode, where vertices within a certain threshold are merged to lie on grid points.
    public static Smoothness SmoothValue
    {
        get => Instance.smoothValue; set => Instance.smoothValue = value;
    }

    /// <summary>
    /// The render radius is how many chunks we process around the player. 0 = just load the chunk
    /// the player is on; 1 = 1 chunk on every side; etc.
    /// </summary>
    public static int RenderRadius
    {
        get => Instance.renderRadius; set => Instance.renderRadius = value;
    }

    /// <summary>
    /// Diameter is a calculated value with the actual number of chunks rendered at a time, 
    /// in any one axis direction. Equal to (2 * RenderRadius) + 1.
    /// </summary>
    public static int RenderDiameter => (2 * Instance.renderRadius) + 1;

    /// <summary>
    /// Gets the fog strength.
    /// Fog strength depends on size and renderRadius, and the exact value was
    /// determined based on fitting a curve to good-looking values.
    /// </summary>
    public static float FogStrength => Instance.fogStrength;

    /// <summary>
    /// By how much should each mesh be offset by default? By default this is size/2 in every axis,
    /// so that the tiles are all centered on the player.
    /// </summary>
    public static Vector3 MeshOffset => Instance.meshOffset;

    public static bool UseDualMarchingCubes 
    {
        get => Instance.useDualMarchingCubes; set => Instance.useDualMarchingCubes = value;
    }

    /// <summary>
    /// A 3D array of GameObjects representing the currently loaded terrain meshes.
    /// This gets shifted around and regenerated based on the player movement.
    /// </summary>
    public static CubeBuffer<Chunk> Chunks => Instance.chunks;

    /// <summary>
    /// A dictionary of cave meshes, sorted by their positions. Used to store previously
    /// loaded chunks.
    /// </summary>
    public static Dictionary<Vector3Int, Chunk> ChunkCache => Instance.chunkCache;

    #endregion

    #region Instance variables

    [SerializeField]
    [Range(1, 32)]
    private int size = 16;

    [SerializeField]
    [Range(0.001f, 256f)]
    private float scale = 128f;

    [SerializeField]
    [Range(0.001f, 256f)]
    private float heightScale = 32.0f;

    [SerializeField]
    [Range(0.001f, 10f)]
    private float precision = 1.0f;

    [SerializeField]
    [Range(-1f, 1f)]
    private float marchingSurface = 0.5f;

    [SerializeField]
    private Smoothness smoothValue = Smoothness.BLOCKY;

    [SerializeField]
    [Range(1, 20)]
    private int renderRadius = 5;
    private int renderDiameter;

    [SerializeField]
    private bool useDualMarchingCubes = false;

    private float fogStrength;

    private Vector3 meshOffset;

    private CubeBuffer<Chunk> chunks;

    private Dictionary<Vector3Int, Chunk> chunkCache;

    // To help the garbage collector, we provide a default size for the vertex array.
    // Too small means resizing (slow!) and too big means a lot to clean up (slow!)
    //protected const int DEFAULT_VERTEX_BUFFER_SIZE = 1800;    // Minimum found to be 1700; adding some room for error.
    //protected const int DEFAULT_TRI_BUFFER_SIZE = 1750;       // Minimum 1650.

    private const int DEFAULT_VERTEX_BUFFER_SIZE = 150;
    private const int DEFAULT_TRI_BUFFER_SIZE = 150;

    /// <summary>
    /// An offset for the terrain gen. Allows for consistent generation for debugging.
    /// </summary>
    [SerializeField]
    private Vector3 GEN_OFFSET = new Vector3 (1023, 1942, 7777);

    /// <summary>
    /// The mesh collider assigning queue.
    /// When we're creating a Mesh asynchronously, one of the biggest blocking operations
    /// is assigning the mesh collider. So we push the work onto a queue and do at most
    /// one per frame.
    /// </summary>
    private Queue<(GameObject, Mesh)> meshColliderAssigningQueue;

    #endregion

    #region Data used for controlling all Generators

    // A diffuse default material we assign to meshes.
    public Material defaultMaterial;

    // A default physics material that is somewhat sticky for testing.
    public PhysicMaterial defaultPhysics;

    // Defining a delegate for the class of data generation functions
    //public delegate float[] DataGenerator(Vector3 position);
    public delegate float[] DataGenerator(Vector3 position);

    // The perlin noise generator for the surface terrain.
    [SerializeField]
    public PerlinGenerator noiseGenerator = new PerlinGenerator(0.0f, 0.0f, 8, 1.0f, 2.0f, 0.5f);

    #endregion

    /**
     * Creates an empty shell GameObject, ready to be passed into "assignMesh".
     * This shell needs its position and scale assigned later. Used internally by Generator.
     */
    public static GameObject generateEmpty() {
        Profiler.BeginSample ("Generate Empty");
        GameObject newObj = new GameObject ();

        newObj.AddComponent<MeshFilter> ();
        newObj.AddComponent<MeshRenderer> ();
        newObj.AddComponent<MeshCollider> ();

        newObj.GetComponent<MeshRenderer> ().material = Instance.defaultMaterial;
        Profiler.EndSample ();
        return newObj;
    }

    /**
     * Creates a new mesh and assigns it to the empty gameobject provided.
     * Return immediately if "unfinishedObj" is destroyed before this method can finish,
     * which can happen if we do this asynchronously. Used internally by Generator.
     */
    private static void assignMesh(GameObject unfinishedObj, Vector3[] vertices, int[] triangles, Vector2[] uvs=null, Vector3[] normals=null) {
        if (unfinishedObj == null) { return; }

        Mesh mesh = new Mesh ();
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        Profiler.BeginSample ("UV assigning");
        if (uvs == null) {
            uvs = new Vector2[vertices.Length];
            for (int i = 0; i < uvs.Length; i += 3) {
                uvs [i + 0] = new Vector2(0, 0);
                uvs [i + 1] = new Vector2(1, 0);
                uvs [i + 2] = new Vector2(1, 1);
            }
        }
        mesh.uv = uvs;
        Profiler.EndSample ();
    
        Profiler.BeginSample ("Normal calculation");
        if (normals == null) {
            mesh.RecalculateNormals ();
        } else {
            mesh.normals = normals;
        }
        Profiler.EndSample ();

        Profiler.BeginSample ("Mesh Filter assigning");
        if (unfinishedObj == null) { return; }
        unfinishedObj.GetComponent<MeshFilter> ().sharedMesh = mesh;
        Profiler.EndSample ();

        Profiler.BeginSample ("Mesh Renderer assigning");
        if (unfinishedObj == null) { return; }
        unfinishedObj.GetComponent<MeshRenderer> ().material = Instance.defaultMaterial;
        Profiler.EndSample ();

        Profiler.BeginSample ("Mesh Collider assigning");
        if (unfinishedObj == null) { return; }
        unfinishedObj.GetComponent<MeshCollider>().sharedMesh = mesh; 
        //Instance.meshColliderAssigningQueue.Enqueue((unfinishedObj, mesh));
        Profiler.EndSample ();
    }

    private static void assignMeshAsync(GameObject unfinishedObj, Vector3[] vertices, int[] triangles, Vector2[] uvs = null, Vector3[] normals = null) 
    {
        if (unfinishedObj == null) { return; }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        Profiler.BeginSample("UV assigning");
        if (uvs == null)
        {
            uvs = new Vector2[vertices.Length];
            for (int i = 0; i < uvs.Length; i += 3)
            {
                uvs[i + 0] = new Vector2(0, 0);
                uvs[i + 1] = new Vector2(1, 0);
                uvs[i + 2] = new Vector2(1, 1);
            }
        }
        mesh.uv = uvs;
        Profiler.EndSample();

        Profiler.BeginSample("Normal calculation");
        if (normals == null)
        {
            mesh.RecalculateNormals();
        }
        else
        {
            mesh.normals = normals;
        }
        Profiler.EndSample();

        Profiler.BeginSample("Mesh Filter assigning");
        if (unfinishedObj == null) { return; }
        unfinishedObj.GetComponent<MeshFilter>().sharedMesh = mesh;
        Profiler.EndSample();

        Profiler.BeginSample("Mesh Renderer assigning");
        if (unfinishedObj == null) { return; }
        unfinishedObj.GetComponent<MeshRenderer>().material = Instance.defaultMaterial;
        Profiler.EndSample();

        Profiler.BeginSample("Mesh Collider assignment deferral");
        if (unfinishedObj == null) { return; }
        //unfinishedObj.GetComponent<MeshCollider>().sharedMesh = mesh; 
        Instance.meshColliderAssigningQueue.Enqueue((unfinishedObj, mesh));
        Profiler.EndSample();
    }

    private static List<Vector3> MARCHING_CUBES_VERTS = new List<Vector3>(25000);
    private static List<int> MARCHING_CUBES_TRIS = new List<int>(25000);

    public static int VERT_SUM = 0;
    public static int TRI_SUM = 0;

    /**
     * Generates a GameObject given a position in world coordinates, and an array with 3D
     * terrain data. Used internally by Generator. If you want to create chunks, you most
     * likely want the function "generateChunk".
     */
    public static GameObject generateObj(Vector3 position, float[] data) 
    {
        Profiler.BeginSample("GenerateObj");
        GameObject toReturn = null;
        if (data != null)
        {
            Profiler.BeginSample("Nonempty data");
            Profiler.BeginSample("GameObject generation");
            GameObject newObj = generateEmpty();
            newObj.transform.position = new Vector3(position.z * Instance.size, position.y * Instance.size, position.x * Instance.size) - Instance.meshOffset;
            newObj.transform.localScale = new Vector3(1.0f / Instance.precision, 1.0f / Instance.precision, 1.0f / Instance.precision);
            newObj.name = "(" + position.x + " ," + position.y + " ," + position.z + ")";
            Profiler.EndSample();

            Profiler.BeginSample("Marching cubes");
            //List<Vector3> verts = new List<Vector3> (DEFAULT_VERTEX_BUFFER_SIZE); 
            //List<int> tris = new List<int> (DEFAULT_TRI_BUFFER_SIZE);
            MARCHING_CUBES_VERTS.Clear();
            MARCHING_CUBES_VERTS.Capacity = 25000;
            MARCHING_CUBES_TRIS.Clear();
            MARCHING_CUBES_TRIS.Capacity = 25000;

            if (Instance.useDualMarchingCubes) 
            {
                // Note: Dual Marching Cubes needs an extra "skirt" of 2 points on the edge to
                // properly tile with neighbors. Because each sample cube is essentially treated
                // as a point in DMC, with normal MC run on top, you can think of this as the same
                // actual number of extra points as normal MC.
                DualMarching marching = new DualMarching();
                marching.Generate(data,
                    (int)(Instance.size * Instance.precision) + 2,
                    (int)(Instance.size * Instance.precision) + 2,
                    (int)(Instance.size * Instance.precision) + 2,
                    MARCHING_CUBES_VERTS, MARCHING_CUBES_TRIS);

                VERT_SUM += MARCHING_CUBES_VERTS.Count;
                TRI_SUM += MARCHING_CUBES_TRIS.Count;
            } else
            {
                OptimizedMarching marching = new OptimizedMarching ();
                //Marching marching = new MarchingCubes();
                marching.Surface = Instance.marchingSurface;

                marching.Generate(data, 
                    (int)(Instance.size * Instance.precision) + 1, 
                    (int)(Instance.size * Instance.precision) + 1, 
                    (int)(Instance.size * Instance.precision) + 1,
                MARCHING_CUBES_VERTS, MARCHING_CUBES_TRIS);

                VERT_SUM += MARCHING_CUBES_VERTS.Count;
                TRI_SUM += MARCHING_CUBES_TRIS.Count;
            }

            //Debug.Log($"Generated {MARCHING_CUBES_VERTS.Count} vertices and {MARCHING_CUBES_TRIS.Count} tris.");

            Profiler.EndSample ();  

            Profiler.BeginSample ("Mesh assigning");
            assignMesh (newObj, MARCHING_CUBES_VERTS.ToArray (), MARCHING_CUBES_TRIS.ToArray ());
            Profiler.EndSample ();

            toReturn = newObj;
            Profiler.EndSample();
        }
        Profiler.EndSample();
        return toReturn;
    }
        

    public static float[] Generate2D(Vector3 position)
    {
        int numPoints = (int)(Instance.size * Instance.precision);
        int sp1 = Instance.useDualMarchingCubes ? numPoints + 2 : numPoints + 1;
        float[] data = new float[sp1 * sp1 * sp1];
        Generate2D(position, ref data, out bool isEmpty);
        //Generate2D_XZY(position, ref data, out bool isEmpty);

        return isEmpty ? null : data;
    }

    /**
     * Generates flat terrain on the surface level.
     * TODO: Rewrite manual normal calculation using actual noise data; this is a bit ugly looking
     * 
     * Maybe it's also a small error in the difference offsetting, but I'm honestly tired 
     * of that code by now; see around "float difference = ...".
     */
    public static void Generate2D(Vector3 position, ref float[] data, out bool isEmpty) {
        int numPoints = (int)(Instance.size * Instance.precision);

        // We generate an extra vertex on each end to allow for seamless transitions.
        int sp1 = Instance.useDualMarchingCubes ? numPoints + 2 : numPoints + 1;
        //float[] data = new float[sp1 * sp1 * sp1];

        // This scale value transforms "position" (in integer chunk coords) to actual
        // world coords, using "size" (# points per mesh per axis) over "scale" (perlin offset).
        // When size == scale, offsetScale == 1, so world coords == chunk coords.
        float offsetScale = numPoints / Instance.scale / Instance.precision;
        Vector3 offset = Instance.GEN_OFFSET + position * offsetScale;

        //float[] noise = new float[sp1 * sp1];
        //int count = 0;

        bool hasGround = false;
        bool hasAir = false;
        float multiplier = 1.0f / numPoints;
        float noise;
        float noiseVal;
        for (int x = 0; x < sp1; x++) {
            for (int z = 0; z < sp1; z++) {
                Profiler.BeginSample("Noise generation");
                // Noise is the actual random noise. This should take into account temp/precip/etc.
                noise = Instance.noiseGenerator.GetValue(
                    offset.x + (x / Instance.scale / Instance.precision), 
                    offset.z + (z / Instance.scale / Instance.precision));
                Profiler.EndSample();

                // Clamps the perlin noise. This may cut off some mountains.
                //noise = Mathf.Clamp (noise, 0f, 1f); 

                // Multiply by the height scale (to normalize to an actual height value), and then
                // divide by the number of points used. Because the sample points essentially cover a
                // 1x1x1 unit cube, this division normalizes the noise value into the sample points' space.
                // Finally, subtract the world chunk position, so that the noise is in [0, 1] iff we are
                // looking at the right chunk.
                noiseVal = (Instance.heightScale * noise / Instance.size) - position.y;

                Profiler.BeginSample("Y axis computation");
                // TODO store "data" in [x, z, y] format to help cache coherence?
                for (int y = 0; y < sp1; y++) {
                    if (SmoothValue == Smoothness.BLOCKY)
                    {
                        if (y * multiplier < noiseVal)
                        {
                            data[(x * sp1 * sp1) + (y * sp1) + z] = 1;
                            hasGround = true;
                        }
                        else
                        {
                            hasAir = true;
                        }
                    }
                    else
                    {

                        //data[(x * sp1 * sp1) + (y * sp1) + z] = noiseVal;

                        // Check if the current sample point is below the surface.
                        if (y * multiplier < noiseVal)
                        {
                            data[(x * sp1 * sp1) + (y * sp1) + z] = 1;
                            hasGround = true;
                        }
                        else
                        {
                            hasAir = true;
                            // If it isn't, this sample point is above the noise value surface.
                            // We do an additional check on the point below us; if another point
                            // below us is also above the surface, then this point needs to do nothing.
                            // This can happen when y == 0.
                            if ((y - 1) * multiplier > noiseVal)
                            {
                                break;
                            }

                            // The height difference between the noise and the next lowest sample point interval.
                            // If e.g. there are 8 sample points, this will look at the next lowest 1/8 and
                            // take the difference. Then, normalize to the range [0, 1].
                            float difference = (noiseVal - ((y - 1) * multiplier)) / multiplier;

                            // Upper point lerps from 0 to 0.5; lower point from 0.5 to 1.0
                            data[(x * sp1 * sp1) + (y * sp1) + z] = Mathf.Lerp(0f, 0.5f, difference);
                            if (y > 0)
                            {
                                data[(x * sp1 * sp1) + ((y - 1) * sp1) + z] = Mathf.Lerp(0.5f, 1f, difference);
                            }
                            break;
                        }
                    }
                    // TODO add case for SmoothValue.SMOOTHER
                }
                Profiler.EndSample();

                Profiler.BeginSample("Y=0 computation");
                // Special case: When y == 0 and it is above the noise surface, we need to resolve it.
                // We do this by checking in the mesh below it, for the contrapositive condition.
                // This code eliminates vertical seams.
                if (numPoints * multiplier < noiseVal && sp1 * multiplier > noiseVal) {
                    if (SmoothValue == Smoothness.BLOCKY)
                    {
                        data[(x * sp1 * sp1) + (numPoints * sp1) + z] = 1.0f;
                    }
                    else
                    {
                        float difference = (noiseVal - (numPoints * multiplier)) / multiplier;
                        data[(x * sp1 * sp1) + (numPoints * sp1) + z] = Mathf.Lerp(0.5f, 1f, difference);
                    }
                }
                // TODO add case for SmoothValue.SMOOTHER
                Profiler.EndSample();
            }
        }

        // If all our points are above the surface, or all of them are below the surface,
        // then we won't need to render a mesh. Thus, we return null for the data.
        isEmpty = hasAir ^ hasGround;
    }
        

    /**
     * Generates flat terrain on the surface level.
     * Does so asynchronously to minimize loading times.
     */
    public static IEnumerator Generate2DAsync(Vector3 position, GameObject unfinishedObj)
    {
        #region Assign position data
        unfinishedObj.transform.position = new Vector3(position.z * Instance.size, position.y * Instance.size, position.x * Instance.size) - Instance.meshOffset;
        unfinishedObj.transform.localScale = new Vector3(1.0f / Instance.precision, 1.0f / Instance.precision, 1.0f / Instance.precision);
        unfinishedObj.name = "(" + position.x + " ," + position.y + " ," + position.z + ")";
        #endregion

        #region Generate data
        // Detailed comments are in the Generate2D method.
        int numPoints = (int)(Instance.size * Instance.precision);

        int sp1 = Instance.useDualMarchingCubes ? numPoints + 2 : numPoints + 1;
        float[] data = new float[sp1 * sp1 * sp1]; // TODO avoid re-creating this to avoid GC

        float offsetScale = numPoints / Instance.scale / Instance.precision;
        Vector3 offset = Instance.GEN_OFFSET + position * offsetScale;

        bool hasNonzero = false;
        float multiplier = 1.0f / numPoints;
        float noise;
        float noiseVal;
        for (int x = 0; x < sp1; x++) {
            Profiler.BeginSample("Async data generation");
            for (int z = 0; z < sp1; z++) {
                noise = Instance.noiseGenerator.GetValue(offset.x + (x / Instance.scale / Instance.precision), offset.z + (z / Instance.scale / Instance.precision));
                noiseVal = (Instance.heightScale * noise / Instance.size) - position.y;

                for (int y = 0; y < sp1; y++)
                {
                    if (SmoothValue == Smoothness.BLOCKY)
                    {
                        if (y * multiplier < noiseVal)
                        {
                            data[(x * sp1 * sp1) + (y * sp1) + z] = 1;
                            hasNonzero = true;
                        }
                    }
                    else
                    {
                        if (y * multiplier < noiseVal)
                        {
                            data[(x * sp1 * sp1) + (y * sp1) + z] = 1;
                            hasNonzero = true;
                        }
                        else
                        {
                            if ((y - 1) * multiplier > noiseVal)
                            {
                                break;
                            }

                            float difference = (noiseVal - ((y - 1) * multiplier)) / multiplier;

                            // Upper point lerps from 0 to 0.5; lower point from 0.5 to 1.0
                            data[(x * sp1 * sp1) + (y * sp1) + z] = Mathf.Lerp(0f, 0.5f, difference);
                            if (y > 0)
                            {
                                data[(x * sp1 * sp1) + ((y - 1) * sp1) + z] = Mathf.Lerp(0.5f, 1f, difference);
                            }
                            break;
                        }
                    }
                }

                if (numPoints * multiplier < noiseVal && sp1 * multiplier > noiseVal)
                {
                    if (SmoothValue == Smoothness.BLOCKY)
                    {
                        data[(x * sp1 * sp1) + (numPoints * sp1) + z] = 1.0f;
                    }
                    else
                    {
                        float difference = (noiseVal - (numPoints * multiplier)) / multiplier;
                        data[(x * sp1 * sp1) + (numPoints * sp1) + z] = Mathf.Lerp(0.5f, 1f, difference);
                    }
                }
            }
            Profiler.EndSample();
            yield return null; 
        }
        #endregion

        if (hasNonzero) {
            //TODO: Make an async version of marching cubes
            #region Perform Marching Cubes

            // TODO reuse these lists
            List<Vector3> verts = new List<Vector3> (DEFAULT_VERTEX_BUFFER_SIZE); 
            List<int> tris = new List<int> (DEFAULT_TRI_BUFFER_SIZE);

            //Marching marching = new MarchingCubes ();
            // TODO cache this object
            OptimizedMarching marching = new OptimizedMarching();
            marching.Surface = Instance.marchingSurface;

            //marching.Generate(data, sp1, sp1, sp1, verts, tris);
            yield return Instance.StartCoroutine(marching.GenerateAsync(data, sp1, sp1, sp1, verts, tris));

            #endregion

            Profiler.BeginSample("Assigning mesh");
            assignMeshAsync(unfinishedObj, verts.ToArray (), tris.ToArray ());
            Profiler.EndSample();
            yield return null;
        }
    }
        

    /**
     * Custom C code for 3D Perlin noise, used in cave generation.
     */
    [DllImport ("FastPerlin")]
    private static extern double GetValue (double x, double y, double z);

    private static RidgedMultifractal noiseGen;

    /**
     * Generates 3D cave systems.
     */
    public static float[] GenerateCave(Vector3 position) {
        int numPoints = (int)(Instance.size * Instance.precision);

        // We generate an extra vertex on each end to allow for seamless transitions.
        int sp1 = Instance.useDualMarchingCubes ? numPoints + 2 : numPoints + 1;
        float[] data = new float[sp1 * sp1 * sp1];

        // This scale value transforms "position" (in integer chunk coords) to actual
        // world coords, using "size" (# points per mesh per axis) over "scale" (perlin offset).
        // When size == scale, offsetScale == 1, so world coords == chunk coords.
        float offsetScale = numPoints / Instance.scale / Instance.precision;
        Vector3 offset = Instance.GEN_OFFSET + position * offsetScale;

        noiseGen = new RidgedMultifractal (); 
        noiseGen.Lacunarity = 1.0f;   
        noiseGen.Frequency = 2.0f;

        // We negate the value because the inverse looks better for RidgedMultifractal. 
        // Switch to positive for Perlin noise.
        int count = 0;
        for (int i = 0; i < sp1; i++) {
            for (int j = 0; j < sp1; j++) {
                for (int k = 0; k < sp1; k++) {
                    //data [count++] = (float) -GetValue(offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));
                    data [count++] = (float) noiseGen.GetValue(offset.x + (i / Instance.scale), offset.y + (j / Instance.scale), offset.z + (k / Instance.scale));
                }
            }
        }

        return data;
    }

    /**
     * Generates 3D cave systems, asyncronously.
     */
    public static IEnumerator GenerateCaveAsync(Vector3 position, GameObject unfinishedObj) {
        if (unfinishedObj != null) {
            unfinishedObj.transform.position = new Vector3 (position.z * Instance.size, position.y * Instance.size, position.x * Instance.size) - Instance.meshOffset;
        }
        yield return null;

        #region Create data

        int sp1 = Instance.size + 1;
        float[] data = new float[sp1 * sp1 * sp1];
        float offsetScale = Instance.size / Instance.scale;
        Vector3 offset = Instance.GEN_OFFSET + position * offsetScale;

        // The yield return is placed there to best optimize runtime.
        int count = 0;
        for (int i = 0; i < sp1; i++) {
            for (int j = 0; j < sp1; j++) {
                for (int k = 0; k < sp1; k++) {
                    //data [count++] = (float) -GetValue(
                        //offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));

                    data[count++] = (float)-noiseGen.GetValue(offset.x + (i / Instance.scale), offset.y + (j / Instance.scale), offset.z + (k / Instance.scale));
                }
            }
            yield return null;
        }

        #endregion

        #region Perform Marching Cubes

        List<Vector3> verts = new List<Vector3> (DEFAULT_VERTEX_BUFFER_SIZE); 
        List<int> tris = new List<int> (DEFAULT_TRI_BUFFER_SIZE);

        Marching marching = new MarchingCubes ();
        marching.Surface = -0.8f;

        marching.Generate(data, sp1, sp1, sp1, verts, tris);
        yield return null;

        #endregion

        assignMesh (unfinishedObj, verts.ToArray (), tris.ToArray ());
        yield return null;
    }

    /**
     * Generates a new Chunk object at the provided position, using the provided function
     * for data generation. Specifically, using Generate2D will create surface terrain,
     * and GenerateCave will generate 3D underground caves.
     */
    public static Chunk GenerateChunk(Vector3 position, DataGenerator generate) {
        Profiler.BeginSample("Vertex generation");
        float[] data = generate (position);
        Profiler.EndSample ();

        return new Chunk (position, generateObj (position, data), data);
    }

    /**
     * Generates an initial region around the player. Called on game start.
     */
    public static void Generate(DataGenerator generator) {

        Profiler.BeginSample ("Generate");
        for (int i = -Instance.renderRadius; i <= Instance.renderRadius; i++) {
            for (int j = -Instance.renderRadius; j <= Instance.renderRadius; j++) {
                for (int k = -Instance.renderRadius; k <= Instance.renderRadius; k++) {
                    //GameObject newObj = generateObj (new Vector3 (i, j, k), generator);
                    Chunk newChunk = GenerateChunk(new Vector3(i, j, k), generator);

                    Instance.chunks [k + Instance.renderRadius, j + Instance.renderRadius, i + Instance.renderRadius] = newChunk;
                    Instance.chunkCache [new Vector3Int (i, j, k)] = newChunk;
                }
            }
        }
        Profiler.EndSample ();
    }
}
