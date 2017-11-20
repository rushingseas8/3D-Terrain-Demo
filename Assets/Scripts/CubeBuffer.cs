﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public enum Direction {
	LEFT = 0,
	RIGHT = 1,
	UP = 2,
	DOWN = 3,
	FRONT = 4,
	BACK = 5,
	NONE = -1
}

public class CubeBuffer<T> where T : UnityEngine.Object {

	private int size;
	private T[,,] cube;

	public static int[][] faceIndices;

	public CubeBuffer (int size) {
		this.size = size;
		cube = new T[size, size, size];

		// O(size^2), but run only once per game, so not too bad
		int enumLength = Enum.GetValues (typeof(Direction)).Length;
		faceIndices = new int[enumLength][];

		for (int enumCount = 0; enumCount < enumLength; enumCount++) {
			//Debug.Log ("Cube buffer init: enumCount=" + enumCount + " Direction: " + ((Direction)enumCount));
			faceIndices [enumCount] = new int[size * size];
			int count = 0;
			for (int i = 0; i < size; i++) {
				for (int j = 0; j < size; j++) {
					switch((Direction)enumCount) {
						case Direction.LEFT:
							faceIndices [enumCount] [count++] = coordsToIndex (0, i, j);
							break;
						case Direction.RIGHT:
							faceIndices [enumCount] [count++] = coordsToIndex (size - 1, i, j);
							break;
						case Direction.DOWN:
							faceIndices [enumCount] [count++] = coordsToIndex (i, 0, j);
							break;
						case Direction.UP:
							faceIndices [enumCount] [count++] = coordsToIndex (i, size - 1, j);
							break;
						case Direction.BACK:
							faceIndices [enumCount] [count++] = coordsToIndex (i, j, 0);
							break;
						case Direction.FRONT:
							faceIndices [enumCount] [count++] = coordsToIndex (i, j, size - 1);
							break;
					}
				}
			}
		}
	}

	// Access using the combined index
	public T this[int index] {
		get { return cube[(index / size / size) % size, (index / size) % size, index % size]; }
		set { cube[(index / size / size) % size, (index / size) % size, index % size] = value; }
	}

	// Access using x,y,z 
	public T this[int x, int y, int z] {
		get { return cube [x, y, z]; }
		set { cube [x, y, z] = value; }
	}

	public int coordsToIndex(int x, int y, int z) {
		return (x * size * size) + (y * size) + z;
	}

	public Vector3Int indexToCoords(int i) {
		return new Vector3Int ((int)((float)i / size / size) % size, (int)((float)i / size) % size, i % size);
	}

	public void delete (Direction face) {
		int[] indices = faceIndices [(int)face];
		for (int i = 0; i < indices.Length; i++) {
			Vector3Int pos = indexToCoords (indices[i]);
			GameObject.Destroy (cube [pos.x, pos.y, pos.z]);
			cube [pos.x, pos.y, pos.z] = null;
		}
	}

	/**
	 * Moves the cube one unit in the provided direction.
	 * 
	 * This deletes the OPPOSITE face, and shifts every element towards the
	 * deleted face. That is to say, if we move LEFT, we delete RIGHT and
	 * shift RIGHT. 
	 */
	public void shift (Direction dir) {
		int xStart = 0;
		int xEnd = size;
		int xDelta = 1;
		int xAmount = 0;

		int yStart = 0;
		int yEnd = size;
		int yDelta = 1;
		int yAmount = 0;

		int zStart = 0;
		int zEnd = size;
		int zDelta = 1;
		int zAmount = 0;

		// Set up the parameters of shifting
		switch (dir) {
			case Direction.LEFT:
				delete (Direction.RIGHT);

				xStart = size - 1;
				xEnd = 0;
				xDelta = -1;
				xAmount = -1;
				break;
			case Direction.RIGHT:
				delete (Direction.LEFT);

				xStart = 0;
				xEnd = size - 1;
				xDelta = 1;
				xAmount = 1;
				break;
			case Direction.DOWN:
				delete (Direction.UP);

				yStart = size - 1;
				yEnd = 0;
				yDelta = -1;
				yAmount = -1;
				break;
			case Direction.UP:
				delete (Direction.DOWN);

				yStart = 0;
				yEnd = size - 1;
				yDelta = 1;
				yAmount = 1;
				break;
			case Direction.BACK:
				delete (Direction.FRONT);

				zStart = size - 1;
				zEnd = 0;
				zDelta = -1;
				zAmount = -1;
				break;
			case Direction.FRONT:
				delete (Direction.BACK);

				zStart = 0;
				zEnd = size - 1;
				zDelta = 1;
				zAmount = 1;
				break;
		}

		/*
		switch (dir) {
			case Direction.LEFT:
				delete (Direction.RIGHT);
				break;
			case Direction.RIGHT:
				delete (Direction.LEFT);
				break;
			case Direction.UP:
				delete (Direction.DOWN);
				break;
			case Direction.DOWN:
				delete (Direction.UP);
				break;
		}
		*/

		// Do the actual shifting operation
		for (int x = xStart; x != xEnd; x += xDelta) {
			for (int y = yStart; y != yEnd; y += yDelta) {
				for (int z = zStart; z != zEnd; z += zDelta) {
					cube [x, y, z] = cube [x + xAmount, y + yAmount, z + zAmount];
					//cube [x, y, z].name = "(" + x + ", " + y + ", " + z + ")";
				}
			}
		}
	}
}