using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Generator {

	public static int size = 3;
	public static bool[] data;

	private static int UP = 1;
	private static int DOWN = 2;
	private static int RIGHT = 4;
	private static int LEFT = 8;
	private static int FORWARD = 16;
	private static int BACKWARD = 32;

	private static Material defaultMaterial;
	private static PhysicMaterial defaultPhysics;

	static Generator() {
		defaultPhysics = new PhysicMaterial ();
		defaultPhysics.bounciness = 0f;
		defaultPhysics.dynamicFriction = 0.4f;
		defaultPhysics.staticFriction = 0.7f;
	}

	private static int getBitvector(int x, int y, int z) {
		int toReturn = 0;
		if (data [coordsToIndex (x, y + 1, z)]) {
			toReturn += UP;
		}
		if (data [coordsToIndex (x, y - 1, z)]) {
			toReturn += DOWN;
		}
		if (data [coordsToIndex (x + 1, y, z)]) {
			toReturn += RIGHT;
		}
		if (data [coordsToIndex (x - 1, y, z)]) {
			toReturn += LEFT;
		}
		if (data [coordsToIndex (x, y, z + 1)]) {
			toReturn += FORWARD;
		}
		if (data [coordsToIndex (x, y, z - 1)]) {
			toReturn += BACKWARD;
		}
		return toReturn;
	}

	private static int getNumNeighbors(int bv) {
		int count = 0;
		while (bv != 0) {
			count += bv & 1;
			bv = bv >> 1;
		}
		return count;
	}

	public static int coordsToIndex(int x, int y, int z) {
		return (x * size * size) + (y * size) + z;
	}

	public static Vector3 indexToCoords(int i) {
		return new Vector3 ((i / size / size) % size, (i / size) % size, i % size);
	}

	public static void generate() {
		data = new bool[size * size * size];
		for (int i = 0; i < data.Length; i++) {
			data [i] = false;
		}

		// Center point is always on
		data [coordsToIndex (1, 1, 1)] = true;

		data [coordsToIndex (1, 0, 1)] = true; // DOWN
		data [coordsToIndex (1, 2, 1)] = true; // UP

		data [coordsToIndex (0, 1, 1)] = true; // LEFT
		data [coordsToIndex (2, 1, 1)] = true; // RIGHT

		//data [coordsToIndex (1, 1, 0)] = true; // BACK
		data [coordsToIndex (1, 1, 2)] = true; // FRONT

		// Vertex array
		Vector3[] vertices = new Vector3[size * size * size];
		for (int i = 0; i < size; i++) {
			for (int j = 0; j < size; j++) {
				for (int k = 0; k < size; k++) {
					vertices [coordsToIndex (i, j, k)] = new Vector3 (i, j, k);
				}
			}
		}

		int[] tris = generateTriangles (1, 1, 1);

		Mesh mesh = new Mesh ();
		mesh.vertices = vertices;
		mesh.triangles = tris;
		mesh.RecalculateNormals();

		GameObject newObj = new GameObject ();
		newObj.AddComponent<MeshFilter> ();
		newObj.AddComponent<MeshRenderer> ();

		newObj.GetComponent<MeshFilter> ().mesh = mesh;
		newObj.GetComponent<MeshRenderer> ().material = new Material(Shader.Find("Diffuse"));

		//newObj.GetComponent<MeshCollider>().sharedMesh = mesh; 

		newObj.transform.position = new Vector3(0,0,0);

	}

	// Creates a triangle fan around "self", given adjacent vertices in "arr".
	private static int[] cycle(int self, int[] arr) {
		int[] toReturn = new int[arr.Length * 3];
		for (int i = 0; i < arr.Length; i++) {
			toReturn [(i * 3) + 0] = self;
			toReturn [(i * 3) + 1] = arr[i % arr.Length];
			toReturn [(i * 3) + 2] = arr[(i + 1) % arr.Length];
		}
		return toReturn;
	}

	// Assumes data is computed
	public static int[] generateTriangles(int x, int y, int z) {
		//List<int> toReturnList = new List<int> ();
		int bv = getBitvector (x, y, z);
		int count = getNumNeighbors (bv);

		int self = coordsToIndex (x, y, z);
		int up = coordsToIndex (x, y + 1, z);
		int down = coordsToIndex (x, y - 1, z);
		int left = coordsToIndex (x - 1, y, z);
		int right = coordsToIndex (x + 1, y, z);
		int forward = coordsToIndex (x, y, z + 1);
		int backward = coordsToIndex (x, y, z - 1);

		switch (count) {
		case 6:
			return null;
		case 5:
			if ((bv & UP) == 0) {
				return cycle (self, new int[] { left, forward, right, backward });
			} else if ((bv & DOWN) == 0) {
				return cycle (self, new int[] { right, forward, left, backward });
			} else if ((bv & LEFT) == 0) {
				return cycle (self, new int[] { up, backward, down, forward });
			} else if ((bv & RIGHT) == 0) {
				return cycle (self, new int[] { up, forward, down, backward });
			} else if ((bv & FORWARD) == 0) {
				return cycle (self, new int[] { up, left, down, right });
			} else if ((bv & BACKWARD) == 0) {
				return cycle (self, new int[] { up, right, down, left });
			}
			return null;
		default:
			return null;
		}


		/*
				toReturnList.Add (coordsToIndex(x, y, z));
				toReturnList.Add (coordsToIndex (x + 1, y, z));
				toReturnList.Add (coordsToIndex (x, y, z + 1));

				toReturnList.Add (coordsToIndex(x, y, z));
				toReturnList.Add (coordsToIndex (x, y, z + 1));
				toReturnList.Add (coordsToIndex (x - 1, y, z));

				toReturnList.Add (coordsToIndex(x, y, z));
				toReturnList.Add (coordsToIndex (x - 1, y, z));
				toReturnList.Add (coordsToIndex (x, y, z - 1));

				toReturnList.Add (coordsToIndex(x, y, z));
				toReturnList.Add (coordsToIndex (x, y, z - 1));
				toReturnList.Add (coordsToIndex (x + 1, y, z));
				*/

		//return toReturnList.ToArray();
	}
}
