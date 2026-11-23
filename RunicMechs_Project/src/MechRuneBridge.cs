using System;
using System.Collections;
using System.Collections.Generic;
using RunicGems;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace RunicMechs
{
    public static class MechRuneBridge
    {
        private static readonly HashSet<string> TechnicalRunes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MechCore16", "Joint_Hip", "Joint_Knee", "Joint_Ankle", "Seat", "FootPoint",
            "Arm_Shoulder", "Arm_Elbow", "Arm_Wrist", "Joint_Arm"
        };

                public static void ScanStorageAnchorsFromPartsBlob(ITreeAttribute targetAttr)
        {
            if (targetAttr == null || targetAttr.GetBool("mech_st_blob_scanned_v4", false)) return;
            targetAttr.SetBool("mech_st_blob_scanned_v4", true);
            byte[] blob = targetAttr.GetBytes("mech_parts_blob");
            if (blob == null || blob.Length < 5) return;

            ArrayList anchors = MultiJointScanner.FindStorageAnchorsFromPartsBlob(blob);
            int cnt = Math.Min(2, anchors.Count);
            targetAttr.SetInt("mech_storage_slots", cnt);
            for (int i = 0; i < cnt; i++)
            {
                JointPoint jp = anchors[i] as JointPoint;
                if (jp == null) continue;
                targetAttr.SetFloat("mech_st_x_" + i, jp.X);
                targetAttr.SetFloat("mech_st_y_" + i, jp.Y);
                targetAttr.SetFloat("mech_st_z_" + i, jp.Z);
                targetAttr.SetFloat("mech_st_nx_" + i, jp.Nx);
                targetAttr.SetFloat("mech_st_ny_" + i, jp.Ny);
                targetAttr.SetFloat("mech_st_nz_" + i, jp.Nz);
            }
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

        public static string ScanChassisRunesCsv(IWorldAccessor world, List<BlockEntityChisel> parts, int minX, int minY, int minZ, int sizeX, int sizeZ, ITreeAttribute targetAttr)
        {
            if (world == null || parts == null || parts.Count == 0) return "";
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var storagePoints = new List<float[]>();

            ArrayList entries = CustomRuneRegistry.GetEntries();
            if (entries == null || entries.Count == 0) return "";

            float halfX = sizeX * 0.5f;
            float halfZ = sizeZ * 0.5f;

            foreach (BlockEntityChisel be in parts)
            {
                if (be?.VoxelCuboids == null || be.BlockIds == null) continue;
                bool[] runeGrid = new bool[16 * 16 * 16];
                bool[] solidGrid = new bool[16 * 16 * 16];
                int litTotal = 0;

                foreach (uint c in be.VoxelCuboids)
                {
                    int mat = (int)((c >> 24) & 0xFF);
                    if (mat < 0 || mat >= be.BlockIds.Length) continue;
                    Block b = world.GetBlock((int)be.BlockIds.GetValue(mat));
                    string code = b?.Code?.ToString() ?? "";
                    bool isRune = code.Contains("inlaid-runiccrystal");

                    int x1 = (int)(c & 0xF), y1 = (int)((c >> 4) & 0xF), z1 = (int)((c >> 8) & 0xF);
                    int x2 = (int)((c >> 12) & 0xF), y2 = (int)((c >> 16) & 0xF), z2 = (int)((c >> 20) & 0xF);
                    for (int x = x1; x <= x2; x++)
                        for (int y = y1; y <= y2; y++)
                            for (int z = z1; z <= z2; z++)
                            {
                                int idx = (y * 16 + z) * 16 + x;
                                solidGrid.SetValue(true, idx);
                                if (isRune) { runeGrid.SetValue(true, idx); litTotal++; }
                            }
                }

                if (litTotal < 4) continue;

                int bx = be.Pos.X - minX;
                int by = be.Pos.Y - minY;
                int bz = be.Pos.Z - minZ;

                for (int axis = 0; axis < 3; axis++)
                {
                    for (int slice = 0; slice < 16; slice++)
                    {
                        bool[] fg = new bool[256];
                        int sliceLit = 0;
                        for (int u = 0; u < 16; u++)
                            for (int v = 0; v < 16; v++)
                            {
                                int x = axis == 0 ? slice : u;
                                int y = axis == 2 ? slice : v;
                                int z = axis == 1 ? slice : (axis == 0 ? u : v);
                                if ((bool)runeGrid.GetValue((y * 16 + z) * 16 + x)) { fg.SetValue(true, v * 16 + u); sliceLit++; }
                            }
                        if (sliceLit < 4) continue;

                        foreach (object obj in entries)
                        {
                            if (obj is not CustomRuneEntry entry) continue;
                            if (string.IsNullOrEmpty(entry.Name) || TechnicalRunes.Contains(entry.Name)) continue;
                            if (string.IsNullOrEmpty(entry.Bits)) continue;
                            int sz = entry.Size;
                            if (sz != 4 && sz != 8 && sz != 16) continue;
                            if (sliceLit < sz) continue;

                            int maxOff = 16 - sz;
                            for (int su = 0; su <= maxOff; su++)
                            {
                                for (int sv = 0; sv <= maxOff; sv++)
                                {
                                    if (!MatchPatchAnyRot(fg, su, sv, sz, entry.Bits)) continue;
                                    found.Add(entry.Name);

                                    if (entry.Name.Equals("Storage", StringComparison.OrdinalIgnoreCase) && storagePoints.Count < 2)
                                    {
                                        float cu = su + sz * 0.5f, cv = sv + sz * 0.5f, cs = slice + 0.5f;
                                        float lx = axis == 0 ? cs : cu;
                                        float ly = axis == 2 ? cs : cv;
                                        float lz = axis == 1 ? cs : (axis == 0 ? cu : cv);

                                        float wx = (bx - halfX) + (lx / 16f);
                                        float wy = by + (ly / 16f);
                                        float wz = (bz - halfZ) + (lz / 16f);

                                        float nx = 0f, ny = 0f, nz = 0f;
                                        int ix = (int)lx, iy = (int)ly, iz = (int)lz;
                                        if (axis == 0) nx = (ix + 1 >= 16 || !(bool)solidGrid.GetValue((iy * 16 + iz) * 16 + (ix + 1))) ? -1f : 1f;
                                        else if (axis == 1) nz = (iz + 1 >= 16 || !(bool)solidGrid.GetValue((iy * 16 + (iz + 1)) * 16 + ix)) ? 1f : -1f;
                                        else ny = (iy + 1 >= 16 || !(bool)solidGrid.GetValue(((iy + 1) * 16 + iz) * 16 + ix)) ? 1f : -1f;

                                        bool dup = false;
                                        foreach (float[] ex in storagePoints)
                                        {
                                            float dx = (float)ex.GetValue(0) - wx, dy = (float)ex.GetValue(1) - wy, dz = (float)ex.GetValue(2) - wz;
                                            if (dx * dx + dy * dy + dz * dz < 0.08f) { dup = true; break; }
                                        }
                                        if (!dup) storagePoints.Add(new float[] { wx, wy, wz, nx, ny, nz });
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (targetAttr != null)
            {
                targetAttr.SetInt("mech_storage_slots", storagePoints.Count);
                for (int i = 0; i < storagePoints.Count; i++)
                {
                    float[] pt = storagePoints[i];
                    targetAttr.SetFloat("mech_st_x_" + i, (float)pt.GetValue(0));
                    targetAttr.SetFloat("mech_st_y_" + i, (float)pt.GetValue(1));
                    targetAttr.SetFloat("mech_st_z_" + i, (float)pt.GetValue(2));
                    targetAttr.SetFloat("mech_st_nx_" + i, (float)pt.GetValue(3));
                    targetAttr.SetFloat("mech_st_ny_" + i, (float)pt.GetValue(4));
                    targetAttr.SetFloat("mech_st_nz_" + i, (float)pt.GetValue(5));
                }
            }

            return found.Count > 0 ? string.Join(",", found) : "";
        }

        private static bool MatchPatchAnyRot(bool[] fg, int su, int sv, int sz, string bits)
        {
            bool[] patch = new bool[sz * sz];
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                    patch.SetValue(fg.GetValue((sv + y) * 16 + (su + x)), y * sz + x);

            for (int rot = 0; rot < 4; rot++)
            {
                if (CheckBits(patch, bits)) return true;
                if (CheckBits(MirrorPatch(patch, sz), bits)) return true;
                patch = RotatePatch(patch, sz);
            }
            return false;
        }

        private static bool CheckBits(bool[] p, string bits)
        {
            for (int i = 0; i < p.Length; i++)
                if ((bool)p.GetValue(i) != (bits.Substring(i, 1) == "1")) return false;
            return true;
        }

        private static bool[] RotatePatch(bool[] p, int sz)
        {
            bool[] r = new bool[sz * sz];
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                    r.SetValue(p.GetValue(y * sz + x), x * sz + (sz - 1 - y));
            return r;
        }

        private static bool[] MirrorPatch(bool[] p, int sz)
        {
            bool[] r = new bool[sz * sz];
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                    r.SetValue(p.GetValue(y * sz + x), y * sz + (sz - 1 - x));
            return r;
        }
    }
}
