using Vintagestory.API.Common.Entities;
using System;
using System.Collections;
using System.Collections.Generic;
using RunicGems;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RunicMechs
{
    public static partial class MechRitualBuilder
    {
        // Руна Меха 16x16 (шестерня-ядро с крестовиной и ободом)
        public const string MechRune16x16 =
            "0000011111100000" +
            "0001110000111000" +
            "0011000110001100" +
            "0110001111000110" +
            "0100011001100010" +
            "1100110000110011" +
            "1001100110011001" +
            "1011001111001101" +
            "1011001111001101" +
            "1001100110011001" +
            "1100110000110011" +
            "0100011001100010" +
            "0110001111000110" +
            "0011000110001100" +
            "0001110000111000" +
            "0000011111100000";

        public static void RegisterMechRune()
        {
            ArrayList entries = CustomRuneRegistry.GetEntries();
            if (entries != null)
            {
                foreach (CustomRuneEntry e in entries)
                {
                    if (e != null && e.Name == "MechCore16") return;
                }
            }
            // MechCore16 in RunicGems
        }

        public static bool TryPerformMechRitual(IWorldAccessor world, BlockPos startPos, IServerPlayer plr, BlockFacing hitFace = null)
        {
            if (world == null || startPos == null) return false;
            if (!(world.BlockAccessor.GetBlockEntity(startPos) is BlockEntityChisel)) return false;

            // Собираем все смежные чизл-блоки (BFS до 64 блоков, чтобы проверить лимит 3x3x3 = 27 блоков)
            var visited = new HashSet<BlockPos>();
            var queue = new Queue<BlockPos>();
            var parts = new List<BlockEntityChisel>();

            visited.Add(startPos.Copy());
            queue.Enqueue(startPos.Copy());

            int minX = startPos.X, maxX = startPos.X;
            int minY = startPos.Y, maxY = startPos.Y;
            int minZ = startPos.Z, maxZ = startPos.Z;

            int[] dx = { 1, -1, 0, 0, 0, 0 };
            int[] dy = { 0, 0, 1, -1, 0, 0 };
            int[] dz = { 0, 0, 0, 0, 1, -1 };

            while (queue.Count > 0 && parts.Count < 64)
            {
                BlockPos cur = queue.Dequeue();
                if (!(world.BlockAccessor.GetBlockEntity(cur) is BlockEntityChisel be)) continue;
                parts.Add(be);

                if (cur.X < minX) minX = cur.X; if (cur.X > maxX) maxX = cur.X;
                if (cur.Y < minY) minY = cur.Y; if (cur.Y > maxY) maxY = cur.Y;
                if (cur.Z < minZ) minZ = cur.Z; if (cur.Z > maxZ) maxZ = cur.Z;

                for (int i = 0; i < 6; i++)
                {
                    BlockPos nb = cur.AddCopy((int)dx.GetValue(i), (int)dy.GetValue(i), (int)dz.GetValue(i));
                    if (!visited.Contains(nb) && world.BlockAccessor.GetBlockEntity(nb) is BlockEntityChisel)
                    {
                        visited.Add(nb);
                        queue.Enqueue(nb);
                    }
                }
            }

            int sizeX = maxX - minX + 1;
            int sizeY = maxY - minY + 1;
            int sizeZ = maxZ - minZ + 1;

            if (sizeX > 3 || sizeY > 3 || sizeZ > 3)
            {
                plr?.SendMessage(0, $"[RunicMechs] Основа меха слишком велика ({sizeX}x{sizeY}x{sizeZ})! Максимум: 3x3x3 блока.", EnumChatType.Notification);
                return false;
            }

            // Проверяем наличие руны Меха 16x16 из рунной крошки хотя бы на одном из блоков конструкции
                        bool hasMechRune = false;
            int crystalVoxels = 0;
            float seatLx = 0f, seatLy = (float)sizeY, seatLz = 0f;
            foreach (BlockEntityChisel be in parts)
            {
                if (ScanBlockForMechRune16(world, be, ref crystalVoxels, out float lx, out float ly, out float lz))
                {
                    hasMechRune = true;
                    seatLx = (be.Pos.X - minX) - sizeX * 0.5f + 0.5f;
                    seatLy = (be.Pos.Y - minY) + ly;
                    seatLz = (be.Pos.Z - minZ) - sizeZ * 0.5f + 0.5f;
                }
            }

            if (!hasMechRune)
            {
                plr?.SendMessage(0, "[RunicMechs] На основе меха не найдена руна Меха 16x16 из рунной крошки!", EnumChatType.Notification);
                return false;
            }

            // Объединяем воксели всех блоков 3x3x3 в единый компактный слепок (с масштабом под реальный размер конструкции)
            ItemStack packed = BuildMergedChassisStack(world, parts, minX, minY, minZ, sizeX, sizeY, sizeZ, crystalVoxels);
            if (packed == null) return false;

            EntityProperties prop = world.GetEntityType(new AssetLocation("runicmechs", "runicmech"));
            if (prop == null) return false;

            EntityRunicMech mech = world.ClassRegistry.CreateEntity(prop) as EntityRunicMech;
            if (mech == null) return false;

            // Удаляем исходные блоки конструкции из мира
            foreach (BlockEntityChisel be in parts)
            {
                world.BlockAccessor.SetBlock(0, be.Pos);
            }

            double cx = (minX + maxX + 1) * 0.5;
            double cz = (minZ + maxZ + 1) * 0.5;
            mech.Pos.SetPos(cx, minY, cz);
                        float frontYaw = plr?.Entity != null ? (plr.Entity.Pos.Yaw + (float)Math.PI) : 0f;
            if (hitFace != null && hitFace.IsHorizontal)
            {
                // Перед меха направлен строго в ту сторону (наружу из грани), по которой нанесён удар ключом!
                frontYaw = (float)Math.Atan2(hitFace.Normalf.X, hitFace.Normalf.Z);
            }
            mech.Pos.Yaw = frontYaw;
            // Сохраняем разницу поворота вокселей, чтобы при Yaw == frontYaw перед модели смотрел в сторону hitFace
            mech.WatchedAttributes.SetFloat("mech_mesh_yaw_offset", -frontYaw);
            mech.UnpackFromItemStack(packed, plr?.PlayerUID ?? "");
            mech.WatchedAttributes.SetInt("mech_dimX", sizeX);
            mech.WatchedAttributes.SetInt("mech_dimY", sizeY);
            mech.WatchedAttributes.SetInt("mech_dimZ", sizeZ);
            mech.WatchedAttributes.SetFloat("mech_seat_lx", seatLx);
            mech.WatchedAttributes.SetFloat("mech_seat_ly", seatLy);
            mech.WatchedAttributes.SetFloat("mech_seat_lz", seatLz);
            byte[] partsBlob = PackRawPartsBlob(world, parts, minX, minY, minZ, sizeX, sizeY, sizeZ);
            if (partsBlob != null) mech.WatchedAttributes.SetBytes("mech_parts_blob", partsBlob);
            mech.WatchedAttributes.MarkAllDirty();

            world.SpawnEntity(mech);
            world.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-active"), mech, null, 0.95f, 24f, 0.9f);
            plr?.SendMessage(0, $"[RunicMechs] Основа меха ({sizeX}x{sizeY}x{sizeZ}) пробуждена! Нажмите ПКМ Рунным ключом для сборки конечностей.", EnumChatType.Notification);
            return true;
        }
    }
}
