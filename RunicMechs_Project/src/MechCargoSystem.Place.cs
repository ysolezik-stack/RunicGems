using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RunicMechs
{
    public static partial class MechCargoSystem
    {
        private static void RestoreBlockEntity(EntityRunicMech mech, BlockPos pos, byte[] data)
        {
            if (data == null || data.Length == 0) return;
            IWorldAccessor world = mech.World;
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be == null) return;

            var tree = new TreeAttribute();
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                tree.FromBytes(br);
            }

            // Обновляем координаты внутри сохранённого TreeAttribute на новую позицию установки
            tree.SetInt("posx", pos.X);
            tree.SetInt("posy", pos.Y);
            tree.SetInt("posz", pos.Z);

            be.FromTreeAttributes(tree, world);
            be.Initialize(mech.Api);
            be.MarkDirty(true);
            world.BlockAccessor.MarkBlockEntityDirty(pos);
            world.BlockAccessor.MarkBlockDirty(pos);
        }

        private static void TryPlaceCargo(EntityRunicMech mech, IServerPlayer player, byte[] blob)
        {
            IWorldAccessor world = mech.World;
            double fwdX = Math.Sin(mech.Pos.Yaw);
            double fwdZ = Math.Cos(mech.Pos.Yaw);
            double reachDist = Math.Max(1.3, mech.WatchedAttributes.GetInt("mech_dimX", 1) * 0.6 + 1.1);

            int px = (int)Math.Floor(mech.Pos.X + fwdX * reachDist);
            int pz = (int)Math.Floor(mech.Pos.Z + fwdZ * reachDist);
            int py = (int)Math.Floor(mech.Pos.Y + 0.5);

            BlockPos placePos = null;
            for (int dy = 2; dy >= -2; dy--)
            {
                BlockPos cand = new BlockPos(px, py + dy, pz);
                Block b = world.BlockAccessor.GetBlock(cand);
                Block below = world.BlockAccessor.GetBlock(cand.DownCopy());
                if ((b == null || b.Id == 0 || b.IsReplacableBy(b)) && below != null && below.CollisionBoxes != null && below.CollisionBoxes.Length > 0)
                {
                    placePos = cand;
                    break;
                }
            }

            if (placePos == null)
            {
                player.SendMessage(0, "[RunicMechs] Нет ровного места перед мехом, чтобы поставить груз.", EnumChatType.Notification);
                return;
            }

            using (var ms = new MemoryStream(blob))
            using (var br = new BinaryReader(ms))
            {
                string mainCode = br.ReadString();
                bool isDouble = br.ReadBoolean();
                int offX = br.ReadSByte();
                int offZ = br.ReadSByte();
                string secondCode = br.ReadString();
                int mainLen = br.ReadInt32();
                byte[] mainData = mainLen > 0 ? br.ReadBytes(mainLen) : Array.Empty<byte>();
                int secLen = br.ReadInt32();
                byte[] secData = secLen > 0 ? br.ReadBytes(secLen) : Array.Empty<byte>();

                Block mainBlock = world.GetBlock(new AssetLocation(mainCode));
                if (mainBlock == null) return;

                if (isDouble)
                {
                    BlockPos secondPos = placePos.AddCopy(offX, 0, offZ);
                    Block b2 = world.BlockAccessor.GetBlock(secondPos);
                    if (b2 != null && b2.Id != 0)
                    {
                        player.SendMessage(0, "[RunicMechs] Нужно свободное место 2x1 перед мехом для двойного сундука.", EnumChatType.Notification);
                        return;
                    }
                    Block secBlock = world.GetBlock(new AssetLocation(secondCode)) ?? mainBlock;
                    world.BlockAccessor.SetBlock(mainBlock.BlockId, placePos);
                    world.BlockAccessor.SetBlock(secBlock.BlockId, secondPos);
                    RestoreBlockEntity(mech, placePos, mainData);
                    RestoreBlockEntity(mech, secondPos, secData);
                }
                else
                {
                    world.BlockAccessor.SetBlock(mainBlock.BlockId, placePos);
                    RestoreBlockEntity(mech, placePos, mainData);
                }
            }

            mech.WatchedAttributes.RemoveAttribute("mech_cargo_blob");
            mech.WatchedAttributes.SetBool("mech_cargo_double", false);
            mech.WatchedAttributes.SetString("mech_cargo_code", "");
            mech.WatchedAttributes.SetString("mech_cargo_type", "");
            mech.WatchedAttributes.MarkAllDirty();
            world.PlaySoundAt(new AssetLocation("game:sounds/block/planks"), placePos.X + 0.5, placePos.Y + 0.5, placePos.Z + 0.5, null, true, 24f, 0.95f);
        }
    }
}
