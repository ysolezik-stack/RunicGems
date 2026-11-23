using System;
using System.Collections;

namespace RunicGems
{
    public static partial class GolemStorageHelper
    {
        public static void ScanGolemStorageAnchor(EntityChiselGolem golem)
        {
            if (golem == null || golem.WatchedAttributes.GetBool("golem_st_scanned", false)) return;
            golem.WatchedAttributes.SetBool("golem_st_scanned", true);

            byte[] animBlob = golem.WatchedAttributes.GetBytes("animBlob");
            if (animBlob == null || animBlob.Length == 0) return;

            ArrayList entries = CustomRuneRegistry.GetEntries();
            CustomRuneEntry storageEntry = null;
            if (entries != null)
            {
                foreach (object o in entries)
                {
                    if (o is CustomRuneEntry re && "Storage".Equals(re.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        storageEntry = re;
                        break;
                    }
                }
            }
            if (storageEntry == null || string.IsNullOrEmpty(storageEntry.Bits)) return;

            GolemAnimSet set = GolemAnimCodec.Decompress(animBlob);
            GolemAnimSlot slot0 = set?.BaseSlot;
            if (slot0 == null) return;

            if (TryFindStorageInLayer(golem, slot0.BottomCuboids, set.BottomCodes, 0f, storageEntry)) return;
            TryFindStorageInLayer(golem, slot0.TopCuboids, set.TopCodes, 1f, storageEntry);
        }

        private static bool TryFindStorageInLayer(EntityChiselGolem golem, byte[] rawCubs, string codesCsv, float yBase, CustomRuneEntry entry)
        {
            if (rawCubs == null || rawCubs.Length < 4 || string.IsNullOrEmpty(codesCsv)) return false;
            string[] codes = codesCsv.Split(',');
            bool[] runeGrid = new bool[4096];
            bool[] solidGrid = new bool[4096];

            int count = rawCubs.Length / 4;
            for (int i = 0; i != count; i++)
            {
                uint c = BitConverter.ToUInt32(rawCubs, i * 4);
                int mat = (int)((c >> 24) & 0xFF);
                string code = mat >= 0 && mat < codes.Length ? (string)codes.GetValue(mat) : "";
                bool isRune = code.Contains("inlaid-runiccrystal");

                int x1 = (int)(c & 0xF), y1 = (int)((c >> 4) & 0xF), z1 = (int)((c >> 8) & 0xF);
                int x2 = (int)((c >> 12) & 0xF), y2 = (int)((c >> 16) & 0xF), z2 = (int)((c >> 20) & 0xF);
                for (int x = x1; x <= x2; x++)
                    for (int y = y1; y <= y2; y++)
                        for (int z = z1; z <= z2; z++)
                        {
                            int idx = (y * 16 + z) * 16 + x;
                            solidGrid.SetValue(true, idx);
                            if (isRune) runeGrid.SetValue(true, idx);
                        }
            }

            int sz = entry.Size;
            int maxOff = 16 - sz;
            for (int axis = 0; axis != 3; axis++)
            {
                for (int slice = 0; slice != 16; slice++)
                {
                    bool[] fg = new bool[256];
                    int lit = 0;
                    for (int u = 0; u != 16; u++)
                        for (int v = 0; v != 16; v++)
                        {
                            int x = axis == 0 ? slice : u;
                            int y = axis == 2 ? slice : v;
                            int z = axis == 1 ? slice : (axis == 0 ? u : v);
                            if ((bool)runeGrid.GetValue((y * 16 + z) * 16 + x)) { fg.SetValue(true, v * 16 + u); lit++; }
                        }
                    if (lit < sz) continue;

                    for (int su = 0; su <= maxOff; su++)
                    {
                        for (int sv = 0; sv <= maxOff; sv++)
                        {
                            if (!MatchPatch(fg, su, sv, sz, entry.Bits)) continue;
                            float cu = su + sz * 0.5f, cv = sv + sz * 0.5f, cs = slice + 0.5f;
                            float lx = axis == 0 ? cs : cu;
                            float ly = axis == 2 ? cs : cv;
                            float lz = axis == 1 ? cs : (axis == 0 ? cu : cv);

                            float nx = 0f, ny = 0f, nz = 0f;
                            int ix = (int)lx, iy = (int)ly, iz = (int)lz;
                            if (axis == 0) nx = (ix + 1 >= 16 || !(bool)solidGrid.GetValue((iy * 16 + iz) * 16 + (ix + 1))) ? -1f : 1f;
                            else if (axis == 1) nz = (iz + 1 >= 16 || !(bool)solidGrid.GetValue((iy * 16 + (iz + 1)) * 16 + ix)) ? 1f : -1f;
                            else ny = (iy + 1 >= 16 || !(bool)solidGrid.GetValue(((iy + 1) * 16 + iz) * 16 + ix)) ? 1f : -1f;

                            golem.WatchedAttributes.SetFloat("golem_st_x", 0.5f - (lx / 16f));
                            golem.WatchedAttributes.SetFloat("golem_st_y", yBase + (ly / 16f));
                            golem.WatchedAttributes.SetFloat("golem_st_z", (lz / 16f) - 0.5f);
                            golem.WatchedAttributes.SetFloat("golem_st_nx", nx);
                            golem.WatchedAttributes.SetFloat("golem_st_ny", ny);
                            golem.WatchedAttributes.SetFloat("golem_st_nz", nz);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool MatchPatch(bool[] fg, int su, int sv, int sz, string bits)
        {
            bool[] p = new bool[sz * sz];
            for (int y = 0; y != sz; y++)
                for (int x = 0; x != sz; x++)
                    p.SetValue(fg.GetValue((sv + y) * 16 + (su + x)), y * sz + x);

            for (int r = 0; r != 4; r++)
            {
                bool ok = true;
                for (int i = 0; i != p.Length; i++) if ((bool)p.GetValue(i) != (bits.Substring(i, 1) == "1")) { ok = false; break; }
                if (ok) return true;
                bool[] rot = new bool[sz * sz];
                for (int y = 0; y != sz; y++)
                    for (int x = 0; x != sz; x++)
                        rot.SetValue(p.GetValue(y * sz + x), x * sz + (sz - 1 - y));
                p = rot;
            }
            return false;
        }
    }
}
