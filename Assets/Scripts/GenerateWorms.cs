using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using LibNoise.Generator;

public class GenerateWorms {

	private static Perlin perlin;

	private static Material defaultMaterial;
	private static PhysicMaterial defaultPhysics;

	public static float[] data;

	//private const int NUM_WORMS;

	public static int coordsToIndex(int size, int x, int y, int z) {
		return (x * size * size) + (y * size) + z;
	}

	public static Vector3 indexToCoords(int size, int i) {
		return new Vector3 ((i / size / size) % size, (i / size) % size, i % size);
	}

	public void generate() {
		perlin = new Perlin ();


	}
}
