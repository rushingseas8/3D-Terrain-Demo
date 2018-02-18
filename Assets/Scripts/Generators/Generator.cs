using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

// DLL support
using System.Runtime.InteropServices;

using LibNoise.Generator;
using MarchingCubesProject;

/**
 * The main
 */
public class Generator : MonoBehaviour {

	#region Data specific to each unique Generator

	// How large is each mesh, in sample points/vertices?
	public static int size = 8;

	// The scale multiplier on the perlin noise. Larger = more zoomed in, so less detailed features.
	// Note that this shouldn't ever be 1.0 because of gradient noise being 0 at integer boundaries.
	// If you want a scale of 1.0, try using 1.1 instead.
	public static float scale = 8f; //16f;

	// The scale multiplier on height. Default 1.0f makes generated height 0-1 units.
	public static float heightScale = 8.0f;

	// The number of vertices per unit length. 1.0 is default; 2.0 means you get a point every 0.5 units.
	public static float precision = 1.0f;

	// The value used for the isosurface for marching cubes.
	protected static float marchingSurface = 0.5f;

	/*
	 * The render radius is how many chunks we process around the player. 0 = just load the chunk
	 * the player is on; 1 = 1 chunk on every side; etc. Diameter is a calculated value with the
	 * actual number of chunks rendered at a time, in any one axis direction.
	 */
	public static int renderRadius = 5;
	public static int renderDiameter = (renderRadius * 2) + 1;

	// How strong is the fog? Calculated from renderRadius and size.
	public static float fogStrength = 0.10f;

	/* 
	 * By how much should each mesh be offset by default? By default this is size/2 in every axis,
	 * so that the tiles are all centered on the player.
	 */
	protected static Vector3 meshOffset = new Vector3 (size / 2, size / 2, size / 2);

	/*
	 * A 3D array of GameObjects representing the currently loaded cave meshes.
	 * This gets shifted around and regenerated based on the player movement.
	 */
	public static CubeBuffer<Chunk> chunks;

	/**
	 * A dictionary of cave meshes, sorted by their positions. Used to store previously
	 * loaded chunks.
	 */
	public static Dictionary<Vector3Int, Chunk> chunkCache;

	// To help the garbage collector, we provide a default size for the vertex array.
	// Too small means resizing (slow!) and too big means a lot to clean up (slow!)
	//protected const int DEFAULT_VERTEX_BUFFER_SIZE = 1800;	// Minimum found to be 1700; adding some room for error.
	//protected const int DEFAULT_TRI_BUFFER_SIZE = 1750;		// Minimum 1650.

	protected const int DEFAULT_VERTEX_BUFFER_SIZE = 150;
	protected const int DEFAULT_TRI_BUFFER_SIZE = 150;

	// An offset for the terrain gen. Allows for consistent generation for debugging.
	protected static Vector3 GEN_OFFSET = new Vector3 (1023, 1942, 7777);

	#endregion

	#region Data used for controlling all Generators

	// A diffuse default material we assign to meshes.
	protected static Material defaultMaterial;

	// A default physics material that is somewhat sticky for testing.
	protected static PhysicMaterial defaultPhysics;

	// Defining a delegate for the class of data generation functions
	public delegate float[] DataGenerator(Vector3 position);

	#endregion


	private static PerlinGenerator temperatureGenerator = new PerlinGenerator (0.0f, 0.0f, 2, 1.0f, 2.0f, 0.5f);

	static Generator() {
		//defaultMaterial = new Material(Shader.Find("Diffuse"));
		defaultMaterial = new Material(Resources.Load("Materials/Grass") as Material);

		defaultPhysics = new PhysicMaterial ();
		defaultPhysics.bounciness = 0.0f;
		defaultPhysics.dynamicFriction = 1.0f;
		defaultPhysics.staticFriction = 1.0f;

		chunkCache = new Dictionary<Vector3Int, Chunk> ();

		chunks = new CubeBuffer<Chunk> (renderDiameter);
		for (int i = 0; i < renderDiameter * renderDiameter * renderDiameter; i++) {
			chunks[i] = null;
		}

		// Found by experimental values and fitting a curve to them
		fogStrength = Mathf.Exp (-0.055f * (size * renderRadius + 16));
	}

	/**
	 * Creates an empty shell GameObject, ready to be passed into "assignMesh".
	 */
	public static GameObject generateEmpty() {
		Profiler.BeginSample ("Generate Empty");
		GameObject newObj = new GameObject ();

		newObj.AddComponent<MeshFilter> ();
		newObj.AddComponent<MeshRenderer> ();
		newObj.AddComponent<MeshCollider> ();

		newObj.GetComponent<MeshRenderer> ().material = defaultMaterial;
		Profiler.EndSample ();
		return newObj;
	}

