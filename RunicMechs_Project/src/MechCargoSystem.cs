using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RunicMechs
{
    public static partial class MechCargoSystem
    {
        public static bool IsSupportedContainer(Block block)
        {
            if (block == null || block.Id == 0) return false;
            string path = block.Code?.Path?.ToLowerInvariant() ?? "";
            return path.Contains("barrel")
                || path.Contains("crate")
                || path.Contains("chest")
                || path.Contains("trunk")
                || path.Contains("vessel")
                || path.Contains("crock")
                || path.Contains("anvil");
        }

        private static BlockPos FindNearestContainerInReach(EntityRunicMech mech, float armReach, float lookYaw)
        {
            IWorldAccessor world = mech.World;
            float lift = mech.GetCurrentLegLift();
            double shoulderY = mech.Pos.Y + lift + Math.Max(0.5, mech.WatchedAttributes.GetInt("mech_dimY", 1) * 0.75);
            double fwdX = Math.Sin(lookYaw);
            double fwdZ = Math.Cos(lookYaw);

            int r = (int)Math.Ceiling(armReach + 2.45);
            BlockPos center = mech.Pos.AsBlockPos;
            BlockPos bestPos = null;
            double bestScore = double.MaxValue;

            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -2; dy <= 3; dy++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        BlockPos cand = center.AddCopy(dx, dy, dz);
                        Block b = world.BlockAccessor.GetBlock(cand);
                        if (!IsSupportedContainer(b)) continue;

                        double vx = (cand.X + 0.5) - mech.Pos.X;
                        double vy = (cand.Y + 0.5) - shoulderY;
                        double vz = (cand.Z + 0.5) - mech.Pos.Z;
                        double dist = Math.Sqrt(vx * vx + vy * vy + vz * vz);
                        if (dist > armReach + 2.45) continue;

                        double horizLen = Math.Sqrt(vx * vx + vz * vz);
                        double dot = horizLen > 0.01 ? ((vx * fwdX + vz * fwdZ) / horizLen) : 1.0;
                        if (dot < 0.25) continue;

                        double score = dist - dot * 0.8;
                        if (score < bestScore) { bestScore = score; bestPos = cand; }
                    }
                }
            }
            return bestPos;
        }

        public static void HandleGrabOrDrop(EntityRunicMech mech, IServerPlayer player, BlockPos targetPos)
        {
            if (mech == null || player == null || mech.World.Side != EnumAppSide.Server) return;

            int armCount = mech.WatchedAttributes.GetInt("mech_arm_count", 0);
            float armReach = mech.WatchedAttributes.GetFloat("mech_arm_reach", 0f);
            if (armCount < 2 || armReach <= 0.1f)
            {
                player.SendMessage(0, "[RunicMechs] Для подъёма грузов нужны 2 собранные руки по 3 сегмента (руна Joint_Arm).", EnumChatType.Notification);
                return;
            }

            byte[] existingCargo = mech.WatchedAttributes.GetBytes("mech_cargo_blob");
            if (existingCargo != null && existingCargo.Length > 0)
            {
                TryPlaceCargo(mech, player, existingCargo);
                return;
            }

            IWorldAccessor world = mech.World;
            Block block = targetPos != null ? world.BlockAccessor.GetBlock(targetPos) : null;
            if (!IsSupportedContainer(block))
            {
                float lookYaw = player.Entity != null ? player.Entity.Pos.Yaw : mech.Pos.Yaw;
                targetPos = FindNearestContainerInReach(mech, armReach, lookYaw);
                block = targetPos != null ? world.BlockAccessor.GetBlock(targetPos) : null;
            }

            if (targetPos == null || !IsSupportedContainer(block))
            {
                player.SendMessage(0, $"[RunicMechs] Подойдите к хранилищу или наковальне (дальность рук: {armReach:F1} бл.) и нажмите ЛКМ.", EnumChatType.Notification);
                return;
            }

            float lift = mech.GetCurrentLegLift();
            double shoulderY = mech.Pos.Y + lift + Math.Max(0.5, mech.WatchedAttributes.GetInt("mech_dimY", 1) * 0.75);
            double dist = Math.Sqrt(
                Math.Pow((targetPos.X + 0.5) - mech.Pos.X, 2) +
                Math.Pow((targetPos.Y + 0.5) - shoulderY, 2) +
                Math.Pow((targetPos.Z + 0.5) - mech.Pos.Z, 2)
            );

            if (dist > armReach + 2.45)
            {
                player.SendMessage(0, $"[RunicMechs] Слишком далеко ({dist:F1} бл.)! Длина ваших рук позволяет достать до {armReach:F1} бл.", EnumChatType.Notification);
                return;
            }

            BlockPos secondPos = FindDoubleChestPartner(world, targetPos, block);
            bool isDouble = secondPos != null;

            string subType = ExtractContainerSubType(world, targetPos);
            byte[] mainBeBytes = SerializeBlockEntity(world, targetPos);
            byte[] secondBeBytes = isDouble ? SerializeBlockEntity(world, secondPos) : Array.Empty<byte>();
            string secondCode = isDouble ? (world.BlockAccessor.GetBlock(secondPos)?.Code?.ToString() ?? "") : "";
            int offX = isDouble ? (secondPos.X - targetPos.X) : 0;
            int offZ = isDouble ? (secondPos.Z - targetPos.Z) : 0;

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(block.Code.ToString());
                bw.Write(isDouble);
                bw.Write((sbyte)offX);
                bw.Write((sbyte)offZ);
                bw.Write(secondCode);
                bw.Write(mainBeBytes.Length);
                if (mainBeBytes.Length > 0) bw.Write(mainBeBytes);
                bw.Write(secondBeBytes.Length);
                if (secondBeBytes.Length > 0) bw.Write(secondBeBytes);

                mech.WatchedAttributes.SetBytes("mech_cargo_blob", ms.ToArray());
                mech.WatchedAttributes.SetBool("mech_cargo_double", isDouble);
                mech.WatchedAttributes.SetString("mech_cargo_code", block.Code.ToString());
                mech.WatchedAttributes.SetString("mech_cargo_type", subType);
                mech.WatchedAttributes.MarkAllDirty();
            }

            if (isDouble)
            {
                world.BlockAccessor.RemoveBlockEntity(secondPos);
                world.BlockAccessor.SetBlock(0, secondPos);
            }
            world.BlockAccessor.RemoveBlockEntity(targetPos);
            world.BlockAccessor.SetBlock(0, targetPos);
            world.PlaySoundAt(new AssetLocation("game:sounds/block/planks"), mech.Pos.X, mech.Pos.Y + 1.0, mech.Pos.Z, null, true, 24f, 0.9f);
        }

        private static string ExtractContainerSubType(IWorldAccessor world, BlockPos pos)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be == null) return "";
            var tree = new TreeAttribute();
            be.ToTreeAttributes(tree);
            return tree.GetString("type", "");
        }

        private static BlockPos FindDoubleChestPartner(IWorldAccessor world, BlockPos pos, Block block)
        {
            string path = block.Code?.Path?.ToLowerInvariant() ?? "";
            if (!path.Contains("chest") && !path.Contains("trunk")) return null;
            BlockEntityGenericTypedContainer be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be == null) return null;

            BlockFacing[] dirs = new BlockFacing[] { BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.EAST, BlockFacing.WEST };
            foreach (BlockFacing f in dirs)
            {
                BlockPos np = pos.AddCopy(f);
                Block nb = world.BlockAccessor.GetBlock(np);
                if (nb != null && nb.Code?.Domain == block.Code?.Domain && nb.Code?.Path == block.Code?.Path)
                {
                    BlockEntityGenericTypedContainer nbe = world.BlockAccessor.GetBlockEntity(np) as BlockEntityGenericTypedContainer;
                    if (nbe != null && nbe.type == be.type && (be.type?.Contains("double") == true || nbe.type?.Contains("double") == true || path.Contains("double")))
                        return np;
                }
            }
            return null;
        }

        private static byte[] SerializeBlockEntity(IWorldAccessor world, BlockPos pos)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be == null) return Array.Empty<byte>();
            var tree = new TreeAttribute();
            be.ToTreeAttributes(tree);
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms)) { tree.ToBytes(bw); return ms.ToArray(); }
        }
    }
}
