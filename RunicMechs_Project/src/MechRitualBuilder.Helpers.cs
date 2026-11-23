using System;
using System.Collections;
using System.Collections.Generic;
using RunicGems;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RunicMechs
{
    public static partial class MechRitualBuilder
    {
        private static bool ScanBlockForMechRune16(IWorldAccessor world, BlockEntityChisel be, ref int crystalVoxels, out float localX, out float localY, out float localZ)
        {
            localX = 0.5f; localY = 1.0f; localZ = 0.5f; if (be == null || be.VoxelCuboids == null || be.BlockIds == null) return false;
            bool[] runeGrid = new bool[16 * 16 * 16];
            int totalRune = 0;

            foreach (uint c in be.VoxelCuboids)
            {
                int mat = (int)((c >> 24) & 0xFF);
                if (mat < 0 || mat >= be.BlockIds.Length) continue;
                int blockId = (int)be.BlockIds.GetValue(mat);
                Block b = world.GetBlock(blockId);
                string code = b?.Code?.ToString() ?? "";
                if (!code.Contains("inlaid-runiccrystal")) continue;

                int x1 = (int)(c & 0xF), y1 = (int)((c >> 4) & 0xF), z1 = (int)((c >> 8) & 0xF);
                int x2 = (int)((c >> 12) & 0xF), y2 = (int)((c >> 16) & 0xF), z2 = (int)((c >> 20) & 0xF);
                for (int x = x1; x <= x2; x++)
                    for (int y = y1; y <= y2; y++)
                        for (int z = z1; z <= z2; z++)
                        {
                            runeGrid[(y * 16 + z) * 16 + x] = true;
                            totalRune++;
                            crystalVoxels++;
                        }
            }

            if (totalRune < 32) return false;

            for (int axis = 0; axis < 3; axis++)
            {
                for (int slice = 0; slice < 16; slice++)
                {
                    bool[] fg = new bool[16 * 16];
                    int lit = 0;
                    for (int u = 0; u < 16; u++)
                        for (int v = 0; v < 16; v++)
                        {
                            int x = axis == 0 ? slice : u;
                            int y = axis == 2 ? slice : v;
                            int z = axis == 1 ? slice : (axis == 0 ? u : v);
                            if (runeGrid[(y * 16 + z) * 16 + x]) { fg.SetValue(true, v * 16 + u); lit++; }
                        }

                    if (lit >= 32 && Match16x16(fg, MechRune16x16))
                    {
                        float sNorm = (slice + 0.5f) / 16f;
                        localX = axis == 0 ? sNorm : 0.5f;
                        localY = axis == 2 ? sNorm : 0.5f;
                        localZ = axis == 1 ? sNorm : 0.5f;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool Match16x16(bool[] fg, string targetBits)
        {
            bool[] cur = (bool[])fg.Clone();
            for (int rot = 0; rot < 4; rot++)
            {
                if (EqualsBits16(cur, targetBits)) return true;
                if (EqualsBits16(Mirror16(cur), targetBits)) return true;
                cur = Rotate16(cur);
            }
            return false;
        }

        private static bool EqualsBits16(bool[] g, string bits)
        {
            for (int i = 0; i < 256; i++)
                if ((bool)g.GetValue(i) != (bits.Substring(i, 1) == "1")) return false;
            return true;
        }

        private static bool[] Rotate16(bool[] g)
        {
            bool[] r = new bool[16 * 16];
            for (int v = 0; v < 16; v++)
                for (int u = 0; u < 16; u++)
                    r.SetValue(g.GetValue(v * 16 + u), u * 16 + (15 - v));
            return r;
        }

        private static bool[] Mirror16(bool[] g)
        {
            bool[] r = new bool[16 * 16];
            for (int v = 0; v < 16; v++)
                for (int u = 0; u < 16; u++)
                    r.SetValue(g.GetValue(v * 16 + u), v * 16 + (15 - u));
            return r;
        }

                        public static byte[] PackRawPartsBlob(IWorldAccessor world, List<BlockEntityChisel> parts, int minX, int minY, int minZ, int sizeX, int sizeY, int sizeZ)
        {
            using (var ms = new System.IO.MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms))
            {
                bw.Write((byte)sizeX);
                bw.Write((byte)sizeY);
                bw.Write((byte)sizeZ);
                bw.Write((ushort)parts.Count);

                foreach (BlockEntityChisel be in parts)
                {
                    bw.Write((sbyte)(be.Pos.X - minX));
                    bw.Write((sbyte)(be.Pos.Y - minY));
                    bw.Write((sbyte)(be.Pos.Z - minZ));

                    var codes = new List<string>();
                    if (be.BlockIds != null)
                    {
                        for (int i = 0; i < be.BlockIds.Length; i++)
                        {
                            Block b = world.GetBlock((int)be.BlockIds.GetValue(i));
                            codes.Add(b?.Code?.ToString() ?? "game:stone-basalt");
                        }
                    }
                    bw.Write(string.Join("|", codes));

                    byte[] rawCubs = be.VoxelCuboids != null ? ToBytes(be.VoxelCuboids) : Array.Empty<byte>();
                    bw.Write(rawCubs.Length);
                    if (rawCubs.Length > 0) bw.Write(rawCubs);
                }
                return ms.ToArray();
            }
        }

        private static byte[] ToBytes(List<uint> list)
        {
            uint[] arr = list.ToArray();
            byte[] raw = new byte[arr.Length * 4];
            Buffer.BlockCopy(arr, 0, raw, 0, raw.Length);
            return raw;
        }

        private static ItemStack BuildMergedChassisStack(IWorldAccessor world, List<BlockEntityChisel> parts, int minX, int minY, int minZ, int sizeX, int sizeY, int sizeZ, int crystalVoxels)
        {
            Item item = world.GetItem(new AssetLocation("runicgems", "golemhead"));
            if (item == null) return null;

            // Собираем единую палитру кодов материалов для всей 3x3x3 конструкции
            var codeList = new List<string>();
            var codeMap = new Dictionary<string, int>();

            int maxDim = Math.Max(sizeX, Math.Max(sizeY, sizeZ));
            int hBlocks = sizeY >= 2 ? 2 : 1;
            int totalH = hBlocks * 16;

            // Масштабная сетка вокселей (16 x (16 или 32) x 16), сжимающая 3x3x3 без потери деталей и рун!
            int[,,] grid = new int[16, totalH, 16];
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < totalH; y++)
                    for (int z = 0; z < 16; z++)
                        grid[x, y, z] = -1;

            foreach (BlockEntityChisel be in parts)
            {
                if (be.VoxelCuboids == null || be.BlockIds == null) continue;
                int bx = be.Pos.X - minX;
                int by = be.Pos.Y - minY;
                int bz = be.Pos.Z - minZ;

                foreach (uint c in be.VoxelCuboids)
                {
                    int mat = (int)((c >> 24) & 0xFF);
                    if (mat < 0 || mat >= be.BlockIds.Length) continue;
                    Block b = world.GetBlock((int)be.BlockIds.GetValue(mat));
                    string code = b?.Code?.ToString() ?? "";
                    if (string.IsNullOrEmpty(code)) continue;

                    if (!codeMap.TryGetValue(code, out int globalMat))
                    {
                        globalMat = codeList.Count;
                        codeList.Add(code);
                        codeMap[code] = globalMat;
                    }

                    int x1 = (int)(c & 0xF), y1 = (int)((c >> 4) & 0xF), z1 = (int)((c >> 8) & 0xF);
                    int x2 = (int)((c >> 12) & 0xF), y2 = (int)((c >> 16) & 0xF), z2 = (int)((c >> 20) & 0xF);

                    for (int vx = x1; vx <= x2; vx++)
                        for (int vy = y1; vy <= y2; vy++)
                            for (int vz = z1; vz <= z2; vz++)
                            {
                                int gx = GameMath.Clamp((bx * 16 + vx) * 16 / (maxDim * 16), 0, 15);
                                int gy = GameMath.Clamp((by * 16 + vy) * totalH / (sizeY * 16), 0, totalH - 1);
                                int gz = GameMath.Clamp((bz * 16 + vz) * 16 / (maxDim * 16), 0, 15);
                                // Приоритет отдаём рунной крошке, чтобы руны суставов не затёрлись при сжатии 3x3x3!
                                if (grid[gx, gy, gz] == -1 || code.Contains("inlaid-runiccrystal"))
                                    grid[gx, gy, gz] = globalMat;
                            }
                }
            }

            var botCubs = new List<uint>();
            var topCubs = new List<uint>();

            for (int x = 0; x < 16; x++)
                for (int y = 0; y < totalH; y++)
                    for (int z = 0; z < 16; z++)
                    {
                        int m = grid[x, y, z];
                        if (m < 0) continue;
                        if (y < 16)
                            botCubs.Add((uint)(x | (y << 4) | (z << 8) | (x << 12) | (y << 16) | (z << 20) | (m << 24)));
                        else
                            topCubs.Add((uint)(x | ((y - 16) << 4) | (z << 8) | (x << 12) | ((y - 16) << 16) | (z << 20) | (m << 24)));
                    }

            string joinedCodes = string.Join("|", codeList);
            var set = new GolemAnimSet
            {
                HeightBlocks = hBlocks,
                Scale = (float)maxDim,
                GolemName = $"Основа меха ({sizeX}x{sizeY}x{sizeZ})",
                BottomCodes = joinedCodes,
                TopCodes = hBlocks >= 2 ? joinedCodes : ""
            };
            set.BaseSlot.BottomCuboids = ToBytes(botCubs);
            if (hBlocks >= 2) set.BaseSlot.TopCuboids = ToBytes(topCubs);

            ItemStack stack = new ItemStack(item);
            stack.Attributes.SetBool("isPackedGolem", true);
            stack.Attributes.SetInt("capacityMl", 250 + crystalVoxels * 5);
            stack.Attributes.SetInt("oilMl", 250);
            GolemAnimCodec.SaveToAttributes(stack.Attributes, set);
            string scannedRunes = MechRuneBridge.ScanChassisRunesCsv(world, parts, minX, minY, minZ, sizeX, sizeZ, stack.Attributes); stack.Attributes.SetString("f_runes_0", scannedRunes); stack.Attributes.SetString("f_runes_3", scannedRunes);
            return stack;
        }
    }
}
