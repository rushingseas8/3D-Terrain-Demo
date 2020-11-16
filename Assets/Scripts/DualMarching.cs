using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Basic idea of DMC:
/// 
/// (pulled from this)
/// https://wordsandbuttons.online/interactive_explanation_of_marching_cubes_and_dual_contouring.html
/// 
/// 0. In MC, we look within a CUBE and if the function changes sign between vertices, we add a line between EDGES.
///     On the other hand, in DMC, we look at EDGES, and if the function changes sign between vertices, we add a vertex at
///     the centroid of the cube. This centroid is the average of the edge points.
/// 1. Thus, one possible (citation needed) algorithm is to run MC on a given grid, and then use the edge points
///     emitted to generate DMC points. 
/// 2. We then need a custom algorithm to join these DMC points together. Specifically, we'll likely have to stitch from
///     the edge points to the centroid to the edge points; this may have to be repeated depending on the exact case. This
///     will allow us to handle boundary points, since then we won't have issues just connecting centroids (since one one exist).
///     NB: this might be the annoying part, since we'll have to do cases manually. Oof. Edge cases also exist.
/// 3. 
/// 
/// 
/// </summary>
public class DualMarching
{
    private float[] data;
    private int width, height, depth;
    private int[] dims;
    private float iso;
    private bool generateManifold = true;

#region Static stuff
    /// Encodes the ambiguous face of cube configurations, which
    /// can cause non-manifold meshes.
    /// Non-problematic configurations have a value of 255.
    /// The first bit of each value actually encodes a positive or negative
    /// direction while the second and third bit enumerate the axis.
    private static readonly int[] problematicConfigs = 
    {
        255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,
        255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,
        255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,
        255,255,255,255,255,255,255,255,255,255,255,255,255,1,0,255,
        255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,
        255,255,255,255,255,255,255,255,255,255,255,3,255,255,2,255,
        255,255,255,255,255,255,255,5,255,255,255,255,255,255,5,5,
        255,255,255,255,255,255,4,255,255,255,3,3,1,1,255,255,
        255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,
        255,255,255,255,255,255,255,255,255,255,255,5,255,5,255,5,
        255,255,255,255,255,255,255,3,255,255,255,255,255,2,255,255,
        255,255,255,255,255,3,255,3,255,4,255,255,0,255,0,255,
        255,255,255,255,255,255,255,1,255,255,255,0,255,255,255,255,
        255,255,255,1,255,255,255,1,255,4,2,255,255,255,2,255,
        255,255,255,0,255,2,4,255,255,255,255,0,255,2,255,255,
        255,255,255,255,255,255,4,255,255,4,255,255,255,255,255,255
    };

    // Edge constants
    private static readonly int
        EDGE0 = 1,
        EDGE1 = 1 << 1,
        EDGE2 = 1 << 2,
        EDGE3 = 1 << 3,
        EDGE4 = 1 << 4,
        EDGE5 = 1 << 5,
        EDGE6 = 1 << 6,
        EDGE7 = 1 << 7,
        EDGE8 = 1 << 8,
        EDGE9 = 1 << 9,
        EDGE10 = 1 << 10,
        EDGE11 = 1 << 11;


