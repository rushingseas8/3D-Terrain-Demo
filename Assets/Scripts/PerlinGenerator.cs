using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerlinGenerator {

	protected int octaves;
	protected float frequency;
	protected float lacunarity;
	protected float persistence;
	protected float xOffset;
	protected float zOffset;

	public PerlinGenerator(float xOffset=0.0f, float zOffset=0.0f, int octaves=8, float frequency=1.0f, float lacunarity=2.0f, float persistence=0.5f) {
		this.xOffset = xOffset;
		this.zOffset = zOffset;
		this.octaves = octaves;
		this.frequency = frequency;
		this.lacunarity = lacunarity;
		this.persistence = persistence;
	}

	public float getValue(float x, float z) {
		float value = 0.0f;
		float multiplier = 1.0f;
		x = (x + xOffset) * frequency;
		z = (z + zOffset) * frequency;
		for (int i = 0; i < octaves; i++) {
			value += multiplier * Mathf.PerlinNoise (x, z);

			multiplier *= persistence;
			x *= lacunarity;
			z *= lacunarity;
		}
		return value;
	}
}
