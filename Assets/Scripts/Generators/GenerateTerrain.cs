using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * 2D terrain generation on the surface. 
 */
public class GenerateTerrain : Generator {

	// How many points do we generate per mesh?
	public static int size = 64;

	// How large is each square in the mesh? 1.0f means this mesh will be (size x size) units.
	public static float worldScale = 1.0f;

	// What is the resolution of the generation we use?
	public static float generationScale = 16f;

	// How tall do we want the world?
	public static float heightScale = 16f;

	#region Temperature map
	private static float temp_xOffset = 0.0f;
	private static float temp_zOffset = 0.0f;
	private static float temp_frequency = 1.0f;
	private static float temp_lacunarity = 2.0f;
	private static float temp_persistence = 0.5f;

	private static int temp_octaves = 2;
	#endregion

	private static float perlinOctaves(float x, float z, int octaves=8, float frequency=1.0f, float lacunarity=2.0f, float persistence=0.5f) {
		float value = 0.0f;
		float multiplier = 1.0f;
		x *= frequency;
		z *= frequency;
		for (int i = 0; i < octaves; i++) {
			value += multiplier * Mathf.PerlinNoise (x, z);

			multiplier *= persistence;
			x *= lacunarity;
			z *= lacunarity;
		}
		return value;
	}
		
	//TODO: this may not be done
	private static float[] getTemperature(Vector3 position) {
		int sp1 = size + 1;
		float[] data = new float[sp1 * sp1];

		float offsetScale = size / generationScale;
		Vector3 offset = position * offsetScale;

		int count = 0;
		for (int i = 0; i < sp1; i++) {
			for (int j = 0; j < sp1; j++) {
				data [count++] = perlinOctaves (
					offset.x + (i / generationScale), offset.z + (j / generationScale),
					temp_octaves,
					temp_frequency,
					temp_lacunarity,
					temp_persistence
				);
			}
		}

		return data;
	}

	private static float[] generateData(Vector3 position) {
		int sp1 = size + 1;
		float[] data = new float[sp1 * sp1];

		float offsetScale = size / generationScale;
		Vector3 offset = position * offsetScale;

		int count = 0;
		for (int i = 0; i < sp1; i++) {
			for (int j = 0; j < sp1; j++) {
				data [count++] = heightScale * perlinOctaves (offset.x + (i / generationScale), offset.z + (j / generationScale));
			}
		}

		return data;
	}

	public static void generateObj(Vector3 position) {
		float[] data = generateData (position);

		//float offsetScale = size / scale;
		Vector3 offset = position * size;

		Vector3[] vertices = new Vector3[4 * size * size];
		for (int i = 0; i < vertices.Length; i += 4) {
			int baseIndex = i / 4;
			int x = baseIndex / size;
			int z = baseIndex % size;

			vertices [i + 0] = new Vector3 (offset.x + (x * worldScale), data[(x * (size + 1)) + z], offset.z + (z * worldScale));
			vertices [i + 1] = new Vector3 (offset.x + ((x + 1) * worldScale), data[((x + 1) * (size + 1)) + z], offset.z + (z * worldScale));
			vertices [i + 2] = new Vector3 (offset.x + (x * worldScale), data[(x * (size + 1)) + z + 1], offset.z + ((z + 1) * worldScale));
			vertices [i + 3] = new Vector3 (offset.x + ((x + 1) * worldScale), data[((x + 1) * (size + 1)) + z + 1], offset.z + ((z + 1) * worldScale));
		}

		int[] triangles = new int[6 * size * size];
		for (int i = 0; i < triangles.Length; i += 6) {
			int vertIndex = (i / 6) * 4;

			triangles [i + 0] = vertIndex;
			triangles [i + 1] = vertIndex + 2;
			triangles [i + 2] = vertIndex + 1;

			triangles [i + 3] = vertIndex + 1;
			triangles [i + 4] = vertIndex + 2;
			triangles [i + 5] = vertIndex + 3;
		}

		Vector2[] uvs = new Vector2[4 * size * size];
		for (int i = 0; i < uvs.Length; i += 4) {
			uvs [i + 0] = new Vector2(0, 0);
			uvs [i + 1] = new Vector2(1, 0);
			uvs [i + 2] = new Vector2(0, 1);
			uvs [i + 3] = new Vector2(1, 1);
		}

		// Manually recalculate normals to smoothen terrain
		Vector3[] normals = new Vector3[4 * size * size];
		int count = 0;
		for (int i = 0; i < size; i++) {
			for (int j = 0; j < size; j++) {
				normals [(count * 4) + 0] = calculateNormal(ref data, i, j);
				normals [(count * 4) + 1] = calculateNormal(ref data, i + 1, j);
				normals [(count * 4) + 2] = calculateNormal(ref data, i, j + 1);
				normals [(count * 4) + 3] = calculateNormal(ref data, i + 1, j + 1);
				count++;
			}
		}

		/*
		Mesh mesh = new Mesh ();
		mesh.vertices = vertices;
		mesh.triangles = triangles;
		mesh.uv = uvs;
		mesh.normals = normals;
		//mesh.RecalculateNormals ();

		GameObject obj = new GameObject ();
		obj.AddComponent<MeshFilter> ();
		obj.AddComponent<MeshRenderer> ();
		obj.AddComponent<MeshCollider> ();

		obj.GetComponent<MeshRenderer> ().material = defaultMaterial;
		obj.GetComponent<MeshFilter> ().mesh = mesh;
		obj.GetComponent<MeshCollider>().sharedMesh = mesh; 
		*/

		GameObject shell = generateEmpty ();
		assignMesh (shell, vertices, triangles, uvs, normals);

	}

	private static Vector3 calculateNormal(ref float[] data, int i, int j) {
		// TODO: for the edge cases, query the perlin noise function instead
		// Get the derivative in the x axis
		float left  = i == 0 	? data [(i * (size + 1)) + j] : data [((i - 1) * (size + 1)) + j];
		float right = i == size ? data [(i * (size + 1)) + j] : data [((i + 1) * (size + 1)) + j];

		// Get the derivative in the z axis
		float back  = j == 0 	? data [(i * (size + 1)) + j] : data [(i * (size + 1)) + j - 1];
		float front = j == size ? data [(i * (size + 1)) + j] : data [(i * (size + 1)) + j + 1];

		return new Vector3((right - left) * -0.5f, 1f, (front - back) * -0.5f).normalized;
	}

	public static void generate() {
		generateObj (new Vector3 (0, 0, 0));
	}
}