    // Encodes the edge vertices for the 256 marching cubes cases.
    // A marching cube case produces up to four faces and thus, up to four
    // dual points.
    private static readonly int[,] dualPointsList = 
    {
        {0, 0, 0, 0}, // 0
        {EDGE0|EDGE3|EDGE8, 0, 0, 0}, // 1
        {EDGE0|EDGE1|EDGE9, 0, 0, 0}, // 2
        {EDGE1|EDGE3|EDGE8|EDGE9, 0, 0, 0}, // 3
        {EDGE4|EDGE7|EDGE8, 0, 0, 0}, // 4
        {EDGE0|EDGE3|EDGE4|EDGE7, 0, 0, 0}, // 5
        {EDGE0|EDGE1|EDGE9, EDGE4|EDGE7|EDGE8, 0, 0}, // 6
        {EDGE1|EDGE3|EDGE4|EDGE7|EDGE9, 0, 0, 0}, // 7
        {EDGE4|EDGE5|EDGE9, 0, 0, 0}, // 8
        {EDGE0|EDGE3|EDGE8, EDGE4|EDGE5|EDGE9, 0, 0}, // 9
        {EDGE0|EDGE1|EDGE4|EDGE5, 0, 0, 0}, // 10
        {EDGE1|EDGE3|EDGE4|EDGE5|EDGE8, 0, 0, 0}, // 11
        {EDGE5|EDGE7|EDGE8|EDGE9, 0, 0, 0}, // 12
        {EDGE0|EDGE3|EDGE5|EDGE7|EDGE9, 0, 0, 0}, // 13
        {EDGE0|EDGE1|EDGE5|EDGE7|EDGE8, 0, 0, 0}, // 14
        {EDGE1|EDGE3|EDGE5|EDGE7, 0, 0, 0}, // 15
        {EDGE2|EDGE3|EDGE11, 0, 0, 0}, // 16
        {EDGE0|EDGE2|EDGE8|EDGE11, 0, 0, 0}, // 17
        {EDGE0|EDGE1|EDGE9, EDGE2|EDGE3|EDGE11, 0, 0}, // 18
        {EDGE1|EDGE2|EDGE8|EDGE9|EDGE11, 0, 0, 0}, // 19
        {EDGE4|EDGE7|EDGE8, EDGE2|EDGE3|EDGE11, 0, 0}, // 20
        {EDGE0|EDGE2|EDGE4|EDGE7|EDGE11, 0, 0, 0}, // 21
        {EDGE0|EDGE1|EDGE9, EDGE4|EDGE7|EDGE8, EDGE2|EDGE3|EDGE11, 0}, // 22
        {EDGE1|EDGE2|EDGE4|EDGE7|EDGE9|EDGE11, 0, 0, 0}, // 23
        {EDGE4|EDGE5|EDGE9, EDGE2|EDGE3|EDGE11, 0, 0}, // 24
        {EDGE0|EDGE2|EDGE8|EDGE11, EDGE4|EDGE5|EDGE9, 0, 0}, // 25
        {EDGE0|EDGE1|EDGE4|EDGE5, EDGE2|EDGE3|EDGE11, 0, 0}, // 26
        {EDGE1|EDGE2|EDGE4|EDGE5|EDGE8|EDGE11, 0, 0, 0}, // 27
        {EDGE5|EDGE7|EDGE8|EDGE9, EDGE2|EDGE3|EDGE11, 0, 0}, // 28
        {EDGE0|EDGE2|EDGE5|EDGE7|EDGE9|EDGE11, 0, 0, 0}, // 29
        {EDGE0|EDGE1|EDGE5|EDGE7|EDGE8, EDGE2|EDGE3|EDGE11, 0, 0}, // 30
        {EDGE1|EDGE2|EDGE5|EDGE7|EDGE11, 0, 0, 0}, // 31
        {EDGE1|EDGE2|EDGE10, 0, 0, 0}, // 32
        {EDGE0|EDGE3|EDGE8, EDGE1|EDGE2|EDGE10, 0, 0}, // 33
        {EDGE0|EDGE2|EDGE9|EDGE10, 0, 0, 0}, // 34
        {EDGE2|EDGE3|EDGE8|EDGE9|EDGE10, 0, 0, 0}, // 35
        {EDGE4|EDGE7|EDGE8, EDGE1|EDGE2|EDGE10, 0, 0}, // 36
        {EDGE0|EDGE3|EDGE4|EDGE7, EDGE1|EDGE2|EDGE10, 0, 0}, // 37
        {EDGE0|EDGE2|EDGE9|EDGE10, EDGE4|EDGE7|EDGE8, 0, 0}, // 38
        {EDGE2|EDGE3|EDGE4|EDGE7|EDGE9|EDGE10, 0, 0, 0}, // 39
        {EDGE4|EDGE5|EDGE9, EDGE1|EDGE2|EDGE10, 0, 0}, // 40
        {EDGE0|EDGE3|EDGE8, EDGE4|EDGE5|EDGE9, EDGE1|EDGE2|EDGE10, 0}, // 41
        {EDGE0|EDGE2|EDGE4|EDGE5|EDGE10, 0, 0, 0}, // 42
        {EDGE2|EDGE3|EDGE4|EDGE5|EDGE8|EDGE10, 0, 0, 0}, // 43
        {EDGE5|EDGE7|EDGE8|EDGE9, EDGE1|EDGE2|EDGE10, 0, 0}, // 44
        {EDGE0|EDGE3|EDGE5|EDGE7|EDGE9, EDGE1|EDGE2|EDGE10, 0, 0}, // 45
        {EDGE0|EDGE2|EDGE5|EDGE7|EDGE8|EDGE10, 0, 0, 0}, // 46
        {EDGE2|EDGE3|EDGE5|EDGE7|EDGE10, 0, 0, 0}, // 47
        {EDGE1|EDGE3|EDGE10|EDGE11, 0, 0, 0}, // 48
        {EDGE0|EDGE1|EDGE8|EDGE10|EDGE11, 0, 0, 0}, // 49
        {EDGE0|EDGE3|EDGE9|EDGE10|EDGE11, 0, 0, 0}, // 50
        {EDGE8|EDGE9|EDGE10|EDGE11, 0, 0, 0}, // 51
        {EDGE4|EDGE7|EDGE8, EDGE1|EDGE3|EDGE10|EDGE11, 0, 0}, // 52
        {EDGE0|EDGE1|EDGE4|EDGE7|EDGE10|EDGE11, 0, 0, 0}, // 53
        {EDGE0|EDGE3|EDGE9|EDGE10|EDGE11, EDGE4|EDGE7|EDGE8, 0, 0}, // 54
        {EDGE4|EDGE7|EDGE9|EDGE10|EDGE11, 0, 0, 0}, // 55
        {EDGE4|EDGE5|EDGE9, EDGE1|EDGE3|EDGE10|EDGE11, 0, 0}, // 56
        {EDGE0|EDGE1|EDGE8|EDGE10|EDGE11, EDGE4|EDGE5|EDGE9, 0, 0}, // 57
        {EDGE0|EDGE3|EDGE4|EDGE5|EDGE10|EDGE11, 0, 0, 0}, // 58
        {EDGE4|EDGE5|EDGE8|EDGE10|EDGE11, 0, 0, 0}, // 59
        {EDGE5|EDGE7|EDGE8|EDGE9, EDGE1|EDGE3|EDGE10|EDGE11, 0, 0}, // 60
        {EDGE0|EDGE1|EDGE5|EDGE7|EDGE9|EDGE10|EDGE11, 0, 0, 0}, // 61
        {EDGE0|EDGE3|EDGE5|EDGE7|EDGE8|EDGE10|EDGE11, 0, 0, 0}, // 62
        {EDGE5|EDGE7|EDGE10|EDGE11, 0, 0, 0}, // 63
        {EDGE6|EDGE7|EDGE11, 0, 0, 0}, // 64
        {EDGE0|EDGE3|EDGE8, EDGE6|EDGE7|EDGE11, 0, 0}, // 65
        {EDGE0|EDGE1|EDGE9, EDGE6|EDGE7|EDGE11, 0, 0}, // 66
        {EDGE1|EDGE3|EDGE8|EDGE9, EDGE6|EDGE7|EDGE11, 0, 0}, // 67
        {EDGE4|EDGE6|EDGE8|EDGE11, 0, 0, 0}, // 68
        {EDGE0|EDGE3|EDGE4|EDGE6|EDGE11, 0, 0, 0}, // 69
        {EDGE0|EDGE1|EDGE9, EDGE4|EDGE6|EDGE8|EDGE11, 0, 0}, // 70
        {EDGE1|EDGE3|EDGE4|EDGE6|EDGE9|EDGE11, 0, 0, 0}, // 71
        {EDGE4|EDGE5|EDGE9, EDGE6|EDGE7|EDGE11, 0, 0}, // 72
        {EDGE0|EDGE3|EDGE8, EDGE4|EDGE5|EDGE9, EDGE6|EDGE7|EDGE11, 0}, // 73
        {EDGE0|EDGE1|EDGE4|EDGE5, EDGE6|EDGE7|EDGE11, 0, 0}, // 74
        {EDGE1|EDGE3|EDGE4|EDGE5|EDGE8, EDGE6|EDGE7|EDGE11, 0, 0}, // 75
        {EDGE5|EDGE6|EDGE8|EDGE9|EDGE11, 0, 0, 0}, // 76
        {EDGE0|EDGE3|EDGE5|EDGE6|EDGE9|EDGE11, 0, 0, 0}, // 77
        {EDGE0|EDGE1|EDGE5|EDGE6|EDGE8|EDGE11, 0, 0, 0}, // 78
        {EDGE1|EDGE3|EDGE5|EDGE6|EDGE11, 0, 0, 0}, // 79
        {EDGE2|EDGE3|EDGE6|EDGE7, 0, 0, 0}, // 80
        {EDGE0|EDGE2|EDGE6|EDGE7|EDGE8, 0, 0, 0}, // 81
        {EDGE0|EDGE1|EDGE9, EDGE2|EDGE3|EDGE6|EDGE7, 0, 0}, // 82
        {EDGE1|EDGE2|EDGE6|EDGE7|EDGE8|EDGE9, 0, 0, 0}, // 83
        {EDGE2|EDGE3|EDGE4|EDGE6|EDGE8, 0, 0, 0}, // 84
        {EDGE0|EDGE2|EDGE4|EDGE6, 0, 0, 0}, // 85
        {EDGE0|EDGE1|EDGE9, EDGE2|EDGE3|EDGE4|EDGE6|EDGE8, 0, 0}, // 86
        {EDGE1|EDGE2|EDGE4|EDGE6|EDGE9, 0, 0, 0}, // 87
        {EDGE4|EDGE5|EDGE9, EDGE2|EDGE3|EDGE6|EDGE7, 0, 0}, // 88
        {EDGE0|EDGE2|EDGE6|EDGE7|EDGE8, EDGE4|EDGE5|EDGE9, 0, 0}, // 89
        {EDGE0|EDGE1|EDGE4|EDGE5, EDGE2|EDGE3|EDGE6|EDGE7, 0, 0}, // 90
        {EDGE1|EDGE2|EDGE4|EDGE5|EDGE6|EDGE7|EDGE8, 0, 0, 0}, // 91
        {EDGE2|EDGE3|EDGE5|EDGE6|EDGE8|EDGE9, 0, 0, 0}, // 92
        {EDGE0|EDGE2|EDGE5|EDGE6|EDGE9, 0, 0, 0}, // 93
        {EDGE0|EDGE1|EDGE2|EDGE3|EDGE5|EDGE6|EDGE8, 0, 0, 0}, // 94
        {EDGE1|EDGE2|EDGE5|EDGE6, 0, 0, 0}, // 95
        {EDGE1|EDGE2|EDGE10, EDGE6|EDGE7|EDGE11, 0, 0}, // 96
        {EDGE0|EDGE3|EDGE8, EDGE1|EDGE2|EDGE10, EDGE6|EDGE7|EDGE11, 0}, // 97
        {EDGE0|EDGE2|EDGE9|EDGE10, EDGE6|EDGE7|EDGE11, 0, 0}, // 98
        {EDGE2|EDGE3|EDGE8|EDGE9|EDGE10, EDGE6|EDGE7|EDGE11, 0, 0}, // 99
        {EDGE4|EDGE6|EDGE8|EDGE11, EDGE1|EDGE2|EDGE10, 0, 0}, // 100
        {EDGE0|EDGE3|EDGE4|EDGE6|EDGE11, EDGE1|EDGE2|EDGE10, 0, 0}, // 101
        {EDGE0|EDGE2|EDGE9|EDGE10, EDGE4|EDGE6|EDGE8|EDGE11, 0, 0}, // 102
        {EDGE2|EDGE3|EDGE4|EDGE6|EDGE9|EDGE10|EDGE11, 0, 0, 0}, // 103
        {EDGE4|EDGE5|EDGE9, EDGE1|EDGE2|EDGE10, EDGE6|EDGE7|EDGE11, 0}, // 104
        {EDGE0|EDGE3|EDGE8, EDGE4|EDGE5|EDGE9, EDGE1|EDGE2|EDGE10, EDGE6|EDGE7|EDGE11}, // 105
        {EDGE0|EDGE2|EDGE4|EDGE5|EDGE10, EDGE6|EDGE7|EDGE11, 0, 0}, // 106
        {EDGE2|EDGE3|EDGE4|EDGE5|EDGE8|EDGE10, EDGE6|EDGE7|EDGE11, 0, 0}, // 107
        {EDGE5|EDGE6|EDGE8|EDGE9|EDGE11, EDGE1|EDGE2|EDGE10, 0, 0}, // 108
        {EDGE0|EDGE3|EDGE5|EDGE6|EDGE9|EDGE11, EDGE1|EDGE2|EDGE10, 0, 0}, // 109
        {EDGE0|EDGE2|EDGE5|EDGE6|EDGE8|EDGE10|EDGE11, 0, 0, 0}, // 110
        {EDGE2|EDGE3|EDGE5|EDGE6|EDGE10|EDGE11, 0, 0, 0}, // 111
        {EDGE1|EDGE3|EDGE6|EDGE7|EDGE10, 0, 0, 0}, // 112
        {EDGE0|EDGE1|EDGE6|EDGE7|EDGE8|EDGE10, 0, 0, 0}, // 113
        {EDGE0|EDGE3|EDGE6|EDGE7|EDGE9|EDGE10, 0, 0, 0}, // 114
        {EDGE6|EDGE7|EDGE8|EDGE9|EDGE10, 0, 0, 0}, // 115
        {EDGE1|EDGE3|EDGE4|EDGE6|EDGE8|EDGE10, 0, 0, 0}, // 116
        {EDGE0|EDGE1|EDGE4|EDGE6|EDGE10, 0, 0, 0}, // 117
        {EDGE0|EDGE3|EDGE4|EDGE6|EDGE8|EDGE9|EDGE10, 0, 0, 0}, // 118
        {EDGE4|EDGE6|EDGE9|EDGE10, 0, 0, 0}, // 119
        {EDGE4|EDGE5|EDGE9, EDGE1|EDGE3|EDGE6|EDGE7|EDGE10, 0, 0}, // 120
        {EDGE0|EDGE1|EDGE6|EDGE7|EDGE8|EDGE10, EDGE4|EDGE5|EDGE9, 0, 0}, // 121
        {EDGE0|EDGE3|EDGE4|EDGE5|EDGE6|EDGE7|EDGE10, 0, 0, 0}, // 122
        {EDGE4|EDGE5|EDGE6|EDGE7|EDGE8|EDGE10, 0, 0, 0}, // 123
        {EDGE1|EDGE3|EDGE5|EDGE6|EDGE8|EDGE9|EDGE10, 0, 0, 0}, // 124
        {EDGE0|EDGE1|EDGE5|EDGE6|EDGE9|EDGE10, 0, 0, 0}, // 125
        {EDGE0|EDGE3|EDGE8, EDGE5|EDGE6|EDGE10, 0, 0}, // 126
        {EDGE5|EDGE6|EDGE10, 0, 0, 0}, // 127
        {EDGE5|EDGE6|EDGE10, 0, 0, 0}, // 128
        {EDGE0|EDGE3|EDGE8, EDGE5|EDGE6|EDGE10, 0, 0}, // 129
        {EDGE0|EDGE1|EDGE9, EDGE5|EDGE6|EDGE10, 0, 0}, // 130
        {EDGE1|EDGE3|EDGE8|EDGE9, EDGE5|EDGE6|EDGE10, 0, 0}, // 131
        {EDGE4|EDGE7|EDGE8, EDGE5|EDGE6|EDGE10, 0, 0}, // 132
        {EDGE0|EDGE3|EDGE4|EDGE7, EDGE5|EDGE6|EDGE10, 0, 0}, // 133
        {EDGE0|EDGE1|EDGE9, EDGE4|EDGE7|EDGE8, EDGE5|EDGE6|EDGE10, 0}, // 134
        {EDGE1|EDGE3|EDGE4|EDGE7|EDGE9, EDGE5|EDGE6|EDGE10, 0, 0}, // 135
        {EDGE4|EDGE6|EDGE9|EDGE10, 0, 0, 0}, // 136
        {EDGE0|EDGE3|EDGE8, EDGE4|EDGE6|EDGE9|EDGE10, 0, 0}, // 137
        {EDGE0|EDGE1|EDGE4|EDGE6|EDGE10, 0, 0, 0}, // 138
        {EDGE1|EDGE3|EDGE4|EDGE6|EDGE8|EDGE10, 0, 0, 0}, // 139
        {EDGE6|EDGE7|EDGE8|EDGE9|EDGE10, 0, 0, 0}, // 140
        {EDGE0|EDGE3|EDGE6|EDGE7|EDGE9|EDGE10, 0, 0, 0}, // 141
        {EDGE0|EDGE1|EDGE6|EDGE7|EDGE8|EDGE10, 0, 0, 0}, // 142
        {EDGE1|EDGE3|EDGE6|EDGE7|EDGE10, 0, 0, 0}, // 143
        {EDGE2|EDGE3|EDGE11, EDGE5|EDGE6|EDGE10, 0, 0}, // 144
        {EDGE0|EDGE2|EDGE8|EDGE11, EDGE5|EDGE6|EDGE10, 0, 0}, // 145
        {EDGE0|EDGE1|EDGE9, EDGE2|EDGE3|EDGE11, EDGE5|EDGE6|EDGE10, 0}, // 146
        {EDGE1|EDGE2|EDGE8|EDGE9|EDGE11, EDGE5|EDGE6|EDGE10, 0, 0}, // 147
        {EDGE4|EDGE7|EDGE8, EDGE2|EDGE3|EDGE11, EDGE5|EDGE6|EDGE10, 0}, // 148
        {EDGE0|EDGE2|EDGE4|EDGE7|EDGE11, EDGE5|EDGE6|EDGE10, 0, 0}, // 149
        {EDGE0|EDGE1|EDGE9, EDGE4|EDGE7|EDGE8, EDGE2|EDGE3|EDGE11, EDGE5|EDGE6|EDGE10}, // 150
        {EDGE1|EDGE2|EDGE4|EDGE7|EDGE9|EDGE11, EDGE5|EDGE6|EDGE10, 0, 0}, // 151
        {EDGE4|EDGE6|EDGE9|EDGE10, EDGE2|EDGE3|EDGE11, 0, 0}, // 152
        {EDGE0|EDGE2|EDGE8|EDGE11, EDGE4|EDGE6|EDGE9|EDGE10, 0, 0}, // 153
        {EDGE0|EDGE1|EDGE4|EDGE6|EDGE10, EDGE2|EDGE3|EDGE11, 0, 0}, // 154
        {EDGE1|EDGE2|EDGE4|EDGE6|EDGE8|EDGE10|EDGE11, 0, 0, 0}, // 155
        {EDGE6|EDGE7|EDGE8|EDGE9|EDGE10, EDGE2|EDGE3|EDGE11, 0, 0}, // 156
        {EDGE0|EDGE2|EDGE6|EDGE7|EDGE9|EDGE10|EDGE11, 0, 0, 0}, // 157
        {EDGE0|EDGE1|EDGE6|EDGE7|EDGE8|EDGE10, EDGE2|EDGE3|EDGE11, 0, 0}, // 158
        {EDGE1|EDGE2|EDGE6|EDGE7|EDGE10|EDGE11, 0, 0, 0}, // 159
        {EDGE1|EDGE2|EDGE5|EDGE6, 0, 0, 0}, // 160
        {EDGE0|EDGE3|EDGE8, EDGE1|EDGE2|EDGE5|EDGE6, 0, 0}, // 161
        {EDGE0|EDGE2|EDGE5|EDGE6|EDGE9, 0, 0, 0}, // 162
        {EDGE2|EDGE3|EDGE5|EDGE6|EDGE8|EDGE9, 0, 0, 0}, // 163
        {EDGE4|EDGE7|EDGE8, EDGE1|EDGE2|EDGE5|EDGE6, 0, 0}, // 164
        {EDGE0|EDGE3|EDGE4|EDGE7, EDGE1|EDGE2|EDGE5|EDGE6, 0, 0}, // 165
        {EDGE0|EDGE2|EDGE5|EDGE6|EDGE9, EDGE4|EDGE7|EDGE8, 0, 0}, // 166
        {EDGE2|EDGE3|EDGE4|EDGE5|EDGE6|EDGE7|EDGE9, 0, 0, 0}, // 167
        {EDGE1|EDGE2|EDGE4|EDGE6|EDGE9, 0, 0, 0}, // 168
        {EDGE0|EDGE3|EDGE8, EDGE1|EDGE2|EDGE4|EDGE6|EDGE9, 0, 0}, // 169
        {EDGE0|EDGE2|EDGE4|EDGE6, 0, 0, 0}, // 170
        {EDGE2|EDGE3|EDGE4|EDGE6|EDGE8, 0, 0, 0}, // 171
        {EDGE1|EDGE2|EDGE6|EDGE7|EDGE8|EDGE9, 0, 0, 0}, // 172
        {EDGE0|EDGE1|EDGE2|EDGE3|EDGE6|EDGE7|EDGE9, 0, 0, 0}, // 173
        {EDGE0|EDGE2|EDGE6|EDGE7|EDGE8, 0, 0, 0}, // 174
        {EDGE2|EDGE3|EDGE6|EDGE7, 0, 0, 0}, // 175
        {EDGE1|EDGE3|EDGE5|EDGE6|EDGE11, 0, 0, 0}, // 176
        {EDGE0|EDGE1|EDGE5|EDGE6|EDGE8|EDGE11, 0, 0, 0}, // 177
        {EDGE0|EDGE3|EDGE5|EDGE6|EDGE9|EDGE11, 0, 0, 0}, // 178
        {EDGE5|EDGE6|EDGE8|EDGE9|EDGE11, 0, 0, 0}, // 179
        {EDGE4|EDGE7|EDGE8, EDGE1|EDGE3|EDGE5|EDGE6|EDGE11, 0, 0}, // 180
        {EDGE0|EDGE1|EDGE4|EDGE5|EDGE6|EDGE7|EDGE11, 0, 0, 0}, // 181
        {EDGE0|EDGE3|EDGE5|EDGE6|EDGE9|EDGE11, EDGE4|EDGE7|EDGE8, 0, 0}, // 182
        {EDGE4|EDGE5|EDGE6|EDGE7|EDGE9|EDGE11, 0, 0, 0}, // 183
        {EDGE1|EDGE3|EDGE4|EDGE6|EDGE9|EDGE11, 0, 0, 0}, // 184
        {EDGE0|EDGE1|EDGE4|EDGE6|EDGE8|EDGE9|EDGE11, 0, 0, 0}, // 185
        {EDGE0|EDGE3|EDGE4|EDGE6|EDGE11, 0, 0, 0}, // 186
        {EDGE4|EDGE6|EDGE8|EDGE11, 0, 0, 0}, // 187
        {EDGE1|EDGE3|EDGE6|EDGE7|EDGE8|EDGE9|EDGE11, 0, 0, 0}, // 188
        {EDGE0|EDGE1|EDGE9, EDGE6|EDGE7|EDGE11, 0, 0}, // 189
        {EDGE0|EDGE3|EDGE6|EDGE7|EDGE8|EDGE11, 0, 0, 0}, // 190
        {EDGE6|EDGE7|EDGE11, 0, 0, 0}, // 191
        {EDGE5|EDGE7|EDGE10|EDGE11, 0, 0, 0}, // 192
        {EDGE0|EDGE3|EDGE8, EDGE5|EDGE7|EDGE10|EDGE11, 0, 0}, // 193
        {EDGE0|EDGE1|EDGE9, EDGE5|EDGE7|EDGE10|EDGE11, 0, 0}, // 194
        {EDGE1|EDGE3|EDGE8|EDGE9, EDGE5|EDGE7|EDGE10|EDGE11, 0, 0}, // 195
        {EDGE4|EDGE5|EDGE8|EDGE10|EDGE11, 0, 0, 0}, // 196
        {EDGE0|EDGE3|EDGE4|EDGE5|EDGE10|EDGE11, 0, 0, 0}, // 197
        {EDGE0|EDGE1|EDGE9, EDGE4|EDGE5|EDGE8|EDGE10|EDGE11, 0, 0}, // 198
        {EDGE1|EDGE3|EDGE4|EDGE5|EDGE9|EDGE10|EDGE11, 0, 0, 0}, // 199
        {EDGE4|EDGE7|EDGE9|EDGE10|EDGE11, 0, 0, 0}, // 200
        {EDGE0|EDGE3|EDGE8, EDGE4|EDGE7|EDGE9|EDGE10|EDGE11, 0, 0}, // 201
        {EDGE0|EDGE1|EDGE4|EDGE7|EDGE10|EDGE11, 0, 0, 0}, // 202
        {EDGE1|EDGE3|EDGE4|EDGE7|EDGE8|EDGE10|EDGE11, 0, 0, 0}, // 203
        {EDGE8|EDGE9|EDGE10|EDGE11, 0, 0, 0}, // 204
        {EDGE0|EDGE3|EDGE9|EDGE10|EDGE11, 0, 0, 0}, // 205
        {EDGE0|EDGE1|EDGE8|EDGE10|EDGE11, 0, 0, 0}, // 206
        {EDGE1|EDGE3|EDGE10|EDGE11, 0, 0, 0}, // 207
        {EDGE2|EDGE3|EDGE5|EDGE7|EDGE10, 0, 0, 0}, // 208
        {EDGE0|EDGE2|EDGE5|EDGE7|EDGE8|EDGE10, 0, 0, 0}, // 209
        {EDGE0|EDGE1|EDGE9, EDGE2|EDGE3|EDGE5|EDGE7|EDGE10, 0, 0}, // 210
        {EDGE1|EDGE2|EDGE5|EDGE7|EDGE8|EDGE9|EDGE10, 0, 0, 0}, // 211
        {EDGE2|EDGE3|EDGE4|EDGE5|EDGE8|EDGE10, 0, 0, 0}, // 212
        {EDGE0|EDGE2|EDGE4|EDGE5|EDGE10, 0, 0, 0}, // 213
        {EDGE0|EDGE1|EDGE9, EDGE2|EDGE3|EDGE4|EDGE5|EDGE8|EDGE10, 0, 0}, // 214
        {EDGE1|EDGE2|EDGE4|EDGE5|EDGE9|EDGE10, 0, 0, 0}, // 215
        {EDGE2|EDGE3|EDGE4|EDGE7|EDGE9|EDGE10, 0, 0, 0}, // 216
        {EDGE0|EDGE2|EDGE4|EDGE7|EDGE8|EDGE9|EDGE10, 0, 0, 0}, // 217
        {EDGE0|EDGE1|EDGE2|EDGE3|EDGE4|EDGE7|EDGE10, 0, 0, 0}, // 218
        {EDGE4|EDGE7|EDGE8, EDGE1|EDGE2|EDGE10, 0, 0}, // 219
        {EDGE2|EDGE3|EDGE8|EDGE9|EDGE10, 0, 0, 0}, // 220
        {EDGE0|EDGE2|EDGE9|EDGE10, 0, 0, 0}, // 221
        {EDGE0|EDGE1|EDGE2|EDGE3|EDGE8|EDGE10, 0, 0, 0}, // 222
        {EDGE1|EDGE2|EDGE10, 0, 0, 0}, // 223
        {EDGE1|EDGE2|EDGE5|EDGE7|EDGE11, 0, 0, 0}, // 224
        {EDGE0|EDGE3|EDGE8, EDGE1|EDGE2|EDGE5|EDGE7|EDGE11, 0, 0}, // 225
        {EDGE0|EDGE2|EDGE5|EDGE7|EDGE9|EDGE11, 0, 0, 0}, // 226
        {EDGE2|EDGE3|EDGE5|EDGE7|EDGE8|EDGE9|EDGE11, 0, 0, 0}, // 227
        {EDGE1|EDGE2|EDGE4|EDGE5|EDGE8|EDGE11, 0, 0, 0}, // 228
        {EDGE0|EDGE1|EDGE2|EDGE3|EDGE4|EDGE5|EDGE11, 0, 0, 0}, // 229
        {EDGE0|EDGE2|EDGE4|EDGE5|EDGE8|EDGE9|EDGE11, 0, 0, 0}, // 230
        {EDGE4|EDGE5|EDGE9, EDGE2|EDGE3|EDGE11, 0, 0}, // 231
        {EDGE1|EDGE2|EDGE4|EDGE7|EDGE9|EDGE11, 0, 0, 0}, // 232
        {EDGE0|EDGE3|EDGE8, EDGE1|EDGE2|EDGE4|EDGE7|EDGE9|EDGE11, 0, 0}, // 233
        {EDGE0|EDGE2|EDGE4|EDGE7|EDGE11, 0, 0, 0}, // 234
        {EDGE2|EDGE3|EDGE4|EDGE7|EDGE8|EDGE11, 0, 0, 0}, // 235
        {EDGE1|EDGE2|EDGE8|EDGE9|EDGE11, 0, 0, 0}, // 236
        {EDGE0|EDGE1|EDGE2|EDGE3|EDGE9|EDGE11, 0, 0, 0}, // 237
        {EDGE0|EDGE2|EDGE8|EDGE11, 0, 0, 0}, // 238
        {EDGE2|EDGE3|EDGE11, 0, 0, 0}, // 239
        {EDGE1|EDGE3|EDGE5|EDGE7, 0, 0, 0}, // 240
        {EDGE0|EDGE1|EDGE5|EDGE7|EDGE8, 0, 0, 0}, // 241
        {EDGE0|EDGE3|EDGE5|EDGE7|EDGE9, 0, 0, 0}, // 242
        {EDGE5|EDGE7|EDGE8|EDGE9, 0, 0, 0}, // 243
        {EDGE1|EDGE3|EDGE4|EDGE5|EDGE8, 0, 0, 0}, // 244
        {EDGE0|EDGE1|EDGE4|EDGE5, 0, 0, 0}, // 245
        {EDGE0|EDGE3|EDGE4|EDGE5|EDGE8|EDGE9, 0, 0, 0}, // 246
        {EDGE4|EDGE5|EDGE9, 0, 0, 0}, // 247
        {EDGE1|EDGE3|EDGE4|EDGE7|EDGE9, 0, 0, 0}, // 248
        {EDGE0|EDGE1|EDGE4|EDGE7|EDGE8|EDGE9, 0, 0, 0}, // 249
        {EDGE0|EDGE3|EDGE4|EDGE7, 0, 0, 0}, // 250
        {EDGE4|EDGE7|EDGE8, 0, 0, 0}, // 251
        {EDGE1|EDGE3|EDGE8|EDGE9, 0, 0, 0}, // 252
        {EDGE0|EDGE1|EDGE9, 0, 0, 0}, // 253
        {EDGE0|EDGE3|EDGE8, 0, 0, 0}, // 254
        {0, 0, 0, 0} // 255
    };
#endregion

