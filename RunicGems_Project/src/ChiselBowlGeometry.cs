using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RunicGems
{
    public static class ChiselBowlGeometry
    {
        public static bool ValidateBowlShape(BlockEntityChisel ch, IWorldAccessor world, out int minCavityY, out int maxRimY, out List<int> cavityColumns, out int capacityItems)
        {
            minCavityY = 16; maxRimY = 0; capacityItems = 100;
            cavityColumns = new List<int>();
            if (ch?.VoxelCuboids == null || ch.BlockIds == null) return false;

            int oilBlockId = world.GetBlock(new AssetLocation("runicgems:runeglass-liquid"))?.BlockId ?? -1;
            int oilMatIdx = -1;
            for (int i = 0; i < ch.BlockIds.Length; i++)
                if ((ch.BlockIds.GetValue(i) is int id ? id : 0) == oilBlockId) { oilMatIdx = i; break; }

            List<bool> solid = Enumerable.Repeat(false, 4096).ToList();
            foreach (uint c in ch.VoxelCuboids)
            {
                if ((int)((c >> 24) & 0xFF) == oilMatIdx) continue;
                int x1 = (int)(c & 0xF), y1 = (int)((c >> 4) & 0xF), z1 = (int)((c >> 8) & 0xF);
                int x2 = (int)((c >> 12) & 0xF) + 1, y2 = (int)((c >> 16) & 0xF) + 1, z2 = (int)((c >> 20) & 0xF) + 1;
                for (int x = x1; x < x2; x++) for (int y = y1; y < y2; y++) for (int z = z1; z < z2; z++)
                {
                    solid[(y * 16 + z) * 16 + x] = true;
                    if (y > maxRimY) maxRimY = y;
                }
            }

            if (maxRimY < 2) return false;

            for (int testRim = maxRimY; testRim >= 2; testRim--)
            {
                int checkY = testRim - 1;
                List<bool> leaked = Enumerable.Repeat(false, 256).ToList();
                Queue<int> q = new Queue<int>();

                for (int i = 0; i < 16; i++)
                {
                    int[] edges = new int[] { i, 15 * 16 + i, i * 16, i * 16 + 15 };
                    foreach (int idx in edges)
                    {
                        int ex = idx / 16, ez = idx % 16;
                        if (!solid.ElementAt((checkY * 16 + ez) * 16 + ex) && !leaked.ElementAt(idx))
                        { leaked[idx] = true; q.Enqueue(idx); }
                    }
                }

                while (q.Count > 0)
                {
                    int cur = q.Dequeue();
                    int cx = cur / 16, cz = cur % 16;
                    int[] nxs = new int[] { cx - 1, cx + 1, cx, cx };
                    int[] nzs = new int[] { cz, cz, cz - 1, cz + 1 };
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = nxs[k], nz = nzs[k];
                        if (nx < 0 || nx >= 16 || nz < 0 || nz >= 16) continue;
                        int nIdx = nx * 16 + nz;
                        if (!leaked.ElementAt(nIdx) && !solid.ElementAt((checkY * 16 + nz) * 16 + nx))
                        { leaked[nIdx] = true; q.Enqueue(nIdx); }
                    }
                }

                List<int> found = new List<int>();
                int lowestFloor = 16, emptyVoxels = 0;
                for (int x = 1; x < 15; x++)
                    for (int z = 1; z < 15; z++)
                    {
                        int idx = x * 16 + z;
                        if (leaked.ElementAt(idx) || solid.ElementAt((checkY * 16 + z) * 16 + x)) continue;
                        int floor = -1;
                        for (int y = checkY - 1; y >= 0; y--)
                            if (solid.ElementAt((y * 16 + z) * 16 + x)) { floor = y; break; }
                        if (floor >= 0)
                        {
                            found.Add(idx);
                            if (floor + 1 < lowestFloor) lowestFloor = floor + 1;
                            for (int y = floor + 1; y <= checkY; y++)
                                if (!solid.ElementAt((y * 16 + z) * 16 + x)) emptyVoxels++;
                        }
                    }

                if (found.Count >= 9)
                {
                    cavityColumns = found;
                    minCavityY = lowestFloor;
                    maxRimY = testRim;
                    capacityItems = Math.Clamp((int)Math.Round(emptyVoxels * 0.25f), 10, 500);
                    return true;
                }
            }
            return false;
        }

        public static bool TryConvertToRuneBowl(IWorldAccessor world, BlockPos pos, BlockEntityChisel ch, IServerPlayer plr)
        {
            if (!ValidateBowlShape(ch, world, out _, out _, out _, out int capItems))
            {
                plr?.SendMessage(0, "Форма не подходит: сделайте герметичную выемку с дном и бортиками (от 3x3 вокселей).", EnumChatType.Notification);
                return false;
            }

            Block bowlBlock = world.GetBlock(new AssetLocation("runicgems:runebowl"));
            if (bowlBlock == null) return false;

            uint[] savedCuboids = ch.VoxelCuboids.ToArray();
            List<int> savedIds = new List<int>();
            for (int i = 0; i < ch.BlockIds.Length; i++) savedIds.Add(ch.BlockIds.GetValue(i) is int v ? v : 0);
            string savedName = ch.BlockName;

            world.BlockAccessor.SetBlock(bowlBlock.BlockId, pos);
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityChiselRuneBowl bowlBe)
            {
                bowlBe.RestoreChiselData(savedCuboids, savedIds.ToArray(), savedName, capItems);
                plr?.SendMessage(0, string.Format("Рунная чаша создана! Объём: {0:0.00} л", capItems / 100f), EnumChatType.Notification);
                return true;
            }
            return false;
        }
    }
}