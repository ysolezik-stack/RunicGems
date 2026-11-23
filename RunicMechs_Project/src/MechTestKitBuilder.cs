using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;
using System;
using System.Collections.Generic;
using RunicGems;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RunicMechs
{
    public static class MechTestKitBuilder
    {
        private static uint PackVoxel(int x, int y, int z, int mat)
        {
            return (uint)((x & 0xF) | ((y & 0xF) << 4) | ((z & 0xF) << 8) | ((x & 0xF) << 12) | ((y & 0xF) << 16) | ((z & 0xF) << 20) | ((mat & 0xFF) << 24));
        }

        private static byte[] ToBytes(List<uint> list)
        {
            uint[] arr = list.ToArray();
            byte[] raw = new byte[arr.Length * 4];
            Buffer.BlockCopy(arr, 0, raw, 0, raw.Length);
            return raw;
        }

        // Рисуем 4x4 руну сустава ("0110 1001 1001 0110") на грани X (x = xSlice)
        private static void StampJoint4x4OnX(List<uint> cubs, int xSlice, int y0, int z0, int matRune)
        {
            string bits = "0110100110010110";
            for (int v = 0; v < 4; v++)
                for (int u = 0; u < 4; u++)
                    if (bits.Substring(v * 4 + u, 1) == "1")
                        cubs.Add(PackVoxel(xSlice, y0 + v, z0 + u, matRune));
        }

        // Рисуем 4x4 руну сустава на горизонтальной грани Y (y = ySlice)
        private static void StampJoint4x4OnY(List<uint> cubs, int ySlice, int x0, int z0, int matRune)
        {
            string bits = "0110100110010110";
            for (int v = 0; v < 4; v++)
                for (int u = 0; u < 4; u++)
                    if (bits.Substring(v * 4 + u, 1) == "1")
                        cubs.Add(PackVoxel(x0 + u, ySlice, z0 + v, matRune));
        }

                                public static void BuildBlackBronzePlatform3x3(IWorldAccessor world, IServerPlayer plr)
        {
            if (world == null || plr?.Entity == null) return;
            Block chiselBlk = world.GetBlock(new AssetLocation("game", "chiselblock"))
                              ?? world.GetBlock(new AssetLocation("game", "chiselblock-1"));
            Block bronzeBlk = world.GetBlock(new AssetLocation("game", "metalblock-new-blackbronze"))
                              ?? world.GetBlock(new AssetLocation("game", "metalblock-riveted-blackbronze"));
            if (bronzeBlk == null)
            {
                foreach (Block b in world.Blocks)
                {
                    if (b?.Code != null && b.Code.Path.Contains("metalblock") && b.Code.Path.Contains("blackbronze"))
                    {
                        bronzeBlk = b;
                        break;
                    }
                }
            }
            if (bronzeBlk == null) bronzeBlk = world.GetBlock(new AssetLocation("game", "stone-basalt"));
            Block crystalBlk = world.GetBlock(new AssetLocation("runicgems", "inlaid-runiccrystal"));
            if (chiselBlk == null || bronzeBlk == null || crystalBlk == null)
            {
                plr.SendMessage(0, $"[RunicMechs] Не найден блок: chisel={chiselBlk != null}, bronze={bronzeBlk != null}, crystal={crystalBlk != null}", EnumChatType.Notification);
                return;
            }

            float yaw = plr.Entity.Pos.Yaw;
            int baseX = (int)Math.Floor(plr.Entity.Pos.X + Math.Sin(yaw) * 3.0) - 1;
            int baseY = (int)Math.Floor(plr.Entity.Pos.Y);
            int baseZ = (int)Math.Floor(plr.Entity.Pos.Z + Math.Cos(yaw) * 3.0) - 1;

            int[] matIds = { bronzeBlk.Id, crystalBlk.Id };
            
            

            for (int dx = 0; dx < 3; dx++)
            {
                for (int dz = 0; dz < 3; dz++)
                {
                    BlockPos pos = new BlockPos(baseX + dx, baseY, baseZ + dz);
                    world.BlockAccessor.SetBlock(chiselBlk.Id, pos);
                    Vintagestory.GameContent.BlockEntityChisel be = world.BlockAccessor.GetBlockEntity(pos) as Vintagestory.GameContent.BlockEntityChisel;
                    if (be == null)
                    {
                        world.BlockAccessor.SpawnBlockEntity("Chisel", pos);
                        be = world.BlockAccessor.GetBlockEntity(pos) as Vintagestory.GameContent.BlockEntityChisel;
                    }
                    if (be == null) continue;

                    be.BlockIds = (int[])matIds.Clone();
                    var cubs = new List<uint>();

                    bool isCenter = (dx == 1 && dz == 1);
                    bool isCorner = (dx != 1 && dz != 1);

                    for (int x = 0; x < 16; x++)
                        for (int y = 0; y < 15; y++)
                            for (int z = 0; z < 16; z++)
                                cubs.Add(PackVoxel(x, y, z, 0));

                    for (int x = 0; x < 16; x++)
                    {
                        for (int z = 0; z < 16; z++)
                        {
                            bool putCrystal = false;
                            if (isCenter)
                            {
                                putCrystal = MechRitualBuilder.MechRune16x16.Substring(z * 16 + x, 1) == "1";
                            }
                            else if (isCorner && x >= 6 && x <= 9 && z >= 6 && z <= 9)
                            {
                                int u = x - 6, v = z - 6;
                                putCrystal = "0110100110010110".Substring(v * 4 + u, 1) == "1";
                            }
                            cubs.Add(PackVoxel(x, 15, z, putCrystal ? 1 : 0));
                        }
                    }

                    be.VoxelCuboids = cubs;
                    be.MarkDirty(true);
                    world.BlockAccessor.MarkBlockDirty(pos);
                }
            }
        }

        public static EntityRunicMech SpawnMechEntity(IWorldAccessor world, IServerPlayer owner, bool spider, double offsetRight)
        {
            ItemStack chassis = CreateChassisStack(world, spider);
            EntityProperties prop = world.GetEntityType(new AssetLocation("runicmechs", "runicmech"));
            if (chassis == null || prop == null || owner?.Entity == null) return null;

            EntityRunicMech mech = world.ClassRegistry.CreateEntity(prop) as EntityRunicMech;
            if (mech == null) return null;

            float yaw = owner.Entity.Pos.Yaw;
            double fwdX = Math.Sin(yaw) * 2.2;
            double fwdZ = Math.Cos(yaw) * 2.2;
            double rightX = Math.Cos(yaw) * offsetRight;
            double rightZ = -Math.Sin(yaw) * offsetRight;

            mech.Pos.SetPos(owner.Entity.Pos.X + fwdX + rightX, owner.Entity.Pos.Y + 0.2, owner.Entity.Pos.Z + fwdZ + rightZ);
            mech.Pos.Yaw = yaw + (float)Math.PI;
            mech.Pos.SetFrom(mech.Pos);
            mech.UnpackFromItemStack(chassis, owner.PlayerUID);
            world.SpawnEntity(mech);
            return mech;
        }

        public static ItemStack CreateChassisStack(IWorldAccessor world, bool spider)
        {
            Item item = world.GetItem(new AssetLocation("runicgems", "golemhead"));
            if (item == null) return null;

            var cubs = new List<uint>();
            // Материал 0: гранит (тело), Материал 1: рунная крошка (руны суставов)
            string codes = "game:metalblock-new-blackbronze|runicgems:inlaid-runiccrystal";

            for (int x = 3; x <= 12; x++)
                for (int y = 2; y <= 13; y++)
                    for (int z = 3; z <= 12; z++)
                    {
                        bool isSideSocket = (x == 3 || x == 12) && (y >= 4 && y <= 7) && (
                            (!spider && z >= 6 && z <= 9) ||
                            (spider && ((z >= 3 && z <= 6) || (z >= 9 && z <= 12)))
                        );
                        if (!isSideSocket) cubs.Add(PackVoxel(x, y, z, 0));
                    }

            if (!spider)
            {
                StampJoint4x4OnX(cubs, 3, 4, 6, 1);
                StampJoint4x4OnX(cubs, 12, 4, 6, 1);
            }
            else
            {
                StampJoint4x4OnX(cubs, 3, 4, 3, 1);
                StampJoint4x4OnX(cubs, 12, 4, 3, 1);
                StampJoint4x4OnX(cubs, 3, 4, 9, 1);
                StampJoint4x4OnX(cubs, 12, 4, 9, 1);
            }

            var set = new GolemAnimSet
            {
                HeightBlocks = 1,
                Scale = 1.0f,
                GolemName = spider ? "Корпус паука (4 сустава)" : "Корпус шагохода (2 сустава)",
                BottomCodes = codes
            };
            set.BaseSlot.BottomCuboids = ToBytes(cubs);
            
            

            ItemStack stack = new ItemStack(item);
            stack.Attributes.SetBool("isPackedGolem", true);
            stack.Attributes.SetInt("capacityMl", 250);
            stack.Attributes.SetInt("oilMl", 250);
            GolemAnimCodec.SaveToAttributes(stack.Attributes, set); stack.Attributes.SetString("f_runes_0", "");
            return stack;
        }

        public static ItemStack CreateLegSegmentStack(IWorldAccessor world, int count)
        {
            Item item = world.GetItem(new AssetLocation("runicgems", "golemhead"));
            if (item == null) return null;

            var cubs = new List<uint>();
            string codes = "game:metalblock-new-copper|runicgems:inlaid-runiccrystal";

            // Стержень сегмента ноги (Y от 1 до 14, X/Z от 6 до 9)
            for (int x = 6; x <= 9; x++)
                for (int y = 1; y <= 14; y++)
                    for (int z = 6; z <= 9; z++)
                        cubs.Add(PackVoxel(x, y, z, 0));

            // Верхняя руна сустава (y = 15) и нижняя руна сустава (y = 0)
            StampJoint4x4OnY(cubs, 15, 6, 6, 1);
            StampJoint4x4OnY(cubs, 0, 6, 6, 1);

            var set = new GolemAnimSet
            {
                HeightBlocks = 1,
                Scale = 0.85f,
                GolemName = "Сегмент ноги (2 сустава)",
                BottomCodes = codes
            };
            set.BaseSlot.BottomCuboids = ToBytes(cubs);
            

            ItemStack stack = new ItemStack(item, count);
            stack.Attributes.SetBool("isPackedGolem", true);
            stack.Attributes.SetInt("capacityMl", 100);
            stack.Attributes.SetInt("oilMl", 100);
            GolemAnimCodec.SaveToAttributes(stack.Attributes, set); stack.Attributes.SetString("f_runes_0", "");
            return stack;
        }
    }
}