    public DualMarching(float surface = 0.5f) 
    {
        this.iso = surface;
    }

    private struct Vertex
    {
        public float x, y, z;

        public static explicit operator Vector3(Vertex v) => new Vector3(v.x, v.y, v.z);
    }

    private int gA(int x, int y, int z) 
    {
        //Debug.Log($"X={x},Y={y},Z={z}. W/H/D={width}");
        return x + width * (y + height * z);
    }

    private int GetCellCode(int cx, int cy, int cz) 
    {
        // determine for each cube corner if it is outside or inside
        int code = 0;
        if (data[gA(cx, cy, cz)] >= iso)
            code |= 1;
        if (data[gA(cx + 1, cy, cz)] >= iso)
            code |= 2;
        if (data[gA(cx, cy + 1, cz)] >= iso)
            code |= 4;
        if (data[gA(cx + 1, cy + 1, cz)] >= iso)
            code |= 8;
        if (data[gA(cx, cy, cz + 1)] >= iso)
            code |= 16;
        if (data[gA(cx + 1, cy, cz + 1)] >= iso)
            code |= 32;
        if (data[gA(cx, cy + 1, cz + 1)] >= iso)
            code |= 64;
        if (data[gA(cx + 1, cy + 1, cz + 1)] >= iso)
            code |= 128;
        return code;
    }

