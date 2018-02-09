using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * Represents a given direction. Used by CubeBuffer to determine which direction to
 * shift in, and which face to delete.
 */
public enum Direction {
	LEFT = 0,
	RIGHT = 1,
	FRONT = 2,
	BACK = 3,
	UP = 4,
	DOWN = 5,
	NONE = -1
}

public class Helper {

	public static int coordsToIndex(int size, int x, int y, int z) {
		return (x * size * size) + (y * size) + z;
	}

	public static Vector3Int indexToCoords(int size, int i) {
		return new Vector3Int ((int)((float)i / size / size) % size, (int)((float)i / size) % size, i % size);
	}
}
