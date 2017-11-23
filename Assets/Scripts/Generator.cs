using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

// DLL support
using System.Runtime.InteropServices;

using LibNoise.Generator;
using MarchingCubesProject;

public class Generator : MonoBehaviour {

	// How large is each mesh, in points?
	public static int size = 8;

	// The scale multiplier on the noise we use. Larger values = larger terrain, but less detail.
	// Note that this shouldn't ever be 1.0 because of gradient noise being 0 at integer boundaries.
	// If you want a scale of 1.0, try using 1.1 instead.
	public static float scale = 16f;

	// By how much should each mesh be offset by default? This is to center it around the player.
	private static Vector3 meshOffset;

	// The material we assign to the cave meshes.
	private static Material defaultMaterial;

	// The physics material we assign to the cave meshes.
	private static PhysicMaterial defaultPhysics;

	/*
	 * A 3D array of GameObjects representing the currently loaded cave meshes.
	 * This gets shifted around and regenerated based on the player movement.
	 */
	public static CubeBuffer chunks;

	/**
	 * A dictionary of cave meshes, sorted by their positions.
	 */
	public static Dictionary<Vector3Int, GameObject> chunkCache;

	// How many meshes should we load at once? The radius is how many meshes are drawn in every
	// direction, plus one for the center. The diameter, N, is how large of an NxNxN cube is 
	// centered around the player.
	public static int renderRadius = 3;
	public static int renderDiameter = (renderRadius * 2) + 1;

	// How strong is the fog? Calculated from renderRadius and size.
	public static float fogStrength = 0.10f;

	// Custom C code for extra speed
	[DllImport ("FastPerlin")]
	private static extern double GetValue (double x, double y, double z);

	//[DllImport ("FastPerlin")]
	//private static extern void GetValue_All (double[,,] data, double x, double y, double z, double step);

	// To help the garbage collector, we provide a default size for the vertex array.
	// Too small means resizing (slow!) and too big means a lot to clean up (slow!)
	private const int DEFAULT_VERTEX_BUFFER_SIZE = 1800;	// Minimum found to be 1700; adding some room for error.
	private const int DEFAULT_TRI_BUFFER_SIZE = 1750;		// Minimum 1650.

	// An offset for the terrain gen.
	//private static Vector3 GEN_OFFSET = new Vector3 (1023, 1942, 7777);
	private static Vector3 GEN_OFFSET = Vector3.zero;

	static Generator() {
		meshOffset = new Vector3 (size / 2, size / 2, size / 2); // Centered on the player; endless caves
		//meshOffset = new Vector3 (size / 2, 0, size / 2); // Centered on the player on the x/z plane

		defaultMaterial = new Material(Resources.Load("Materials/Rock") as Material);

		defaultPhysics = new PhysicMaterial ();
		defaultPhysics.bounciness = 0.0f;
		defaultPhysics.dynamicFriction = 1.0f;
		defaultPhysics.staticFriction = 1.0f;

		chunkCache = new Dictionary<Vector3Int, GameObject> ();

		chunks = new CubeBuffer (renderDiameter);
		for (int i = 0; i < renderDiameter * renderDiameter * renderDiameter; i++) {
			chunks[i] = null;
		}

		// Found by experimental values and fitting a curve to them
		fogStrength = Mathf.Exp (-0.055f * (size * renderRadius + 16));
	}

	public static void generate() {
		// This will center the generation entirely around the player. Useful for 3D cave exploration.
		for (int i = -renderRadius; i <= renderRadius; i++) {
			for (int j = -renderRadius; j <= renderRadius; j++) {
				for (int k = -renderRadius; k <= renderRadius; k++) {
					GameObject newObj = generateObj (new Vector3 (i, j, k));

					chunks [k + renderRadius, j + renderRadius, i + renderRadius] = newObj;
					newObj.name = "(" + (i + renderRadius) + ", " + (j + renderRadius) + ", " + (k + renderRadius) + ")";
					chunkCache [new Vector3Int (i, j, k)] = newObj;
				}
			}
		}

		// Land-based generation, for underground caves
		/*
		for (int i = -renderRadius; i <= renderRadius; i++) {
			for (int j = -renderDiameter; j < 0; j++) {
				for (int k = -renderRadius; k <= renderRadius; k++) {
					GameObject newObj = generateObj (new Vector3 (i, j, k));

					chunks [k + renderRadius, j + renderDiameter, i + renderRadius] = newObj;
					//newObj.name = "(" + (i + renderRadius) + ", " + (j + renderRadius) + ", " + (k + renderRadius) + ")";
					chunkCache [new Vector3Int (i, j, k)] = newObj;
				}
			}
		}
		*/
	}

