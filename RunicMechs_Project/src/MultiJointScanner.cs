using System;
using System.Collections;
using RunicGems;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public static partial class MultiJointScanner
    {
        private const string Joint4x4 = "0110100110010110";
        private const string JointOval8x8 = "0011110001000010100000011000000110000001100000010100001000111100";
        private const string JointRing8x8 = "0011110001000010100110011010010110100101100110010100001000111100";
        private const string JointArm8x8 = "0011110001000010101111011010010110100101101111010100001000111100";

                public static ArrayList FindStorageAnchorsFromPartsBlob(byte[] blob)
        {
            var anchors = new ArrayList();
            if (blob == null || blob.Length == 0) return anchors;
            using (var ms = new System.IO.MemoryStream(blob))
            using (var br = new System.IO.BinaryReader(ms))
            {
                int sizeX = br.ReadByte();
                int sizeY = br.ReadByte();
                int sizeZ = br.ReadByte();
                int count = br.ReadUInt16();
                float halfX = sizeX * 0.5f;
                float halfZ = sizeZ * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    int bx = br.ReadSByte();
                    int by = br.ReadSByte();
                    int bz = br.ReadSByte();
                    string codes = br.ReadString();
                    int len = br.ReadInt32();
                    byte[] rawCubs = len > 0 ? br.ReadBytes(len) : Array.Empty<byte>();
                    if (rawCubs.Length == 0 || string.IsNullOrEmpty(codes)) continue;
                    var partSet = new GolemAnimSet { HeightBlocks = 1, Scale = 1f, BottomCodes = codes };
                    partSet.BaseSlot.BottomCuboids = rawCubs;
                    ArrayList localJoints = FindAllJointsRaw(partSet, false);
                    foreach (JointPoint jp in localJoints)
                    {
                        if (!jp.IsStorage) continue;
                        anchors.Add(new JointPoint
                        {
                            X = (bx - halfX) + jp.X,
                            Y = by + jp.Y,
                            Z = (bz - halfZ) + jp.Z,
                            Nx = jp.Nx,
                            Ny = jp.Ny,
                            Nz = jp.Nz,
                            IsStorage = true
                        });
                    }
                }
            }
            return anchors;
        }

        public static ArrayList FindAllJointsFromPartsBlob(byte[] blob)
        {
            var found = new ArrayList();
            if (blob == null || blob.Length == 0) return found;

            using (var ms = new System.IO.MemoryStream(blob))
            using (var br = new System.IO.BinaryReader(ms))
            {
                int sizeX = br.ReadByte();
                int sizeY = br.ReadByte();
                int sizeZ = br.ReadByte();
                int count = br.ReadUInt16();

                float halfX = sizeX * 0.5f;
                float halfZ = sizeZ * 0.5f;

                for (int i = 0; i < count; i++)
                {
                    int bx = br.ReadSByte();
                    int by = br.ReadSByte();
                    int bz = br.ReadSByte();
                    string codes = br.ReadString();
                    int len = br.ReadInt32();
                    byte[] rawCubs = len > 0 ? br.ReadBytes(len) : Array.Empty<byte>();
                    if (rawCubs.Length == 0 || string.IsNullOrEmpty(codes)) continue;

                    var partSet = new GolemAnimSet { HeightBlocks = 1, Scale = 1f, BottomCodes = codes };
                    partSet.BaseSlot.BottomCuboids = rawCubs;

                    // Для блоков корпуса берём прямые неинвертированные координаты центра руны внутри блока [0..1]
                    ArrayList localJoints = FindAllJointsRaw(partSet, false);
                    foreach (JointPoint jp in localJoints)
                    {
                        found.Add(new JointPoint
                        {
                            X = (bx - halfX) + jp.X,
                            Y = by + jp.Y,
                            Z = (bz - halfZ) + jp.Z,
                            Nx = jp.Nx,
                            Ny = jp.Ny,
                            Nz = jp.Nz,
                            IsArm = jp.IsArm
                        });
                    }
                }
            }
            return found;
        }

        public static ArrayList FindAllJoints(GolemAnimSet set) => FindAllJointsRaw(set, true);

        public static ArrayList FindAllJointsRaw(GolemAnimSet set, bool invertSegmentSpace)
        {
            var result = new ArrayList();
            if (set == null || set.BaseSlot == null || set.BaseSlot.IsEmpty) return result;

            int hBlocks = Math.Max(1, set.HeightBlocks);
            int maxY = hBlocks * 16;
            bool[] runeGrid = new bool[16 * maxY * 16];
            bool[] solidGrid = new bool[16 * maxY * 16];
            int minX = 16, maxX = -1, minY = maxY, minZ = 16, maxZ = -1;

            FillLayer(set.BaseSlot.BottomCuboids, set.BottomCodes, 0, runeGrid, solidGrid, ref minX, ref maxX, ref minY, ref minZ, ref maxZ);
            if (hBlocks >= 2)
                FillLayer(set.BaseSlot.TopCuboids, set.TopCodes, 16, runeGrid, solidGrid, ref minX, ref maxX, ref minY, ref minZ, ref maxZ);

            if (maxX < minX) return result;

            float cx = ((minX + maxX + 1) * 0.5f) / 16f;
            float cy = minY / 16f;
            float cz = ((minZ + maxZ + 1) * 0.5f) / 16f;

            ArrayList patterns = GetJointPatterns();

            for (int by = 0; by < hBlocks; by++)
            {
                int yBase = by * 16;
                for (int axis = 0; axis < 3; axis++)
                {
                    for (int slice = 0; slice < 16; slice++)
                    {
                        bool[] fg = new bool[256];
                        int lit = 0;
                        for (int u = 0; u < 16; u++)
                        {
                            for (int v = 0; v < 16; v++)
                            {
                                int x = axis == 0 ? slice : u;
                                int y = yBase + (axis == 2 ? slice : v);
                                int z = axis == 1 ? slice : (axis == 0 ? u : v);
                                if (runeGrid[(y * 16 + z) * 16 + x]) { fg[v * 16 + u] = true; lit++; }
                            }
                        }
                        if (lit < 4) continue;

                        for (int p = 0; p < patterns.Count; p++)
                        {
                            CustomRuneEntry pat = patterns[p] as CustomRuneEntry;
                            if (pat == null || string.IsNullOrEmpty(pat.Bits)) continue;
                            int sz = pat.Size;
                            if (sz != 4 && sz != 8 && sz != 16) continue;

                            int maxOff = 16 - sz;
                            for (int su = 0; su <= maxOff; su++)
                            {
                                for (int sv = 0; sv <= maxOff; sv++)
                                {
                                    if (!MatchPatch(fg, su, sv, sz, pat.Bits)) continue;
                                    float cu = su + sz * 0.5f, cv = sv + sz * 0.5f, cs = slice + 0.5f;
                                    float lx = axis == 0 ? cs : cu;
                                    float ly = (axis == 2 ? cs : cv) + yBase;
                                    float lz = axis == 1 ? cs : (axis == 0 ? cu : cv);

                                    float wx = invertSegmentSpace ? (1.0f - (lx / 16f + (0.5f - cx))) : (lx / 16f);
                                    float wy = invertSegmentSpace ? ((ly / 16f) - cy) : (ly / 16f);
                                    float wz = invertSegmentSpace ? (1.0f - (lz / 16f + (0.5f - cz))) : (lz / 16f);
                                    bool isArmPat = pat.Name != null && pat.Name.Equals("Joint_Arm", StringComparison.OrdinalIgnoreCase); bool isStoragePat = pat.Name != null && pat.Name.Equals("Storage", StringComparison.OrdinalIgnoreCase);

                                    bool merged = false;
                                    for (int k = 0; k < result.Count; k++)
                                    {
                                        JointPoint ex = result[k] as JointPoint;
                                        float dx = ex.X - wx, dy = ex.Y - wy, dz = ex.Z - wz;
                                        if (dx * dx + dy * dy + dz * dz < 0.032f)
                                        {
                                            ex.X = (ex.X + wx) * 0.5f;
                                            ex.Y = (ex.Y + wy) * 0.5f;
                                            ex.Z = (ex.Z + wz) * 0.5f;
                                            ex.IsArm = isArmPat;
                                            merged = true;
                                            break;
                                        }
                                    }
                                    if (!merged)
                                    {
                                        JointPoint jp = new JointPoint { X = wx, Y = wy, Z = wz, IsArm = isArmPat, IsStorage = isStoragePat };
                                        ComputeNormal(solidGrid, maxY, axis, (int)lx, (int)ly, (int)lz, jp);
                                        result.Add(jp);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }
    }
}
