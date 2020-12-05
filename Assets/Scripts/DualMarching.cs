#define PROFILE

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using System.Runtime.CompilerServices;

/// <summary>
/// Basic idea of DMC summarized below:
/// https://wordsandbuttons.online/interactive_explanation_of_marching_cubes_and_dual_contouring.html
/// 
/// This code was translated from a C++ implementation here:
/// https://github.com/dominikwodniok/dualmc
/// 
/// It was then cleaned up and modified to work a bit better with C# and Unity.
/// 
/// </summary>
public class DualMarching
{
    //private float[] data;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int gA(int x, int y, int z)
    {
        //Debug.Log($"X={x},Y={y},Z={z}. W/H/D={width}");
#if PROFILE
        //Profiler.BeginSample("gA");
#endif
        int toReturn = x + width * (y + height * z);
#if PROFILE
        //Profiler.EndSample();
#endif
        return toReturn;
    }

    private int[] cellCodes;

    private void ComputeCellCodes(float[] data)
    {
        // Lazy initialization of the array: only if it needs resizing
        if (cellCodes == null || (cellCodes.Length != (width * height * depth)))
        {
            cellCodes = new int[width * height * depth];
        }

        // determine for each cube corner if it is outside or inside
        for (int cz = 0; cz < depth - 1; cz++)
        {
            for (int cy = 0; cy < height - 1; cy++)
            {
                for (int cx = 0; cx < width - 1; cx++)
                {
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

                    cellCodes[gA(cx, cy, cz)] = code;
                }
            }
        }
    }

    private int GetCellCode(float[] data, int cx, int cy, int cz)
    {


        //int code = 0;
        //if (data[gA(cx, cy, cz)] >= iso)
        //    code |= 1;
        //if (data[gA(cx + 1, cy, cz)] >= iso)
        //    code |= 2;
        //if (data[gA(cx, cy + 1, cz)] >= iso)
        //    code |= 4;
        //if (data[gA(cx + 1, cy + 1, cz)] >= iso)
        //    code |= 8;
        //if (data[gA(cx, cy, cz + 1)] >= iso)
        //    code |= 16;
        //if (data[gA(cx + 1, cy, cz + 1)] >= iso)
        //    code |= 32;
        //if (data[gA(cx, cy + 1, cz + 1)] >= iso)
        //    code |= 64;
        //if (data[gA(cx + 1, cy + 1, cz + 1)] >= iso)
        //    code |= 128;

        return cellCodes[gA(cx, cy, cz)];

        //if (data[(cx + 0) + width * ((cy + 0) + height * (cz + 0))] >= iso)
        //    code |= 1;
        //if (data[(cx + 1) + width * ((cy + 0) + height * (cz + 0))] >= iso)
        //    code |= 2;
        //if (data[(cx + 0) + width * ((cy + 1) + height * (cz + 0))] >= iso)
        //    code |= 4;
        //if (data[(cx + 1) + width * ((cy + 1) + height * (cz + 0))] >= iso)
        //    code |= 8;
        //if (data[(cx + 0) + width * ((cy + 0) + height * (cz + 1))] >= iso)
        //    code |= 16;
        //if (data[(cx + 1) + width * ((cy + 0) + height * (cz + 1))] >= iso)
        //    code |= 32;
        //if (data[(cx + 0) + width * ((cy + 1) + height * (cz + 1))] >= iso)
        //    code |= 64;
        //if (data[(cx + 1) + width * ((cy + 1) + height * (cz + 1))] >= iso)
        //    code |= 128;


        //return code;
    }