	private static float[] generateData(int size, Vector3 position) {
		// We generate an extra vertex on each end to allow for seamless transitions.
		int sp1 = size + 1;
		float[] data = new float[sp1 * sp1 * sp1];

		// This scale value transforms "position" (in integer chunk coords) to actual
		// world coords, using "size" (# points per mesh per axis) over "scale" (perlin offset).
		// When size == scale, offsetScale == 1, so world coords == chunk coords.
		float offsetScale = size / scale;
		Vector3 offset = GEN_OFFSET + position * offsetScale;

		// We negate the value because the inverse looks better for RidgedMultifractal. 
		// Switch to positive for Perlin noise.
		int count = 0;
		for (int i = 0; i < sp1; i++) {
			for (int j = 0; j < sp1; j++) {
				for (int k = 0; k < sp1; k++) {
					data [count++] = (float) -GetValue(offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));
				}
			}
		}

		return data;
	}

	private static GameObject generateObj(Vector3 position) {
		Profiler.BeginSample("Vertex generation");
		float[] data = generateData (size, position);
		Profiler.EndSample ();

		List<Vector3> verts = new List<Vector3> (DEFAULT_VERTEX_BUFFER_SIZE); 
		List<int> tris = new List<int> (DEFAULT_TRI_BUFFER_SIZE);

		Profiler.BeginSample("Marching cubes");
		Marching marching = new MarchingCubes ();
		marching.Surface = 0f;

		marching.Generate(data, size + 1, size + 1, size + 1, verts, tris);
		Profiler.EndSample ();

		Profiler.BeginSample ("Mesh generation");
		GameObject newObj = generateEmpty ();
		assignMesh (newObj, verts.ToArray (), tris.ToArray ());
		newObj.transform.position = new Vector3(position.z * size, position.y * size, position.x * size) - meshOffset;
		newObj.name = "(" + position.x + " ," + position.y + " ," + position.z + ")";
		Profiler.EndSample ();

		return newObj;
	}

	/**
	 * Return immediately if "unfinishedObj" is destroyed before this method can finish.
	 */
	private static void assignMesh(GameObject unfinishedObj, Vector3[] vertices, int[] triangles) {
		if (unfinishedObj == null) { return; }
		Mesh mesh = new Mesh ();
		mesh.vertices = vertices;
		mesh.triangles = triangles;

		Vector2[] uvs = new Vector2[vertices.Length];
		for (int i = 0; i < uvs.Length; i += 3) {
			uvs [i + 0] = new Vector2(0, 0);
			uvs [i + 1] = new Vector2(1, 0);
			uvs [i + 2] = new Vector2(1, 1);
		}
		mesh.uv = uvs;
		
		mesh.RecalculateNormals ();

		if (unfinishedObj == null) { return; }
		unfinishedObj.GetComponent<MeshFilter> ().mesh = mesh;

		if (unfinishedObj == null) { return; }
		unfinishedObj.GetComponent<MeshRenderer> ().material = defaultMaterial;

		if (unfinishedObj == null) { return; }
		unfinishedObj.GetComponent<MeshCollider>().sharedMesh = mesh; 
	}

	public static IEnumerator generateAsync(Vector3 position, GameObject unfinishedObj) {
		if (unfinishedObj != null) {
			unfinishedObj.transform.position = new Vector3 (position.z * size, position.y * size, position.x * size) - meshOffset;
		}
		yield return null;

		#region Create data

		int sp1 = size + 1;
		float[] data = new float[sp1 * sp1 * sp1];
		float offsetScale = size / scale;
		Vector3 offset = GEN_OFFSET + position * offsetScale;

		// The yield return is placed there to best optimize runtime.
		int count = 0;
		for (int i = 0; i < sp1; i++) {
			for (int j = 0; j < sp1; j++) {
				for (int k = 0; k < sp1; k++) {
					data [count++] = (float) -GetValue(
						offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));
				}
			}
			yield return null;
		}

		#endregion

		#region Perform Marching Cubes

		List<Vector3> verts = new List<Vector3> (DEFAULT_VERTEX_BUFFER_SIZE); 
		List<int> tris = new List<int> (DEFAULT_TRI_BUFFER_SIZE);

		Marching marching = new MarchingCubes ();
		marching.Surface = 0f;

		marching.Generate(data, sp1, sp1, sp1, verts, tris);
		yield return null;

		#endregion

		assignMesh (unfinishedObj, verts.ToArray (), tris.ToArray ());
		yield return null;
	}

	public static GameObject generateEmpty() {
		GameObject newObj = new GameObject ();

		newObj.AddComponent<MeshFilter> ();
		newObj.AddComponent<MeshRenderer> ();
		newObj.AddComponent<MeshCollider> ();

		newObj.GetComponent<MeshRenderer> ().material = defaultMaterial;
		return newObj;
	}

	public static void shiftArray(Direction dir) {
		chunks.shift (dir);
	}

	void Start() {
	}

	void Update() {
		
	}
}
