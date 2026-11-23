using System;
using System.Collections;
using RunicGems;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public class MechAssemblyInventory : InventoryGeneric
    {
        public readonly EntityChiselGolem Host;
        public readonly ArrayList HostSockets;
        private bool isLoading;

        public MechAssemblyInventory(EntityChiselGolem host, ArrayList hostSockets, ICoreAPI api)
            : base(32, "mechassembly-" + host.EntityId, api)
        {
            Host = host;
            HostSockets = hostSockets ?? new ArrayList();
            LoadFromHost();
        }

        public override bool CanContain(ItemSlot sinkSlot, ItemSlot sourceSlot)
        {
            if (sourceSlot?.Itemstack == null) return false;
            if (sourceSlot.Itemstack.Collectible?.Code?.ToString() != "runicgems:golemhead") return false;
            int slotId = GetSlotId(sinkSlot);
            int row = slotId / 4;
            int col = slotId % 4;
            if (row >= 0 && row < HostSockets.Count && HostSockets[row] is JointPoint jp && jp.IsArm && col >= 3)
                return false;
            ITreeAttribute st = sourceSlot.Itemstack.Attributes;
            return st != null && (st.GetBool("isPackedGolem", false) || st.HasAttribute("f_cuboids_0") || st.HasAttribute("animBlob"));
        }

        public void LoadFromHost()
        {
            isLoading = true;
            try
            {
                for (int i = 0; i < Count; i++) this[i].Itemstack = null;
                byte[] data = Host.WatchedAttributes.GetBytes("mech_chains_v2");
                ArrayList chains = MechChainCodec.Decode(data);
                Item item = Api.World.GetItem(new AssetLocation("runicgems", "golemhead"));
                if (item == null) return;

                foreach (MechLegChain ch in chains)
                {
                    int row = FindMatchingSocketRow(ch.RootSocket);
                    if (row < 0 || row >= 8) continue;
                    int maxSeg = ch.IsArm ? 3 : 4;
                    int segIdx = 0;
                    foreach (MechSegmentNode seg in ch.Segments)
                    {
                        if (segIdx >= maxSeg) break;
                        GolemAnimSet set = GolemAnimCodec.Decompress(seg.GolemBlob);
                        if (set != null)
                        {
                            ItemStack st = new ItemStack(item);
                            st.Attributes.SetBool("isPackedGolem", true);
                            GolemAnimCodec.SaveToAttributes(st.Attributes, set);
                            this[row * 4 + segIdx].Itemstack = st;
                        }
                        segIdx++;
                    }
                }
            }
            finally { isLoading = false; }
        }

        private int FindMatchingSocketRow(JointPoint target)
        {
            if (target == null) return -1;
            int bestIdx = -1;
            float bestDist = 0.25f;
            for (int i = 0; i < Math.Min(8, HostSockets.Count); i++)
            {
                JointPoint jp = HostSockets[i] as JointPoint;
                if (jp == null || jp.IsArm != target.IsArm) continue;
                float dx = jp.X - target.X, dy = jp.Y - target.Y, dz = jp.Z - target.Z;
                float d = dx * dx + dy * dy + dz * dz;
                if (d < bestDist) { bestDist = d; bestIdx = i; }
            }
            return bestIdx;
        }

        public override void OnItemSlotModified(ItemSlot slot)
        {
            base.OnItemSlotModified(slot);
            if (isLoading || Api.Side != EnumAppSide.Server) return;
            SyncToHost();
        }

        public void SyncToHost()
        {
            var chains = new ArrayList();
            int maxRows = Math.Min(8, HostSockets.Count);
            int fullArms = 0;
            float minArmReach = 99f;

            for (int row = 0; row < maxRows; row++)
            {
                JointPoint root = (JointPoint)HostSockets[row];
                var ch = new MechLegChain { RootSocket = root, IsArm = root.IsArm };
                int maxSeg = root.IsArm ? 3 : 4;
                float totalLen = 0f;

                for (int s = 0; s < maxSeg; s++)
                {
                    ItemStack st = this[row * 4 + s].Itemstack;
                    if (st == null || st.StackSize <= 0) break;

                    MechSegmentNode node = BuildSegmentNode(st, root.IsArm && s == 2);
                    if (node != null)
                    {
                        ch.Segments.Add(node);
                        totalLen += node.Length;
                    }
                }

                if (ch.Segments.Count > 0)
                {
                    chains.Add(ch);
                    if (root.IsArm && ch.Segments.Count == 3)
                    {
                        fullArms++;
                        if (totalLen < minArmReach) minArmReach = totalLen;
                    }
                }
            }

            if (chains.Count == 0) Host.WatchedAttributes.RemoveAttribute("mech_chains_v2");
            else Host.WatchedAttributes.SetBytes("mech_chains_v2", MechChainCodec.Encode(chains));

            Host.WatchedAttributes.SetInt("mech_arm_count", fullArms);
            Host.WatchedAttributes.SetFloat("mech_arm_reach", fullArms >= 2 ? minArmReach : 0f);
            Host.WatchedAttributes.MarkAllDirty();
        }

        private MechSegmentNode BuildSegmentNode(ItemStack segStack, bool isHandTip)
        {
            GolemAnimSet segSet = GolemAnimCodec.LoadFromAttributes(segStack.Attributes);
            if (segSet == null || segSet.BaseSlot == null || segSet.BaseSlot.IsEmpty)
            {
                segSet = new GolemAnimSet
                {
                    HeightBlocks = segStack.Attributes.GetInt("heightBlocks", 1),
                    Scale = segStack.Attributes.GetFloat("golemScale", 1f),
                    BottomCodes = segStack.Attributes.GetString("f_codes_0", ""),
                    TopCodes = segStack.Attributes.GetString("f_top_codes_0", "")
                };
                segSet.BaseSlot.BottomCuboids = segStack.Attributes.GetBytes("f_cuboids_0");
                segSet.BaseSlot.TopCuboids = segStack.Attributes.GetBytes("f_top_cuboids_0");
            }

            ArrayList joints = MultiJointScanner.FindAllJoints(segSet);
            JointPoint topJ = joints.Count > 0 ? (JointPoint)joints.ToArray().GetValue(0) : new JointPoint { X = 0.5f, Y = 0.95f, Z = 0.5f, Ny = 1f };
            JointPoint botJ = topJ;
            foreach (JointPoint cand in joints)
            {
                if (cand.Y > topJ.Y) topJ = cand;
                if (cand.Y < botJ.Y) botJ = cand;
            }

            if (joints.Count <= 1)
            {
                if (isHandTip)
                {
                    // Для кисти с 1 руной запястья: кончик пальцев направлен от запястья через центр масс модели кисти!
                    Vec3f com = ComputeVoxelCenter(segSet.BaseSlot.BottomCuboids);
                    float vx = com.X - topJ.X, vy = com.Y - topJ.Y, vz = com.Z - topJ.Z;
                    float vLen = (float)Math.Sqrt(vx * vx + vy * vy + vz * vz);
                    if (vLen > 0.05f)
                        botJ = new JointPoint { X = topJ.X + (vx / vLen) * 0.65f, Y = topJ.Y + (vy / vLen) * 0.65f, Z = topJ.Z + (vz / vLen) * 0.65f };
                    else
                        botJ = new JointPoint { X = topJ.X, Y = 0.04f, Z = topJ.Z, Ny = -1f };
                }
                else
                {
                    botJ = new JointPoint { X = topJ.X, Y = 0.04f, Z = topJ.Z, Ny = -1f };
                }
            }
            else if (Math.Abs(topJ.Y - botJ.Y) < 0.08f && !isHandTip)
            {
                botJ = new JointPoint { X = topJ.X, Y = 0.04f, Z = topJ.Z, Ny = -1f };
            }

            float dx = topJ.X - botJ.X, dy = topJ.Y - botJ.Y, dz = topJ.Z - botJ.Z;
            float sc = GameMath.Clamp(segSet.Scale, 0.5f, 1.25f);
            float len = Math.Max(0.35f, (float)Math.Sqrt(dx * dx + dy * dy + dz * dz)) * sc;

            return new MechSegmentNode
            {
                GolemBlob = GolemAnimCodec.Compress(segSet),
                Scale = sc,
                TopJoint = topJ,
                BottomJoint = botJ,
                Length = len
            };
        }

        private Vec3f ComputeVoxelCenter(byte[] raw)
        {
            if (raw == null || raw.Length < 4) return new Vec3f(0.5f, 0.5f, 0.5f);
            int len = raw.Length / 4;
            uint[] arr = new uint[len];
            Buffer.BlockCopy(raw, 0, arr, 0, raw.Length);
            double sx = 0, sy = 0, sz = 0;
            for (int i = 0; i < len; i++)
            {
                uint c = arr[i];
                double cx = ((c & 0xF) + ((c >> 12) & 0xF) + 1) / 32.0;
                double cy = (((c >> 4) & 0xF) + ((c >> 16) & 0xF) + 1) / 32.0;
                double cz = (((c >> 8) & 0xF) + ((c >> 20) & 0xF) + 1) / 32.0;
                sx += (1.0 - cx); sy += cy; sz += (1.0 - cz);
            }
            return new Vec3f((float)(sx / len), (float)(sy / len), (float)(sz / len));
        }
    }
}
