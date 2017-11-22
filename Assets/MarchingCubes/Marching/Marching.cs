using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine.Profiling;

using UnityEngine;

namespace MarchingCubesProject
{
    public abstract class Marching : IMarching
    {

        public float Surface { get; set; }

        private float[] Cube { get; set; }

        /// <summary>
        /// Winding order of triangles use 2,1,0 or 0,1,2
        /// </summary>
        protected int[] WindingOrder { get; private set; }

        public Marching(float surface = 0.5f)
        {
            Surface = surface;
            Cube = new float[8];
            WindingOrder = new int[] { 0, 1, 2 };
        }

		public virtual void Generate(float[] voxels, int width, int height, int depth, IList<Vector3> verts, IList<int> indices)
        {
            if (Surface > 0.0f)
            {
                WindingOrder[0] = 0;
                WindingOrder[1] = 1;
                WindingOrder[2] = 2;
            }
            else
            {
                WindingOrder[0] = 2;
                WindingOrder[1] = 1;
                WindingOrder[2] = 0;
            }

            int x, y, z;
			int wh = width * height;

			int[] yw = new int[height];
			for (int i = 0; i < height; i++) { yw [i] = i * width; }

			int[] zwh = new int[depth];
			for (int i = 0; i < depth; i++) { zwh [i] = i * wh; }

            for (x = 0; x < width - 1; x++) {
                for (y = 0; y < height - 1; y++) {
                    for (z = 0; z < depth - 1; z++) {
                        //Get the values in the 8 neighbours which make up a cube
						//Profiler.BeginSample("Neighbor search");

						int baseIndex = x + yw[y] + zwh[z];
						int b1 = baseIndex + 1;
						int b1w = b1 + width;
						int bw = baseIndex + width;

						Cube [0] = voxels[baseIndex];
						Cube [1] = voxels[b1];
						Cube [2] = voxels[b1w];
						Cube [3] = voxels[bw];

						Cube [4] = voxels[baseIndex + wh];
						Cube [5] = voxels[b1 + wh];
						Cube [6] = voxels[b1w + wh];
						Cube [7] = voxels[bw + wh];

						//Profiler.EndSample ();

                        //Perform algorithm
						//Profiler.BeginSample("March");
                        March(x, y, z, Cube, verts, indices);
						//Profiler.EndSample ();
                    }
                }
			}

        }

         /// <summary>
        /// MarchCube performs the Marching algorithm on a single cube
        /// </summary>
        protected abstract void March(float x, float y, float z, float[] cube, IList<Vector3> vertList, IList<int> indexList);

        /// <summary>
        /// GetOffset finds the approximate point of intersection of the surface
        /// between two points with the values v1 and v2
        /// </summary>
        protected virtual float GetOffset(float v1, float v2)
        {
            float delta = v2 - v1;
            return (delta == 0.0f) ? Surface : (Surface - v1) / delta;
        }

        /// <summary>
        /// VertexOffset lists the positions, relative to vertex0, 
        /// of each of the 8 vertices of a cube.
        /// vertexOffset[8][3]
        /// </summary>
        protected static readonly int[,] VertexOffset = new int[,]
	    {
	        {0, 0, 0},{1, 0, 0},{1, 1, 0},{0, 1, 0},
	        {0, 0, 1},{1, 0, 1},{1, 1, 1},{0, 1, 1}
	    };

    }

}