    private void CalculateDualPoint(int cx, int cy, int cz, int pointCode, out Vertex v) 
    {
        // initialize the point with lower voxel coordinates
        v.x = cx;
        v.y = cy;
        v.z = cz;

        // compute the dual point as the mean of the face vertices belonging to the
        // original marching cubes face
        Vertex p;
        p.x = 0;
        p.y = 0;
        p.z = 0;
        int points = 0;

        // sum edge intersection vertices using the point code
        if ((pointCode & EDGE0) != 0)
        {
            p.x += ((float)iso - (float)data[gA(cx, cy, cz)]) / ((float)data[gA(cx + 1, cy, cz)] - (float)data[gA(cx, cy, cz)]);
            points++;
        }

        if ((pointCode & EDGE1) != 0)
        {
            p.x += 1.0f;
            p.z += ((float)iso - (float)data[gA(cx + 1, cy, cz)]) / ((float)data[gA(cx + 1, cy, cz + 1)] - (float)data[gA(cx + 1, cy, cz)]);
            points++;
        }

        if ((pointCode & EDGE2) != 0)
        {
            p.x += ((float)iso - (float)data[gA(cx, cy, cz + 1)]) / ((float)data[gA(cx + 1, cy, cz + 1)] - (float)data[gA(cx, cy, cz + 1)]);
            p.z += 1.0f;
            points++;
        }

        if ((pointCode & EDGE3) != 0)
        {
            p.z += ((float)iso - (float)data[gA(cx, cy, cz)]) / ((float)data[gA(cx, cy, cz + 1)] - (float)data[gA(cx, cy, cz)]);
            points++;
        }

        if ((pointCode & EDGE4) != 0)
        {
            p.x += ((float)iso - (float)data[gA(cx, cy + 1, cz)]) / ((float)data[gA(cx + 1, cy + 1, cz)] - (float)data[gA(cx, cy + 1, cz)]);
            p.y += 1.0f;
            points++;
        }

        if ((pointCode & EDGE5) != 0)
        {
            p.x += 1.0f;
            p.z += ((float)iso - (float)data[gA(cx + 1, cy + 1, cz)]) / ((float)data[gA(cx + 1, cy + 1, cz + 1)] - (float)data[gA(cx + 1, cy + 1, cz)]);
            p.y += 1.0f;
            points++;
        }

        if ((pointCode & EDGE6) != 0)
        {
            p.x += ((float)iso - (float)data[gA(cx, cy + 1, cz + 1)]) / ((float)data[gA(cx + 1, cy + 1, cz + 1)] - (float)data[gA(cx, cy + 1, cz + 1)]);
            p.z += 1.0f;
            p.y += 1.0f;
            points++;
        }

        if ((pointCode & EDGE7) != 0)
        {
            p.z += ((float)iso - (float)data[gA(cx, cy + 1, cz)]) / ((float)data[gA(cx, cy + 1, cz + 1)] - (float)data[gA(cx, cy + 1, cz)]);
            p.y += 1.0f;
            points++;
        }

        if ((pointCode & EDGE8) != 0)
        {
            p.y += ((float)iso - (float)data[gA(cx, cy, cz)]) / ((float)data[gA(cx, cy + 1, cz)] - (float)data[gA(cx, cy, cz)]);
            points++;
        }

        if ((pointCode & EDGE9) != 0)
        {
            p.x += 1.0f;
            p.y += ((float)iso - (float)data[gA(cx + 1, cy, cz)]) / ((float)data[gA(cx + 1, cy + 1, cz)] - (float)data[gA(cx + 1, cy, cz)]);
            points++;
        }

        if ((pointCode & EDGE10) != 0)
        {
            p.x += 1.0f;
            p.y += ((float)iso - (float)data[gA(cx + 1, cy, cz + 1)]) / ((float)data[gA(cx + 1, cy + 1, cz + 1)] - (float)data[gA(cx + 1, cy, cz + 1)]);
            p.z += 1.0f;
            points++;
        }

        if ((pointCode & EDGE11) != 0)
        {
            p.z += 1.0f;
            p.y += ((float)iso - (float)data[gA(cx, cy, cz + 1)]) / ((float)data[gA(cx, cy + 1, cz + 1)] - (float)data[gA(cx, cy, cz + 1)]);
            points++;
        }

        // divide by number of accumulated points
        float invPoints = 1.0f / (float)points;
        p.x *= invPoints;
        p.y *= invPoints;
        p.z *= invPoints;

        // offset point by voxel coordinates
        v.x += p.x;
        v.y += p.y;
        v.z += p.z;
    }

