using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

using LibNoise.Generator;
using MarchingCubesProject;

//TODO: merge common features (such as size, scale, etc.) into GeneratorBase and extend from there
public class Generator2D : Generator {

	//private static RidgedMultifractal noiseGen;
	//private static float marchingSurface = 0.5f;

	// How large is each mesh, in points?
	//public static int size = 8;

	// The scale multiplier on the perlin noise. Larger = more zoomed in, so less detailed features.
	// Note that this shouldn't ever be 1.0 because of gradient noise being 0 at integer boundaries.
	// If you want a scale of 1.0, try using 1.1 instead.
	//public static float scale = 8f; //16f;

	//TODO: this changes actual height scale. Default 1.0f makes generated height 0-1.
	//public static float heightScale = 8.0f;

	//TODO: add a "precision" parameter for dividing the actual scale by some value
	// that way we can have a larger mesh per unit area for more accurate land approximation
	//public static float precision = 1.0f;

	//public static int renderRadius = 1;
	//public static int renderDiameter = (renderRadius * 2) + 1;

	// By how much should each mesh be offset by default? This is to center it around the player.
	//private static Vector3 meshOffset;

	/*
	 * A 3D array of GameObjects representing the currently loaded cave meshes.
	 * This gets shifted around and regenerated based on the player movement.
	 */
	//public static CubeBuffer chunks;

	/**
	 * A dictionary of cave meshes, sorted by their positions.
	 */
	//public static Dictionary<Vector3Int, GameObject> chunkCache;

	// To help the garbage collector, we provide a default size for the vertex array.
	// Too small means resizing (slow!) and too big means a lot to clean up (slow!)
	//private const int DEFAULT_VERTEX_BUFFER_SIZE = 1800;	// Minimum found to be 1700; adding some room for error.
	//private const int DEFAULT_TRI_BUFFER_SIZE = 1750;		// Minimum 1650.

	// An offset for the terrain gen.
	//private static Vector3 GEN_OFFSET = new Vector3 (1023, 1942, 7777);

	public Generator2D() {
		meshOffset = new Vector3 ((size / precision) / 2, (size / precision) / 2, (size / precision) / 2); // Centered on the player; endless terrain

		chunkCache = new Dictionary<Vector3Int, Chunk> ();

		chunks = new CubeBuffer<Chunk> (renderDiameter);
		for (int i = 0; i < renderDiameter * renderDiameter * renderDiameter; i++) {
			chunks[i] = null;
		}
	}

	public static float[] generateData(Vector3 position) {
		int numPoints = (int)(size * precision);

		// We generate an extra vertex on each end to allow for seamless transitions.
		int sp1 = numPoints + 1;
		float[] data = new float[sp1 * sp1 * sp1];

		// This scale value transforms "position" (in integer chunk coords) to actual
		// world coords, using "size" (# points per mesh per axis) over "scale" (perlin offset).
		// When size == scale, offsetScale == 1, so world coords == chunk coords.
		float offsetScale = numPoints / scale / precision;
		Vector3 offset = GEN_OFFSET + position * offsetScale;

		float[] noise = new float[sp1 * sp1];
		int count = 0;
		for (int i = 0; i < sp1; i++) {
			for (int j = 0; j < sp1; j++) {
				noise[count++] = heightScale * Mathf.PerlinNoise(offset.x + (i / scale / precision), offset.z + (j / scale / precision));
			}
		}


		count = 0;
		for (int i = 0; i < sp1; i++) {
			for (int j = 0; j < sp1; j++) {
				for (int k = 0; k < sp1; k++) {
					//data [count++] = (float) -GetValue(offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));
					//data [count++] = (float) -noiseGen.GetValue(offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));
					if ((((float)j / numPoints) + position.y) * sp1 < noise [(i * sp1) + k]) {
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

	/**
	 * Initializes the land around the player.
	 */
	/*
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
	*/
}
