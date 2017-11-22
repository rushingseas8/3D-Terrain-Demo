using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

// DLL support
using System.Runtime.InteropServices;

using LibNoise.Generator;
using MarchingCubesProject;

public class Generator : MonoBehaviour {

	//private static RidgedMultifractal noiseGen;
	//private static SimplexNoiseGenerator noiseGen;

	public static int size = 4;
	public static float scale = 16f;
	//public static float[] data;

	private static Vector3 meshOffset;

	public static float resolution = 2.0f;

	private static Material defaultMaterial;
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

	public static int renderRadius = 6;
	public static int renderDiameter = (renderRadius * 2) + 1;

	// We use the custom C code for extra speed
	[DllImport ("FastPerlin")]
	private static extern double GetValue (double x, double y, double z);

	//[DllImport ("FastPerlin")]
	//private static extern void GetValue_All (double[,,] data, double x, double y, double z, double step);

	// To help the garbage collector, we provide a default size for the vertex array.
	// Too small means resizing (slow!) and too big means a lot to clean up (slow!)
	private const int DEFAULT_VERTEX_BUFFER_SIZE = 1800;	// Minimum found to be 1700; adding some room for error.
	private const int DEFAULT_TRI_BUFFER_SIZE = 1750;		// Minimum 1650.

	private static Vector3 GEN_OFFSET = new Vector3 (1023, 1942, 7777);

	static Generator() {
		//noiseGen = new RidgedMultifractal ();
		//noiseGen = new SimplexNoiseGenerator("test");
		//noiseGen.OctaveCount = 4;

		meshOffset = new Vector3 (size / 2, size / 2, size / 2);

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

		/*
		if (renderDiameter == 9) {
			RenderSettings.fogDensity = 0.07f;
		} else if (renderDiameter == 7) {
			RenderSettings.fogDensity = 0.10f;
		} else if (renderDiameter == 5) {
			RenderSettings.fogDensity = 0.17f;
		}
		*/
		RenderSettings.fog = true;
		RenderSettings.fogDensity = 0.10f;

		//RenderSettings.fogDensity = Mathf.Exp (-0.33f * renderDiameter);
	}

	public static void generate() {
		//noiseGen = new RidgedMultifractal ();
		//noiseGen.OctaveCount = 10;

		//int count = 3;

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

		/*
		// Testing with just cubes
		float offsetScale = size / scale;
		for (int i = -renderRadius; i <= renderRadius; i++) {
			for (int j = -renderRadius; j <= renderRadius; j++) {
				for (int k = -renderRadius; k <= renderRadius; k++) {
					GameObject newCube = GameObject.CreatePrimitive (PrimitiveType.Cube);
					newCube.transform.position = new Vector3 (i, j, k);
					newCube.name = "(" + i + ", " + j + ", " + k + ")";
					chunks [i + renderRadius, j + renderRadius, k + renderRadius] = newCube;
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
					//data [Helper.coordsToIndex (sp1, i, j, k)] = (float) -noiseGen.GetValue(
					//	offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));

					//(x * size * size) + (y * size) + z

					data [count++] = (float) -GetValue(
						offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));
				}
			}
		}

		return data;

		/*
		double[,,] data = new double[9,9,9];
		for (int i = 0; i < 9; i++) {
			for (int j = 0; j < 9; j++) {
				for (int k = 0; k < 9; k++) {
					data [i, j, k] = 0;
				}
			}
		}
		GetValue_All(data, offset.x, offset.y, offset.z, 1.0 / scale);

		float[] toReturn = new float[9 * 9 * 9];
		for (int i = 0; i < 9; i++) {
			for (int j = 0; j < 9; j++) {
				for (int k = 0; k < 9; k++) {
					toReturn [(i * 81) + (j * 9) + k] = (float) data [i, j, k];
				}
			}
		}

		return toReturn;
		*/
	}

	private static GameObject generateObj(Vector3 position, bool doubleSided = false) {
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

		//Debug.Log (verts.Count);

		Profiler.BeginSample ("Mesh generation");
		GameObject newObj = generateEmpty ();
		assignMesh (newObj, verts.ToArray (), tris.ToArray ());
		newObj.transform.position = new Vector3(position.z * size, position.y * size, position.x * size) - meshOffset;
		newObj.name = "(" + position.x + " ," + position.y + " ," + position.z + ")";

		//newObj.transform.localScale = new Vector3 (resolution, resolution, resolution);

		Profiler.EndSample ();
		return newObj;

		/*
		if (doubleSided) {
			List<int> reverseTris = new List<int>();
			for (int i = 0; i < tris.Count; i += 3) {
				reverseTris.Add (tris [i]);
				reverseTris.Add (tris [i + 2]);
				reverseTris.Add (tris [i + 1]);
			}

			Mesh mesh2 = new Mesh ();
			mesh2.vertices = verts.ToArray ();
			mesh2.triangles = reverseTris.ToArray();
			mesh2.RecalculateNormals();

			GameObject newObj2 = new GameObject ();
			newObj2.AddComponent<MeshFilter> ();
			newObj2.AddComponent<MeshRenderer> ();
			newObj2.AddComponent<MeshCollider> ();

			newObj2.GetComponent<MeshFilter> ().mesh = mesh2;
			newObj2.GetComponent<MeshRenderer> ().material = new Material(Shader.Find("Diffuse"));
			newObj2.GetComponent<MeshCollider>().sharedMesh = mesh2; 

			newObj2.transform.position = new Vector3(position.z * size, position.y * size, position.x * size) -
				new Vector3(size / 2, size / 2, size / 2);
			//newObj2.transform.localScale = new Vector3 (resolution, resolution, resolution);
		}
		*/
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

	public static IEnumerator generateAsync(Vector3 position, GameObject unfinishedObj, bool doubleSided=false) {
		
		if (unfinishedObj != null) {
			unfinishedObj.transform.position = new Vector3 (position.z * size, position.y * size, position.x * size) - meshOffset;
			//unfinishedObj.name = "(" + position.x + " ," + position.y + " ," + position.z + ")";
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
		//Debug.Log("Shift array called "  + x + ", " + y + ", " + z);
		chunks.shift (dir);
	}

	void Start() {
	}

	void Update() {
		
	}
}