    private Vector3 CalculateDualPoint(float[] data, int cx, int cy, int cz, int pointCode)
    {
#if PROFILE
        Profiler.BeginSample("CalculateDualPoint");
#endif
        Vector3 v;
        // initialize the point with lower voxel coordinates
        v.x = cx;
        v.y = cy;
        v.z = cz;

        // compute the dual point as the mean of the face vertices belonging to the
        // original marching cubes face
        Vector3 p;
        p.x = 0;
        p.y = 0;
        p.z = 0;
        int points = 0;

        // Prefetch values that will likely be used
        // This optimization works best when the mesh is expected to be dense.
        // If the mesh is expected to be sparse, it may be best to conditionally prefetch 
        // (i.e., check if the value will be used by checking edge values)
        float x0y0z0 = data[gA(cx, cy, cz)];
        float x1y0z0 = data[gA(cx + 1, cy, cz)];
        float x0y1z0 = data[gA(cx, cy + 1, cz)];
        float x1y1z0 = data[gA(cx + 1, cy + 1, cz)];
        float x0y0z1 = data[gA(cx, cy, cz + 1)];
        float x1y0z1 = data[gA(cx + 1, cy, cz + 1)];
        float x0y1z1 = data[gA(cx, cy + 1, cz + 1)];
        float x1y1z1 = data[gA(cx + 1, cy + 1, cz + 1)];

        // sum edge intersection vertices using the point code
        if ((pointCode & EDGE0) != 0)
        {
            //p.x += ((float)iso - (float)data[gA(cx, cy, cz)]) / ((float)data[gA(cx + 1, cy, cz)] - (float)data[gA(cx, cy, cz)]);
            p.x += (iso - x0y0z0) / (x1y0z0 - x0y0z0);
            points++;
        }

        if ((pointCode & EDGE1) != 0)
        {
            p.x += 1.0f;
            //p.z += ((float)iso - (float)data[gA(cx + 1, cy, cz)]) / ((float)data[gA(cx + 1, cy, cz + 1)] - (float)data[gA(cx + 1, cy, cz)]);
            p.z += (iso - x1y0z0) / (x1y0z1 - x1y0z0);
            points++;
        }

        if ((pointCode & EDGE2) != 0)
        {
            //p.x += ((float)iso - (float)data[gA(cx, cy, cz + 1)]) / ((float)data[gA(cx + 1, cy, cz + 1)] - (float)data[gA(cx, cy, cz + 1)]);
            p.x += (iso - x0y0z1) / (x1y0z1 - x0y0z1);
            p.z += 1.0f;
            points++;
        }

        if ((pointCode & EDGE3) != 0)
        {
            //p.z += ((float)iso - (float)data[gA(cx, cy, cz)]) / ((float)data[gA(cx, cy, cz + 1)] - (float)data[gA(cx, cy, cz)]);
            p.z += (iso - x0y0z0) / (x0y0z1 - x0y0z0);
            points++;
        }

        if ((pointCode & EDGE4) != 0)
        {
            //p.x += ((float)iso - (float)data[gA(cx, cy + 1, cz)]) / ((float)data[gA(cx + 1, cy + 1, cz)] - (float)data[gA(cx, cy + 1, cz)]);
            p.x += (iso - x0y1z0) / (x1y1z0 - x0y1z0);
            p.y += 1.0f;
            points++;
        }

        if ((pointCode & EDGE5) != 0)
        {
            p.x += 1.0f;
            //p.z += ((float)iso - (float)data[gA(cx + 1, cy + 1, cz)]) / ((float)data[gA(cx + 1, cy + 1, cz + 1)] - (float)data[gA(cx + 1, cy + 1, cz)]);
            p.z += (iso - x1y1z0) / (x1y1z1 - x1y1z0);
            p.y += 1.0f;
            points++;
        }

        if ((pointCode & EDGE6) != 0)
        {
            //p.x += ((float)iso - (float)data[gA(cx, cy + 1, cz + 1)]) / ((float)data[gA(cx + 1, cy + 1, cz + 1)] - (float)data[gA(cx, cy + 1, cz + 1)]);
            p.x += (iso - x0y1z1) / (x1y1z1 - x0y1z1);
            p.z += 1.0f;
            p.y += 1.0f;
            points++;
        }

        if ((pointCode & EDGE7) != 0)
        {
            //p.z += ((float)iso - (float)data[gA(cx, cy + 1, cz)]) / ((float)data[gA(cx, cy + 1, cz + 1)] - (float)data[gA(cx, cy + 1, cz)]);
            p.z += (iso - x0y1z0) / (x0y1z1 - x0y1z0);
            p.y += 1.0f;
            points++;
        }

        if ((pointCode & EDGE8) != 0)
        {
            //p.y += ((float)iso - (float)data[gA(cx, cy, cz)]) / ((float)data[gA(cx, cy + 1, cz)] - (float)data[gA(cx, cy, cz)]);
            p.y += (iso - x0y0z0) / (x0y1z0 - x0y0z0);
            points++;
        }

        if ((pointCode & EDGE9) != 0)
        {
            p.x += 1.0f;
            //p.y += ((float)iso - (float)data[gA(cx + 1, cy, cz)]) / ((float)data[gA(cx + 1, cy + 1, cz)] - (float)data[gA(cx + 1, cy, cz)]);
            p.y += (iso - x1y0z0) / (x1y1z0 - x1y0z0);
            points++;
        }

        if ((pointCode & EDGE10) != 0)
        {
            p.x += 1.0f;
            //p.y += ((float)iso - (float)data[gA(cx + 1, cy, cz + 1)]) / ((float)data[gA(cx + 1, cy + 1, cz + 1)] - (float)data[gA(cx + 1, cy, cz + 1)]);
            p.y += (iso - x1y0z1) / (x1y1z1 - x1y0z1);
            p.z += 1.0f;
            points++;
        }

        if ((pointCode & EDGE11) != 0)
        {
            p.z += 1.0f;
            //p.y += ((float)iso - (float)data[gA(cx, cy, cz + 1)]) / ((float)data[gA(cx, cy + 1, cz + 1)] - (float)data[gA(cx, cy, cz + 1)]);
            p.y += (iso - x0y0z1) / (x0y1z1 - x0y0z1);
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
        
#if PROFILE
        Profiler.EndSample();
#endif
        return v;
    }

    private static int _direction;
    private static int[] _neighborCoords = new int[3];
    private static int _component;
    private static int _delta;

    private int GetDualPointCode(float[] data, int cx, int cy, int cz, int edge)
    {
#if PROFILE
        Profiler.BeginSample("GetDualPointCode");
#endif
        int cubeCode = GetCellCode(data, cx, cy, cz);

        // is manifold dual marching cubes desired?
        if (generateManifold)
        {
            // The Manifold Dual Marching Cubes approach from Rephael Wenger as described in
            // chapter 3.3.5 of his book "Isosurfaces: Geometry, Topology, and Algorithms"
            // is implemented here.
            // If a problematic C16 or C19 configuration shares the ambiguous face 
            // with another C16 or C19 configuration we simply invert the cube code
            // before looking up dual points. Doing this for these pairs ensures
            // manifold meshes.
            // But this removes the dualism to marching cubes.

            // check if we have a potentially problematic configuration
            _direction = problematicConfigs[cubeCode];
            // If the direction code is in {0,...,5} we have a C16 or C19 configuration.
            if (_direction != 255)
            {
                // We have to check the neighboring cube, which shares the ambiguous
                // face. For this we decode the direction. This could also be done
                // with another lookup table.
                // copy current cube coordinates into an array.
                //_neighborCoords = { cx, cy, cz };
                _neighborCoords[0] = cx;
                _neighborCoords[1] = cy;
                _neighborCoords[2] = cz;
                // get the dimension of the non-zero coordinate axis
                _component = _direction >> 1;
                // get the sign of the direction
                _delta = (_direction & 1) == 1 ? 1 : -1;
                // modify the correspong cube coordinate
                _neighborCoords[_component] += _delta;
                // have we left the volume in this direction?
                if (_neighborCoords[_component] >= 0 && _neighborCoords[_component] < (dims[_component] - 1))
                {
                    // get the cube configuration of the relevant neighbor
                    int neighborCubeCode = GetCellCode(data, _neighborCoords[0], _neighborCoords[1], _neighborCoords[2]);
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
            if ((dualPointsList[cubeCode, i] & edge) != 0)
            {
                Profiler.EndSample();
                return dualPointsList[cubeCode, i];
            }
        }
#if PROFILE
        Profiler.EndSample();
#endif
        return 0;
    }

    private static Matrix4x4 _mat = new Matrix4x4();

    /// <summary>
    /// Given four vertices, with A, B, C in CCW order, returns true if D is
    /// contained within the circumcircle formed by A, B, C. 
    /// 
    /// This condition is 1:1 with whether we should prefer to triangulate the
    /// quadrangle ABCD WITHOUT the triangle ABC. In other words, if this returns
    /// true, we should flip an edge to avoid using triangle ABC.
    /// 
    /// </summary>
    private bool ShouldFlip(Vector3 A, Vector3 B, Vector3 C, Vector3 D)
    {
#if PROFILE
        Profiler.BeginSample("ShouldFlip");
#endif
        // A - D
        // | \ |
        // B - C
        // Need to check if the angles B + D > 180.
        // Thus we check ((BA) dot (BC)) + ((DA) dot (DC)).
        Vector3 BA = A - B;
        Vector3 BC = C - B;

        Vector3 DA = A - D;
        Vector3 DC = C - D;

        float test = Vector3.Angle(BA, BC) + Vector3.Angle(DA, DC);
#if PROFILE
        Profiler.EndSample();
#endif
        return test >= 180.0f;

    }

    /// <summary>
    /// Helper function for generating a quad given four vertices that are going
    /// to be part of the mesh. 
    /// 
    /// TODO finish explaining the details of this
    /// </summary>
    /// <param name="A">A.</param>
    /// <param name="B">B.</param>
    /// <param name="C">C.</param>
    /// <param name="D">D.</param>
    /// <param name="entering">If <c>true</c>, we are entering this quad. Represents orientation.</param>
    /// <param name="verts">Vertices list.</param>
    private void AddQuad(Vector3 A, Vector3 B, Vector3 C, Vector3 D, bool entering, IList<Vector3> verts)
    {
#if PROFILE
        Profiler.BeginSample("AddQuad");
#endif
        if (entering)
        {
            // Testing if this triangulation is NOT ideal:
            // 0 - 3
            // | / |
            // 1 - 2
            if (ShouldFlip(B, A, D, C))
            {
                // Not ideal, so use this one:
                // 0 - 3
                // | \ |
                // 1 - 2
                verts.Add(A);
                verts.Add(D);
                verts.Add(C);

                verts.Add(B);
                verts.Add(A);
                verts.Add(C);
            }
            else
            {
                // 0, 1, 2, 3
                // We're fine, use this triangulation:
                // 0 - 3
                // | / |
                // 1 - 2
                verts.Add(A);
                verts.Add(D);
                verts.Add(B);

                verts.Add(B);
                verts.Add(D);
                verts.Add(C);
            }
        }
        else
        {
            // Testing if this triangulation is NOT ideal:
            // (Note that 1 and 3 are swapped from above, since we are exiting)
            // 0 - 1
            // | / |
            // 3 - 2
            if (ShouldFlip(D, A, B, C))
            {
                // NOT fine, so flip triangulation:
                // 0 - 1
                // | \ |
                // 3 - 2
                verts.Add(A);
                verts.Add(B);
                verts.Add(C);

                verts.Add(D);
                verts.Add(A);
                verts.Add(C);
            }
            else
            {
                // 0, 3, 2, 1
                // We're fine, use this triangulation:
                // 0 - 1
                // | / |
                // 3 - 2
                verts.Add(A);
                verts.Add(B);
                verts.Add(D);

                verts.Add(D);
                verts.Add(B);
                verts.Add(C);
            }
        }
#if PROFILE
        Profiler.EndSample();
#endif
    }

    public void Generate(float[] data, int width, int height, int depth, IList<Vector3> verts, IList<int> indices)
    {
        Profiler.BeginSample("Dual Marching Cubes Generate");
        //this.data = data;
        this.width = width;
        this.height = height;
        this.depth = depth;
        this.dims = new int[] { width, height, depth };

        Profiler.BeginSample("Compute Cell Codes");
        ComputeCellCodes(data);
        Profiler.EndSample();

        int reducedX = width - 2;
        int reducedY = height - 2;
        int reducedZ = depth - 2;

        Vector3 vertex0;
        Vector3 vertex1;
        Vector3 vertex2;
        Vector3 vertex3;
        int pointCode;

        float center;
        float offset;

        List<Vector3> vertices = new List<Vector3>();
        
        Profiler.BeginSample("Triple for loops");
        for (int z = 1; z < reducedZ; z++)
        {
            for (int y = 1; y < reducedY; y++)
            {
                for (int x = 0; x < reducedX; x++)
                {
                    center = data[gA(x, y, z)];
                    // construct quad for x edge
                    // if (z > 0 && y > 0)
                    // is edge intersected?
                    offset = data[gA(x + 1, y, z)];
                    bool entering = center < iso && offset >= iso;
                    bool exiting = center >= iso && offset < iso;
                    if (entering || exiting)
                    {
                        // generate quad
                        pointCode = GetDualPointCode(data, x, y, z, EDGE0);
                        vertex0 = CalculateDualPoint(data, x, y, z, pointCode);

                        pointCode = GetDualPointCode(data, x, y, z - 1, EDGE2);
                        vertex1 = CalculateDualPoint(data, x, y, z - 1, pointCode);

                        pointCode = GetDualPointCode(data, x, y - 1, z - 1, EDGE6);
                        vertex2 = CalculateDualPoint(data, x, y - 1, z - 1, pointCode);

                        pointCode = GetDualPointCode(data, x, y - 1, z, EDGE4);
                        vertex3 = CalculateDualPoint(data, x, y - 1, z, pointCode);

                        AddQuad(vertex0, vertex1, vertex2, vertex3, exiting, verts); // Note exiting, not entering 
                    }
                }
            }
        }
        
        for (int z = 1; z < reducedZ; z++)
        {
            for (int y = 0; y < reducedY; y++)
            {
                for (int x = 1; x < reducedX; x++)
                {
                    center = data[gA(x, y, z)];
                    // construct quad for y edge
                    //if (z > 0 && x > 0)
                    // is edge intersected?
                    offset = data[gA(x, y + 1, z)];
                    bool entering = center < iso && offset >= iso;
                    bool exiting = center >= iso && offset < iso;
                    if (entering || exiting)
                    {
                        // generate quad
                        pointCode = GetDualPointCode(data, x, y, z, EDGE8);
                        vertex0 = CalculateDualPoint(data, x, y, z, pointCode);

                        pointCode = GetDualPointCode(data, x, y, z - 1, EDGE11);
                        vertex1 = CalculateDualPoint(data, x, y, z - 1, pointCode);

                        pointCode = GetDualPointCode(data, x - 1, y, z - 1, EDGE10);
                        vertex2 = CalculateDualPoint(data, x - 1, y, z - 1, pointCode);

                        pointCode = GetDualPointCode(data, x - 1, y, z, EDGE9);
                        vertex3 = CalculateDualPoint(data, x - 1, y, z, pointCode);

                        AddQuad(vertex0, vertex1, vertex2, vertex3, entering, verts); 
                    }
                }
            }
        }

        for (int z = 0; z < reducedZ; z++)
        {
            for (int y = 1; y < reducedY; y++)
            {
                for (int x = 1; x < reducedX; x++)
                {
                    center = data[gA(x, y, z)];
                    // construct quad for z edge
                    //if (x > 0 && y > 0)
                    // is edge intersected?
                    offset = data[gA(x, y, z + 1)];
                    bool entering = center < iso && offset >= iso;
                    bool exiting = center >= iso && offset < iso;
                    if (entering || exiting)
                    {
                        // generate quad
                        pointCode = GetDualPointCode(data, x, y, z, EDGE3);
                        vertex0 = CalculateDualPoint(data, x, y, z, pointCode);

                        pointCode = GetDualPointCode(data, x - 1, y, z, EDGE1);
                        vertex1 = CalculateDualPoint(data, x - 1, y, z, pointCode);

                        pointCode = GetDualPointCode(data, x - 1, y - 1, z, EDGE5);
                        vertex2 = CalculateDualPoint(data, x - 1, y - 1, z, pointCode);

                        pointCode = GetDualPointCode(data, x, y - 1, z, EDGE7);
                        vertex3 = CalculateDualPoint(data, x, y - 1, z, pointCode);

                        AddQuad(vertex0, vertex1, vertex2, vertex3, entering, verts);
                    }
                }
            }
        }

        //// iterate voxels
        //for (int z = 0; z < reducedZ; ++z)
        //{
        //    for (int y = 0; y < reducedY; ++y)
        //    {
        //        for (int x = 0; x < reducedX; ++x)
        //        {
        //            //Profiler.BeginSample("Inner loop");
        //            center = data[gA(x, y, z)];
        //            // construct quad for x edge
        //            if (z > 0 && y > 0)
        //            {
        //                // is edge intersected?
        //                offset = data[gA(x + 1, y, z)];
        //                bool entering = center < iso && offset >= iso;
        //                bool exiting = center >= iso && offset < iso;
        //                if (entering || exiting)
        //                {
        //                    // generate quad
        //                    pointCode = GetDualPointCode(data, x, y, z, EDGE0);
        //                    vertex0 = CalculateDualPoint(data, x, y, z, pointCode);

        //                    pointCode = GetDualPointCode(data, x, y, z - 1, EDGE2);
        //                    vertex1 = CalculateDualPoint(data, x, y, z - 1, pointCode);

        //                    pointCode = GetDualPointCode(data, x, y - 1, z - 1, EDGE6);
        //                    vertex2 = CalculateDualPoint(data, x, y - 1, z - 1, pointCode);

        //                    pointCode = GetDualPointCode(data, x, y - 1, z, EDGE4);
        //                    vertex3 = CalculateDualPoint(data, x, y - 1, z, pointCode);

        //                    AddQuad(vertex0, vertex1, vertex2, vertex3, exiting, verts); // Note exiting, not entering 
        //                }
        //            }

        //            // construct quad for y edge
        //            if (z > 0 && x > 0)
        //            {
        //                // is edge intersected?
        //                offset = data[gA(x, y + 1, z)];
        //                bool entering = center < iso && offset >= iso;
        //                bool exiting = center >= iso && offset < iso;
        //                if (entering || exiting)
        //                {
        //                    // generate quad
        //                    pointCode = GetDualPointCode(data, x, y, z, EDGE8);
        //                    vertex0 = CalculateDualPoint(data, x, y, z, pointCode);

        //                    pointCode = GetDualPointCode(data, x, y, z - 1, EDGE11);
        //                    vertex1 = CalculateDualPoint(data, x, y, z - 1, pointCode);

        //                    pointCode = GetDualPointCode(data, x - 1, y, z - 1, EDGE10);
        //                    vertex2 = CalculateDualPoint(data, x - 1, y, z - 1, pointCode);

        //                    pointCode = GetDualPointCode(data, x - 1, y, z, EDGE9);
        //                    vertex3 = CalculateDualPoint(data, x - 1, y, z, pointCode);

        //                    AddQuad(vertex0, vertex1, vertex2, vertex3, entering, verts); 
        //                }
        //            }

        //            // construct quad for z edge
        //            if (x > 0 && y > 0)
        //            {
        //                // is edge intersected?
        //                offset = data[gA(x, y, z + 1)];
        //                bool entering = center < iso && offset >= iso;
        //                bool exiting = center >= iso && offset < iso;
        //                if (entering || exiting)
        //                {
        //                    // generate quad
        //                    pointCode = GetDualPointCode(data, x, y, z, EDGE3);
        //                    vertex0 = CalculateDualPoint(data, x, y, z, pointCode);

        //                    pointCode = GetDualPointCode(data, x - 1, y, z, EDGE1);
        //                    vertex1 = CalculateDualPoint(data, x - 1, y, z, pointCode);

        //                    pointCode = GetDualPointCode(data, x - 1, y - 1, z, EDGE5);
        //                    vertex2 = CalculateDualPoint(data, x - 1, y - 1, z, pointCode);

        //                    pointCode = GetDualPointCode(data, x, y - 1, z, EDGE7);
        //                    vertex3 = CalculateDualPoint(data, x, y - 1, z, pointCode);

        //                    AddQuad(vertex0, vertex1, vertex2, vertex3, entering, verts);
        //                }
        //            }
        //            //Profiler.EndSample();
        //        }
        //    }
        //}
        Profiler.EndSample();

        Profiler.BeginSample("Triangle generation");
        for (int i = 0; i < verts.Count / 6; i++)
        {
            indices.Add((i * 6) + 0);
            indices.Add((i * 6) + 1);
            indices.Add((i * 6) + 2);

            indices.Add((i * 6) + 3);
            indices.Add((i * 6) + 4);
            indices.Add((i * 6) + 5);
        }
        Profiler.EndSample();

        //Debug.Log($"Generated {verts.Count} vertices and {indices.Count / 3} triangles.");

        // TODO find a better way to remove skinnies


        //int countSkinny = 0;
        //for (int i = 0; i < indices.Count - 2; i += 3) 
        //{
        //    Vector3 A = verts[indices[i + 0]];
        //    Vector3 B = verts[indices[i + 1]];
        //    Vector3 C = verts[indices[i + 2]];

        //    Vector3 BA = A - B;
        //    Vector3 CB = B - C;
        //    Vector3 CA = A - C;

        //    float AngleA = Vector3.Angle(BA, CA);
        //    float AngleB = Vector3.Angle(BA, CB);
        //    float AngleC = Vector3.Angle(CB, CA);

        //    float minAngle = Mathf.Min(AngleA, AngleB, AngleC);
        //    if (minAngle >= 10.0f) 
        //    {
        //        continue;
        //    }

        //    // A is smallest angle. So pick furthest other point to draw line..
        //    if (Mathf.Approximately(minAngle, AngleA))
        //    {
        //        // C furthest, so merge B into AC
        //        if (CA.magnitude > BA.magnitude) 
        //        {
        //            // Find projection point & rewrite point B to be this point.
        //            Vector3 Bp = B + Vector3.Project(BA, CA);
        //            verts[indices[i + 1]] = Bp;
        //            continue;
        //        }
        //        else // B furthest, so merge C into BC 
        //        {
        //            Vector3 Cp = C + Vector3.Project(CA, CB);
        //            verts[indices[i + 2]] = Cp;
        //            continue;
        //        }
        //    }
        //    // B is smallest angle.
        //    else if (Mathf.Approximately(minAngle, AngleB)) 
        //    {
        //        // C furthest, so merge A into BC
        //        if (CB.magnitude > BA.magnitude) 
        //        {
        //            Vector3 Ap = A + Vector3.Project(BA, CB);
        //            verts[indices[i + 0]] = Ap;
        //            continue;
        //        }
        //        else // A furthest, so merge C into AC
        //        {
        //            Vector3 Cp = C + Vector3.Project(CB, CA);
        //            verts[indices[i + 2]] = Cp;
        //            continue;
        //        }
        //    }
        //    // C is smallest angle (or a tie, so it's arbitrary)
        //    else 
        //    {
        //        // B furthest, so merge A into BC
        //        if (CB.magnitude > CA.magnitude) 
        //        {
        //            Vector3 Ap = A + Vector3.Project(CA, CB);
        //            verts[indices[i + 0]] = Ap;
        //            continue;
        //        }
        //        else // A furthest, so merge B into AC 
        //        {
        //            Vector3 Bp = B + Vector3.Project(CB, CA);
        //            verts[indices[i + 1]] = Bp;
        //            continue;
        //        }
        //    }

        //    //if (IsSkinny(verts[indices[i]], verts[indices[i + 1]], verts[indices[i + 2]])) 
        //    //{

        //    //    //countSkinny++;
        //    //}
        //}
        //Debug.Log($"Found {countSkinny} skinny triangles, out of {indices.Count / 3} total.");

        Profiler.EndSample();
    }
}
