using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SquareBuffer {

	private int size;
	private GameObject[,] square;

	/**
	 * Storage for the indicies for each edge.
	 */
	public static int[][] edgeIndicies;

	public SquareBuffer(int size) {
		this.size = size;
		square = new GameObject[size, size];

		for (int enumCount = 0; enumCount < 4; enumCount++) {
			edgeIndicies [enumCount] = new int[size];
			for (int i = 0; i < size; i++) {
				switch ((Direction)enumCount) {
					case Direction.LEFT:
						edgeIndicies [enumCount] [i] = (0 * size) + i;
						break;
				}
			}
		}
	}


}