    private int GetDualPointCode(int cx, int cy, int cz, int edge) 
    {
        int cubeCode = GetCellCode(cx, cy, cz);

        // is manifold dual marching cubes desired?
        if (generateManifold)
        {
            // The Manifold Dual Marching Cubes approach from Rephael Wenger as described in
            // chapter 3.3.5 of his book "Isosurfaces: Geometry, Topology, and Algorithms"
            // is implemente here.
            // If a problematic C16 or C19 configuration shares the ambiguous face 
            // with another C16 or C19 configuration we simply invert the cube code
            // before looking up dual points. Doing this for these pairs ensures
            // manifold meshes.
            // But this removes the dualism to marching cubes.

            // check if we have a potentially problematic configuration
            int direction = problematicConfigs[cubeCode];
            // If the direction code is in {0,...,5} we have a C16 or C19 configuration.
            if (direction != 255)
            {
                // We have to check the neighboring cube, which shares the ambiguous
                // face. For this we decode the direction. This could also be done
                // with another lookup table.
                // copy current cube coordinates into an array.
                int[] neighborCoords = { cx, cy, cz };
                // get the dimension of the non-zero coordinate axis
                int component = direction >> 1;
                // get the sign of the direction
                int delta = (direction & 1) == 1 ? 1 : -1;
                // modify the correspong cube coordinate
                neighborCoords[component] += delta;
                // have we left the volume in this direction?
                if (neighborCoords[component] >= 0 && neighborCoords[component] < (dims[component] - 1))
                {
                    // get the cube configuration of the relevant neighbor
                    int neighborCubeCode = GetCellCode(neighborCoords[0], neighborCoords[1], neighborCoords[2]);
                    // Look up the neighbor configuration ambiguous face direction.
                    // If the direction is valid we have a C16 or C19 neighbor.
                    // As C16 and C19 have exactly one ambiguous face this face is
                    // guaranteed to be shared for the pair.
                    if (problematicConfigs[neighborCubeCode] != 255)
                    {
                        // replace the cube configuration with its inverse.
                        cubeCode ^= 0xff;
                    }
                }
            }
        }

        for (int i = 0; i < 4; i++)
        {
            if ((dualPointsList[cubeCode,i] & edge) != 0)
            {
                return dualPointsList[cubeCode,i];
            }
        }
        return 0;
    }

