using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

using LibNoise.Generator;
using MarchingCubesProject;

public class Generator2D : GeneratorBase {

	//private static RidgedMultifractal noiseGen;
	private static float marchingSurface = 0.5f;

	// How large is each mesh, in points?
	public static int size = 8;

	// The scale multiplier on the noise we use. Larger values = larger terrain, but less detail.
	// Note that this shouldn't ever be 1.0 because of gradient noise being 0 at integer boundaries.
	// If you want a scale of 1.0, try using 1.1 instead.
	public static float scale = 16f;

	//TODO: add a "density" parameter for dividing the actual scale by some value
	// that way we can have a larger mesh per unit area for more accurate land approximation

	public static int renderRadius = 2;
	public static int renderDiameter = (renderRadius * 2) + 1;

	// By how much should each mesh be offset by default? This is to center it around the player.
	private static Vector3 meshOffset;

	/*
	 * A 3D array of GameObjects representing the currently loaded cave meshes.
	 * This gets shifted around and regenerated based on the player movement.
	 */
	public static CubeBuffer chunks;

	/**
	 * A dictionary of cave meshes, sorted by their positions.
	 */
	public static Dictionary<Vector3Int, GameObject> chunkCache;

	// To help the garbage collector, we provide a default size for the vertex array.
	// Too small means resizing (slow!) and too big means a lot to clean up (slow!)
	private const int DEFAULT_VERTEX_BUFFER_SIZE = 1800;	// Minimum found to be 1700; adding some room for error.
	private const int DEFAULT_TRI_BUFFER_SIZE = 1750;		// Minimum 1650.

	// An offset for the terrain gen.
	private static Vector3 GEN_OFFSET = new Vector3 (1023, 1942, 7777);

	static Generator2D() {
		meshOffset = new Vector3 (size / 2, size / 2, size / 2); // Centered on the player; endless terrain

		chunkCache = new Dictionary<Vector3Int, GameObject> ();

		chunks = new CubeBuffer (renderDiameter);
		for (int i = 0; i < renderDiameter * renderDiameter * renderDiameter; i++) {
			chunks[i] = null;
		}
	}

	public static void generate() {
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

		float[] noise = new float[sp1 * sp1];
		int count = 0;
		for (int i = 0; i < sp1; i++) {
			for (int j = 0; j < sp1; j++) {
				noise[count++] = Mathf.PerlinNoise(offset.x + (i / scale), offset.z + (j / scale));
			}
		}


		count = 0;
		for (int i = 0; i < sp1; i++) {
			for (int j = 0; j < sp1; j++) {
				for (int k = 0; k < sp1; k++) {
					//data [count++] = (float) -GetValue(offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));
					//data [count++] = (float) -noiseGen.GetValue(offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));
					if ((j / scale) < noise [(i * sp1) + k] - (position.y * offsetScale)) {
						data [count] = 1;
					} else {
						data [count] = 0;
					}
					count++;
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
		marching.Surface = marchingSurface;

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
}