	/**
	 * Creates a new mesh and assigns it to the empty gameobject provided.
	 * Return immediately if "unfinishedObj" is destroyed before this method can finish,
	 * which can happen if we do this asynchronously.
	 */
	public static void assignMesh(GameObject unfinishedObj, Vector3[] vertices, int[] triangles, Vector2[] uvs=null, Vector3[] normals=null) {
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
		unfinishedObj.GetComponent<MeshFilter> ().mesh = mesh;
		Profiler.EndSample ();

		Profiler.BeginSample ("Mesh Renderer assigning");
		if (unfinishedObj == null) { return; }
		unfinishedObj.GetComponent<MeshRenderer> ().material = defaultMaterial;
		Profiler.EndSample ();

		Profiler.BeginSample ("Mesh Collider assigning");
		if (unfinishedObj == null) { return; }
		unfinishedObj.GetComponent<MeshCollider>().sharedMesh = mesh; 
		Profiler.EndSample ();
	}

	public static GameObject generateObj(Vector3 position, float[] data) {
		Profiler.BeginSample ("GameObject generation");
		GameObject newObj = generateEmpty ();
		newObj.transform.position = new Vector3(position.z * size, position.y * size, position.x * size) - meshOffset;
		newObj.transform.localScale = new Vector3(1.0f / precision, 1.0f / precision, 1.0f / precision);
		newObj.name = "(" + position.x + " ," + position.y + " ," + position.z + ")";
		Profiler.EndSample ();

		if (data != null) {
			Profiler.BeginSample("Marching cubes");
			List<Vector3> verts = new List<Vector3> (DEFAULT_VERTEX_BUFFER_SIZE); 
			List<int> tris = new List<int> (DEFAULT_TRI_BUFFER_SIZE);

			OptimizedMarching marching = new OptimizedMarching ();
			marching.Surface = marchingSurface;

			marching.Generate(data, (int)(size * precision) + 1, (int)(size * precision) + 1, (int)(size * precision) + 1, verts, tris);
			Profiler.EndSample ();	

			Profiler.BeginSample ("Mesh assigning");
			assignMesh (newObj, verts.ToArray (), tris.ToArray ());
			Profiler.EndSample ();
		}

		return newObj;
	}

	public static Chunk generateChunk(Vector3 position, DataGenerator generate) {
		Profiler.BeginSample("Vertex generation");
		float[] data = generate (position);
		Profiler.EndSample ();

		return new Chunk (position, generateObj (position, data), data);
	}

	/**
	 * Generates an initial region around the player. Called on game start.
	 */
	public static void generate(DataGenerator generator) {

		Profiler.BeginSample ("Generate");
		for (int i = -renderRadius; i <= renderRadius; i++) {
			for (int j = -renderRadius; j <= renderRadius; j++) {
				for (int k = -renderRadius; k <= renderRadius; k++) {
					//GameObject newObj = generateObj (new Vector3 (i, j, k), generator);
					Chunk newChunk = generateChunk(new Vector3(i, j, k), generator);

					chunks [k + renderRadius, j + renderRadius, i + renderRadius] = newChunk;
					chunkCache [new Vector3Int (i, j, k)] = newChunk;
				}
			}
		}
		Profiler.EndSample ();
	}

	/**
	 * Shifts the chunk array in a certain direction. Used for regenerating in Controller.
	 */
	public static void shiftArray(Direction dir) {
		chunks.shift (dir);
	}

	#region Generate2D

	/**
	 * Generates flat terrain on the surface level.
	 * TODO: optimize me and make me O(n^2)
	 */
	public static float[] Generate2D(Vector3 position) {
		int numPoints = (int)(size * precision);

		// We generate an extra vertex on each end to allow for seamless transitions.
		int sp1 = numPoints + 1;
		float[] data = new float[sp1 * sp1 * sp1];

		// This scale value transforms "position" (in integer chunk coords) to actual
		// world coords, using "size" (# points per mesh per axis) over "scale" (perlin offset).
		// When size == scale, offsetScale == 1, so world coords == chunk coords.
		float offsetScale = numPoints / scale / precision;
		Vector3 offset = GEN_OFFSET + position * offsetScale;

		//float[] noise = new float[sp1 * sp1];
		//int count = 0;

		bool hasNonzero = false;
		float multiplier = 1.0f / numPoints;
		float noise;
		float noiseVal;
		for (int x = 0; x < sp1; x++) {
			for (int z = 0; z < sp1; z++) {
				noise = heightScale * temperatureGenerator.getValue(offset.x + (x / scale / precision), offset.z + (z / scale / precision));
				noiseVal = (noise / sp1) - position.y;

				// Note: old code (easier to understand why these values are chosen)
				// if ((((float)j / numPoints) + position.y) * sp1 < noise [(i * sp1) + k]) {
				//     data [count] = 1 (else 0)

				for (int y = 0; y < sp1; y++) {
					if (y * multiplier < noiseVal) {
						data[(x * sp1 * sp1) + (y * sp1) + z] = 1;
						hasNonzero = true;
					}
				}
			}
		}

		if (hasNonzero) {
			return data;
		} else {
			return null;
		}
	}

