using System;
using System.Collections;
using RunicGems;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public static partial class MultiJointScanner
    {
        private static void ComputeNormal(bool[] solidGrid, int maxY, int axis, int ix, int iy, int iz, JointPoint jp)
        {
            ix = GameMath.Clamp(ix, 0, 15);
            iy = GameMath.Clamp(iy, 0, maxY - 1);
            iz = GameMath.Clamp(iz, 0, 15);
            float nx = 0, ny = 0, nz = 0;
            if (axis == 0)
            {
                if (ix + 1 >= 16 || !solidGrid[(iy * 16 + iz) * 16 + (ix + 1)]) nx = -1f;
                else if (ix - 1 < 0 || !solidGrid[(iy * 16 + iz) * 16 + (ix - 1)]) nx = 1f;
            }
            else if (axis == 1)
            {
                if (iz + 1 >= 16 || !solidGrid[(iy * 16 + (iz + 1)) * 16 + ix]) nz = -1f;
                else if (iz - 1 < 0 || !solidGrid[(iy * 16 + (iz - 1)) * 16 + ix]) nz = 1f;
            }
            else
            {
                if (iy + 1 >= maxY || !solidGrid[((iy + 1) * 16 + iz) * 16 + ix]) ny = 1f;
                else if (iy - 1 < 0 || !solidGrid[((iy - 1) * 16 + iz) * 16 + ix]) ny = -1f;
            }
            if (nx == 0 && ny == 0 && nz == 0) nx = 1f;
            jp.Nx = nx; jp.Ny = ny; jp.Nz = nz;
        }

        private static ArrayList GetJointPatterns()
        {
            var list = new ArrayList();
            list.Add(new CustomRuneEntry("Joint4", 4, Joint4x4));
            list.Add(new CustomRuneEntry("JointOval8", 8, JointOval8x8));
            list.Add(new CustomRuneEntry("JointRing8", 8, JointRing8x8));
            list.Add(new CustomRuneEntry("Joint_Arm", 8, JointArm8x8));

            ArrayList custom = CustomRuneRegistry.GetEntries();
            if (custom != null)
            {
                for (int i = 0; i < custom.Count; i++)
                {
                    CustomRuneEntry e = custom[i] as CustomRuneEntry;
                    if (e == null || string.IsNullOrEmpty(e.Name)) continue;
                    string low = e.Name.ToLowerInvariant();
                    if (low.Contains("joint") || low.Contains("сустав") || low.Contains("socket") || low.Contains("arm") || low.Contains("storage") || low.Contains("хранен")) list.Add(e);
                }
            }
            return list;
        }

        private static void FillLayer(byte[] raw, string codesStr, int yOff, bool[] runeGrid, bool[] solidGrid, ref int minX, ref int maxX, ref int minY, ref int minZ, ref int maxZ)
        {
            if (raw == null || raw.Length == 0 || string.IsNullOrEmpty(codesStr)) return;
            string[] codes = codesStr.Split('|');
            int len = raw.Length / 4;
            uint[] arr = new uint[len];
            Buffer.BlockCopy(raw, 0, arr, 0, raw.Length);

            for (int i = 0; i != len; i++)
            {
                uint c = arr[i];
                int mat = (int)((c >> 24) & 0xFF);
                if (mat < 0 || mat >= codes.Length) continue;
                bool isCrystal = (codes[mat] ?? "").ToLowerInvariant().Contains("inlaid-runiccrystal");

                int x1 = (int)(c & 0xF), y1 = (int)((c >> 4) & 0xF), z1 = (int)((c >> 8) & 0xF);
                int x2 = (int)((c >> 12) & 0xF), y2 = (int)((c >> 16) & 0xF), z2 = (int)((c >> 20) & 0xF);
                if (x1 < minX) minX = x1; if (x2 > maxX) maxX = x2;
                if (y1 + yOff < minY) minY = y1 + yOff;
                if (z1 < minZ) minZ = z1; if (z2 > maxZ) maxZ = z2;

                for (int x = x1; x <= x2; x++)
                    for (int y = y1; y <= y2; y++)
                        for (int z = z1; z <= z2; z++)
                        {
                            int idx = ((y + yOff) * 16 + z) * 16 + x;
                            solidGrid[idx] = true;
                            if (isCrystal) runeGrid[idx] = true;
                        }
            }
        }

        private static bool MatchPatch(bool[] fg, int u0, int v0, int sz, string targetBits)
        {
            if (targetBits.Length < sz * sz) return false;
            bool[] cur = new bool[sz * sz];
            for (int v = 0; v < sz; v++)
                for (int u = 0; u < sz; u++)
                    cur[v * sz + u] = fg[(v0 + v) * 16 + (u0 + u)];

            for (int rot = 0; rot < 4; rot++)
            {
                if (EqualsBits(cur, sz, targetBits)) return true;
                if (EqualsBits(MirrorGrid(cur, sz), sz, targetBits)) return true;
                cur = RotateGrid90(cur, sz);
            }
            return false;
        }

        private static bool EqualsBits(bool[] g, int sz, string bits)
        {
            int len = sz * sz;
            for (int i = 0; i < len; i++) if (g[i] != (bits[i] == '1')) return false;
            return true;
        }

        private static bool[] RotateGrid90(bool[] g, int sz)
        {
            bool[] r = new bool[sz * sz];
            for (int v = 0; v < sz; v++)
                for (int u = 0; u < sz; u++)
                    r[u * sz + (sz - 1 - v)] = g[v * sz + u];
            return r;
        }

        private static bool[] MirrorGrid(bool[] g, int sz)
        {
            bool[] r = new bool[sz * sz];
            for (int v = 0; v < sz; v++)
                for (int u = 0; u < sz; u++)
                    r[v * sz + (sz - 1 - u)] = g[v * sz + u];
            return r;
        }
    }
}
