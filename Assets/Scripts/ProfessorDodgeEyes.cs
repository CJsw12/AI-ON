using System.Collections.Generic;
using UnityEngine;

// Pupil centres measured on the source Happy PNGs and visually reviewed.
// Update these anchors when replacing a portrait; dimensions are source pixels, not Unity import sizes.
public static class ProfessorDodgeEyes
{
    public readonly struct Entry
    {
        public readonly float Width, Height;
        public readonly Vector2 Left, Right;
        public readonly string SourceHash;
        public Entry(float width, float height, Vector2 left, Vector2 right, string hash)
        { Width = width; Height = height; Left = left; Right = right; SourceHash = hash; }
    }

    static readonly Dictionary<string, Entry> Eyes = new Dictionary<string, Entry>
    {
        ["ProfessorBaeJunWooHappyTransparent"] = new Entry(1698f, 926f, new Vector2(797.500f, 203.500f), new Vector2(883.000f, 203.000f), "3f6e035c66a8d1f1eba4ec59e0d55bb6"),
        ["ProfessorBaekSoDamHappyTransparent"] = new Entry(1698f, 926f, new Vector2(834.000f, 90.000f), new Vector2(870.000f, 90.000f), "d242c2e8429df70d783dfd5509f0df77"),
        ["ProfessorChaYuRiHappyTransparent"] = new Entry(1024f, 1536f, new Vector2(442.500f, 225.500f), new Vector2(527.500f, 206.500f), "bfd975d2a01b1e4418ae26336167e793"),
        ["ProfessorChoiHappyTransparent"] = new Entry(1698f, 926f, new Vector2(831.000f, 84.000f), new Vector2(866.000f, 84.000f), "1a678a295585126d3365190f983e5fb9"),
        ["ProfessorEomTaeOHappyTransparent"] = new Entry(1024f, 1536f, new Vector2(404.000f, 166.000f), new Vector2(485.000f, 145.000f), "337d6c4dd16085d199757538dfb56f8c"),
        ["ProfessorGuMathHappyTransparent"] = new Entry(1024f, 1536f, new Vector2(472.000f, 208.000f), new Vector2(546.000f, 184.000f), "556ee80b3cedcc4fcedbf45164621cbf"),
        ["ProfessorHaEunByeolHappyTransparent"] = new Entry(1698f, 926f, new Vector2(803.500f, 187.500f), new Vector2(883.000f, 186.000f), "28b6870f0d500848279a17209853a11d"),
        ["ProfessorHaEunChaeHappyTransparent"] = new Entry(1024f, 1536f, new Vector2(436.500f, 267.500f), new Vector2(527.500f, 239.500f), "077c1ce059d608c45e17cb4f03a5dd1e"),
        ["ProfessorHanHappyTransparent"] = new Entry(1698f, 926f, new Vector2(831.000f, 92.000f), new Vector2(866.000f, 92.000f), "101e70bed981efba5c71defeadfd8e9f"),
        ["ProfessorImHappyTransparent"] = new Entry(1698f, 926f, new Vector2(836.000f, 192.000f), new Vector2(897.000f, 171.000f), "6536499cbe84043fd591c2accb213bf3"),
        ["ProfessorJinSeoHanHappyTransparent"] = new Entry(1698f, 926f, new Vector2(809.000f, 205.000f), new Vector2(891.000f, 204.000f), "8903274c30edb54b3c854052d99f92e6"),
        ["ProfessorJoMinHyukHappyTransparent"] = new Entry(1698f, 926f, new Vector2(806.000f, 189.000f), new Vector2(883.500f, 187.500f), "d96b83702cb71952bbdb875f6d031c85"),
        ["ProfessorJungChemHappyTransparent"] = new Entry(1024f, 1536f, new Vector2(451.000f, 226.000f), new Vector2(548.500f, 213.500f), "52521319131993ee20fef0a4204f55eb"),
        ["ProfessorJungDoYoonHappyTransparent"] = new Entry(1698f, 926f, new Vector2(774.500f, 313.500f), new Vector2(909.500f, 312.500f), "2ea98e30f79a391507f18b947d4df79c"),
        ["ProfessorKangHappyTransparent"] = new Entry(1697f, 927f, new Vector2(829.000f, 96.000f), new Vector2(856.000f, 96.000f), "b851d42ebed54e53d9104e035594a661"),
        ["ProfessorKimHappyTransparent"] = new Entry(1498f, 1050f, new Vector2(700.442f, 194.363f), new Vector2(795.028f, 193.209f), "4f9d3813d4b95afca8d75ad53f527ad0"),
        ["ProfessorKwonDoKyungHappyTransparent"] = new Entry(1698f, 926f, new Vector2(800.000f, 199.000f), new Vector2(884.000f, 198.000f), "4153078f144a8e27cb9739a48040c515"),
        ["ProfessorLeeHappyTransparent"] = new Entry(1698f, 926f, new Vector2(823.000f, 167.000f), new Vector2(890.000f, 163.000f), "919a7f883c581f0f1339a75496a3e0f2"),
        ["ProfessorLimBioHappyTransparent"] = new Entry(1024f, 1536f, new Vector2(518.500f, 261.500f), new Vector2(607.500f, 270.500f), "5d788dbe056f1df1a8640df1db70e30f"),
        ["ProfessorMoonBioHappyTransparent"] = new Entry(1024f, 1536f, new Vector2(478.000f, 239.000f), new Vector2(573.500f, 214.500f), "e427a3930ab7822361583defdddd0b4d"),
        ["ProfessorNamgungHyunHappyTransparent"] = new Entry(1024f, 1536f, new Vector2(418.535f, 202.047f), new Vector2(511.521f, 178.800f), "08f420448ccc2b9660ef159ca5876010"),
        ["ProfessorNaYunSeulHappyTransparent"] = new Entry(1698f, 926f, new Vector2(807.500f, 200.500f), new Vector2(885.500f, 198.500f), "9d098fe3d09923aa8d7ea79f4ce9490b"),
        ["ProfessorOhChemHappyTransparent"] = new Entry(1086f, 1448f, new Vector2(477.000f, 239.000f), new Vector2(581.500f, 214.500f), "ac8a04a1548ee7e3f2fc1afd7735e078"),
        ["ProfessorOhHappyTransparent"] = new Entry(1698f, 926f, new Vector2(814.500f, 147.500f), new Vector2(881.000f, 140.000f), "dc9f4048148d95f4dc7959c402b8c880"),
        ["ProfessorOhHaRamHappyTransparent"] = new Entry(1698f, 926f, new Vector2(783.500f, 260.500f), new Vector2(894.000f, 257.000f), "e0ab5679a38ad146c9c73ac39a9ef8bd"),
        ["ProfessorOhSeRinHappyTransparent"] = new Entry(1698f, 926f, new Vector2(803.500f, 190.500f), new Vector2(887.000f, 190.000f), "464115d82dacb535bf6467ea3388a65d"),
        ["ProfessorParkHappyTransparent"] = new Entry(1699f, 926f, new Vector2(823.000f, 84.000f), new Vector2(858.000f, 83.000f), "52d4c100c0b58b527779978b88dfc9ba"),
        ["ProfessorRyuBoRaHappyTransparent"] = new Entry(1024f, 1536f, new Vector2(482.842f, 192.270f), new Vector2(571.205f, 165.312f), "97749f48141e3743b831654c7dd1af1d"),
        ["ProfessorSeoHappyTransparent"] = new Entry(1698f, 926f, new Vector2(834.000f, 81.000f), new Vector2(868.000f, 82.000f), "3447f0b3fa4154f2ac8374503dc390dd"),
        ["ProfessorSeolMathHappyTransparent"] = new Entry(1024f, 1536f, new Vector2(462.000f, 192.000f), new Vector2(552.000f, 174.000f), "f583ad94709533f875be545237f22e6e"),
        ["ProfessorShinDoYoonHappyTransparent"] = new Entry(1024f, 1536f, new Vector2(505.000f, 226.000f), new Vector2(596.500f, 237.500f), "4fda0a314821b9d5f686e69831c64fe0"),
        ["ProfessorSongDaOnHappyTransparent"] = new Entry(1698f, 926f, new Vector2(804.000f, 197.000f), new Vector2(885.500f, 197.500f), "2343e3b975e2e6991f0918207e7616c8"),
        ["ProfessorTakGiHunHappyTransparent"] = new Entry(1698f, 926f, new Vector2(791.674f, 210.953f), new Vector2(869.814f, 209.558f), "ac77087652e16b9534de24ed16047828"),
        ["ProfessorYoonHappyTransparent"] = new Entry(1698f, 926f, new Vector2(830.000f, 89.000f), new Vector2(862.000f, 91.000f), "eaef434653f9aadfac963f5bb97376b1"),
        ["ProfessorShinHappyTransparent"] = new Entry(1699f, 926f, new Vector2(798.5f, 279.5f), new Vector2(903.5f, 281.5f), "f532a066c336e494c6b3ac13141a32f8"),
        ["ProfessorHaHappyTransparent"] = new Entry(1698f, 926f, new Vector2(803.0f, 269.0f), new Vector2(899.5f, 265.5f), "167725dad7805340b9cc620558c4389e"),
        ["ProfessorChaHappyTransparent"] = new Entry(1699f, 926f, new Vector2(814.0f, 263.0f), new Vector2(901.5f, 263.5f), "cbbcc2c8a7f31c3d9fc0091dfbf1b099"),
    };

    public static bool TryGet(string picture, out Entry entry) => Eyes.TryGetValue(picture, out entry);
}