    public void Generate(float[] data, int width, int height, int depth, IList<Vector3> verts, IList<int> indices) 
    {
        this.data = data;
        this.width = width;
        this.height = height;
        this.depth = depth;
        this.dims = new int[] { width, height, depth };

        int reducedX = width - 1;
        int reducedY = height - 1;
        int reducedZ = depth - 1;

        Vertex vertex0;
        Vertex vertex1;
        Vertex vertex2;
        Vertex vertex3;
        int pointCode;

        List<Vertex> vertices = new List<Vertex>();

        // iterate voxels
        for (int z = 0; z < reducedZ; ++z)
            for (int y = 0; y < reducedY; ++y)
                for (int x = 0; x < reducedX; ++x)
                {
                    // construct quad for x edge
                    if (z > 0 && y > 0)
                    {
                        // is edge intersected?
                        bool entering = data[gA(x, y, z)] < iso && data[gA(x + 1, y, z)] >= iso;
                        bool exiting  = data[gA(x, y, z)] >= iso && data[gA(x + 1, y, z)] < iso;
                        if (entering || exiting)
                        {
                            // generate quad
                            pointCode = GetDualPointCode(x, y, z, EDGE0);
                            CalculateDualPoint(x, y, z, pointCode, out vertex0);

                            pointCode = GetDualPointCode(x, y, z - 1, EDGE2);
                            CalculateDualPoint(x, y, z - 1, pointCode, out vertex1);

                            pointCode = GetDualPointCode(x, y - 1, z - 1, EDGE6);
                            CalculateDualPoint(x, y - 1, z - 1, pointCode, out vertex2);

                            pointCode = GetDualPointCode(x, y - 1, z, EDGE4);
                            CalculateDualPoint(x, y - 1, z, pointCode, out vertex3);

                            if (entering)
                            {
                                // 0, 1, 2, 3
                                vertices.Add(vertex0);
                                vertices.Add(vertex1);
                                vertices.Add(vertex3);

                                vertices.Add(vertex1);
                                vertices.Add(vertex2);
                                vertices.Add(vertex3);
                            }
                            else
                            {
                                // 0, 3, 2, 1
                                vertices.Add(vertex0);
                                vertices.Add(vertex3);
                                vertices.Add(vertex1);

                                vertices.Add(vertex3);
                                vertices.Add(vertex2);
                                vertices.Add(vertex1);
                            }
                        }
                    }

                    // construct quad for y edge
                    if (z > 0 && x > 0)
                    {
                        // is edge intersected?
                        bool entering = data[gA(x, y, z)] < iso && data[gA(x, y + 1, z)] >= iso;
                        bool exiting  = data[gA(x, y, z)] >= iso && data[gA(x, y + 1, z)] < iso;
                        if (entering || exiting)
                        {
                            // generate quad
                            pointCode = GetDualPointCode(x, y, z, EDGE8);
                            CalculateDualPoint(x, y, z, pointCode, out vertex0);

                            pointCode = GetDualPointCode(x, y, z - 1, EDGE11);
                            CalculateDualPoint(x, y, z - 1, pointCode, out vertex1);

                            pointCode = GetDualPointCode(x - 1, y, z - 1, EDGE10);
                            CalculateDualPoint(x - 1, y, z - 1, pointCode, out vertex2);

                            pointCode = GetDualPointCode(x - 1, y, z, EDGE9);
                            CalculateDualPoint(x - 1, y, z, pointCode, out vertex3);

                            if (entering)
                            {
                                // 0, 1, 2, 3
                                vertices.Add(vertex0);
                                vertices.Add(vertex3);
                                vertices.Add(vertex1);

                                vertices.Add(vertex1);
                                vertices.Add(vertex3);
                                vertices.Add(vertex2);
                            }
                            else
                            {
                                // 0, 3, 2, 1
                                vertices.Add(vertex0);
                                vertices.Add(vertex1);
                                vertices.Add(vertex3);

                                vertices.Add(vertex3);
                                vertices.Add(vertex1);
                                vertices.Add(vertex2);
                            }
                        }
                    }

                    // construct quad for z edge
                    if (x > 0 && y > 0)
                    {
                        // is edge intersected?
                        bool entering = data[gA(x, y, z)] < iso && data[gA(x, y, z + 1)] >= iso;
                        bool exiting  = data[gA(x, y, z)] >= iso && data[gA(x, y, z + 1)] < iso;
                        if (entering || exiting)
                        {
                            // generate quad
                            pointCode = GetDualPointCode(x, y, z, EDGE3);
                            CalculateDualPoint(x, y, z, pointCode, out vertex0);

                            pointCode = GetDualPointCode(x - 1, y, z, EDGE1);
                            CalculateDualPoint(x - 1, y, z, pointCode, out vertex1);

                            pointCode = GetDualPointCode(x - 1, y - 1, z, EDGE5);
                            CalculateDualPoint(x - 1, y - 1, z, pointCode, out vertex2);

                            pointCode = GetDualPointCode(x, y - 1, z, EDGE7);
                            CalculateDualPoint(x, y - 1, z, pointCode, out vertex3);

                            if (entering)
                            {
                                // 0, 1, 2, 3
                                // 0 - 3
                                // |   |
                                // 1 - 2
                                vertices.Add(vertex0);
                                vertices.Add(vertex3);
                                vertices.Add(vertex1);

                                vertices.Add(vertex1);
                                vertices.Add(vertex3);
                                vertices.Add(vertex2);
                            }
                            else
                            {
                                // 0, 3, 2, 1
                                // 0 - 1
                                // |   |
                                // 3 - 2
                                vertices.Add(vertex0);
                                vertices.Add(vertex1);
                                vertices.Add(vertex3);

                                vertices.Add(vertex3);
                                vertices.Add(vertex1);
                                vertices.Add(vertex2);
                            }
                        }
                    }
                }

        // correction factor
        float CF = 1f;
        foreach (Vertex vert in vertices) 
        {
            verts.Add(new Vector3(CF * vert.x, CF * vert.y, CF * vert.z));
        }

        for (int i = 0; i < vertices.Count / 6; i++) 
        {
            indices.Add((i * 6) + 0);
            indices.Add((i * 6) + 1);
            indices.Add((i * 6) + 2);

            indices.Add((i * 6) + 3);
            indices.Add((i * 6) + 4);
            indices.Add((i * 6) + 5);
        }

        // generate triangle soup quads
        //size_t const numQuads = vertices.size() / 4;
        //quads.reserve(numQuads);
        //for (size_t i = 0; i < numQuads; ++i)
        //{
        //    quads.emplace_back(i * 4, i * 4 + 1, i * 4 + 2, i * 4 + 3);
        //}

    }
}