	#endregion

	#region Generate2DAsyc

	/**
	 * Generates flat terrain on the surface level.
	 * Does so asynchronously to minimize loading times.
	 */
	public static IEnumerator Generate2DAsync(Vector3 position, GameObject unfinishedObj) {

		#region Assign position data
		unfinishedObj.transform.position = new Vector3(position.z * size, position.y * size, position.x * size) - meshOffset;
		unfinishedObj.transform.localScale = new Vector3(1.0f / precision, 1.0f / precision, 1.0f / precision);
		unfinishedObj.name = "(" + position.x + " ," + position.y + " ," + position.z + ")";
		#endregion

		#region Create data
		int numPoints = (int)(size * precision);

		// We generate an extra vertex on each end to allow for seamless transitions.
		int sp1 = numPoints + 1;
		float[] data = new float[sp1 * sp1 * sp1];

		// This scale value transforms "position" (in integer chunk coords) to actual
		// world coords, using "size" (# points per mesh per axis) over "scale" (perlin offset).
		// When size == scale, offsetScale == 1, so world coords == chunk coords.
		float offsetScale = numPoints / scale / precision;
		Vector3 offset = GEN_OFFSET + position * offsetScale;

		bool hasNonzero = false;
		float multiplier = 1.0f / numPoints;
		float noise;
		float noiseVal;
		for (int x = 0; x < sp1; x++) {
			for (int z = 0; z < sp1; z++) {
				noise = heightScale * temperatureGenerator.getValue(offset.x + (x / scale / precision), offset.z + (z / scale / precision));
				noiseVal = (noise / sp1) - position.y;

				// Note: old code (easier to understand why these values are chosen)
				// if ((((float)j / numPoints) + position.y) * sp1 < noise [(i * sp1) + k]) {
				//     data [count] = 1 (else 0)

				for (int y = 0; y < sp1; y++) {
					if (y * multiplier < noiseVal) {
						data[(x * sp1 * sp1) + (y * sp1) + z] = 1;
						hasNonzero = true;
					}
				}

				yield return null;
			}
		}

		#endregion

		if (hasNonzero) {
			//TODO: Make an async version of marching cubes
			#region Perform Marching Cubes

			List<Vector3> verts = new List<Vector3> (DEFAULT_VERTEX_BUFFER_SIZE); 
			List<int> tris = new List<int> (DEFAULT_TRI_BUFFER_SIZE);

			Marching marching = new MarchingCubes ();
			marching.Surface = marchingSurface;

			marching.Generate(data, sp1, sp1, sp1, verts, tris);
			yield return null;

			#endregion

			assignMesh (unfinishedObj, verts.ToArray (), tris.ToArray ());
			yield return null;
		}
	}

	#endregion

	#region GenerateCave

	// Custom C code for extra speed
	[DllImport ("FastPerlin")]
	private static extern double GetValue (double x, double y, double z);

	/**
	 * Generates 3D cave systems.
	 */
	public static float[] GenerateCave(Vector3 position) {
		int numPoints = (int)(size * precision);

		// We generate an extra vertex on each end to allow for seamless transitions.
		int sp1 = numPoints + 1;
		float[] data = new float[sp1 * sp1 * sp1];

		// This scale value transforms "position" (in integer chunk coords) to actual
		// world coords, using "size" (# points per mesh per axis) over "scale" (perlin offset).
		// When size == scale, offsetScale == 1, so world coords == chunk coords.
		float offsetScale = numPoints / scale / precision;
		Vector3 offset = GEN_OFFSET + position * offsetScale;

		// We negate the value because the inverse looks better for RidgedMultifractal. 
		// Switch to positive for Perlin noise.
		int count = 0;
		for (int i = 0; i < sp1; i++) {
			for (int j = 0; j < sp1; j++) {
				for (int k = 0; k < sp1; k++) {
					data [count++] = (float) -GetValue(offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));
					//data [count++] = (float) -noiseGen.GetValue(offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));
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
		marching.Surface = marchingSurface;

		marching.Generate(data, sp1, sp1, sp1, verts, tris);
		yield return null;

		#endregion

		assignMesh (unfinishedObj, verts.ToArray (), tris.ToArray ());
		yield return null;
	}


	#endregion
}
