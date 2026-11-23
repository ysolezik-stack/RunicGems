using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RunicMechs
{
    public partial class EntityRunicMech
    {
        private double currentSprintMult = 1.0;
        private double verticalVel = 0.0;
        private float netFwd, netStrafe, netYaw;
        private bool netSprint, hasNetInput, wasGrabPressed, isArmAttacking;
        private float grabHoldTimer, armAttackCooldown;

        public float GetCurrentLegLift() => Math.Max(0f, lastBoxH - Math.Max(1, WatchedAttributes.GetInt("mech_dimY", 1)));

        private void DismountPilot(EntityPlayer pilot)
        {
            WatchedAttributes.SetLong("mech_pilot_id", 0);
            WatchedAttributes.SetBool("mech_sprinting", false);
            WatchedAttributes.SetBool("mech_strafing", false);
            WatchedAttributes.SetBool("mech_arm_attack", false);
            hasNetInput = false;
            isArmAttacking = false;
            netFwd = 0f;
            netStrafe = 0f;
            netSprint = false; netCrouch = false;
            if (pilot != null)
            {
                pilot.WatchedAttributes.SetLong("mech_riding_id", 0);
                pilot.Controls.FloorSitting = false;
                pilot.StopAnimation("sitflooridle");
                pilot.SetCollisionBox(0.6f, 1.85f);
                double sx = Math.Cos(Pos.Yaw) * 1.4;
                double sz = -Math.Sin(Pos.Yaw) * 1.4;
                pilot.Pos.SetPos(Pos.X + sx, Pos.Y + 0.35, Pos.Z + sz);
                #pragma warning disable 0618
                pilot.ServerPos.SetPos(pilot.Pos.XYZ);
                #pragma warning restore 0618
            }
        }

        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data)
        {
            if (packetid == 9917 && player != null && WatchedAttributes.GetLong("mech_pilot_id", 0) == player.Entity?.EntityId)
            {
                bool atk = data != null && data.Length >= 1 && (byte)data.GetValue(0) == 1;
                isArmAttacking = atk;
                WatchedAttributes.SetBool("mech_arm_attack", atk);
                return;
            }
            if (packetid == 9916 && player != null && WatchedAttributes.GetLong("mech_pilot_id", 0) == player.Entity?.EntityId)
            {
                BlockPos tp = null;
                if (data != null && data.Length >= 1)
                {
                    using (var ms = new MemoryStream(data))
                    using (var br = new BinaryReader(ms))
                    {
                        if (br.ReadBoolean() && data.Length >= 13)
                        {
                            tp = new BlockPos(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
                        }
                    }
                }
                MechCargoSystem.HandleGrabOrDrop(this, player, tp);
                return;
            }
            if (packetid == 9915 && data != null && data.Length > 0)
            {
                string mode = System.Text.Encoding.UTF8.GetString(data);
                if (mode == "auto" || mode == "spider" || mode == "biped") WatchedAttributes.SetString("mech_gait", mode);
                return;
            }
                        if (packetid == 9918 && World.Side == EnumAppSide.Server && player is IServerPlayer spReb3)
            {
                if (WatchedAttributes.GetLong("mech_pilot_id", 0) != 0)
                {
                    spReb3.SendMessage(0, "[RunicMechs] Сначала высадите пилота с меха!", EnumChatType.Notification);
                    return;
                }
                if (WatchedAttributes.GetInt("mounted_storage_count", 0) > 0 || WatchedAttributes.HasAttribute("mech_cargo_blob"))
                {
                    spReb3.SendMessage(0, "[RunicMechs] Сначала снимите навесные хранилища и выгрузите груз из рук!", EnumChatType.Notification);
                    return;
                }
                byte[] pBlob = WatchedAttributes.GetBytes("mech_parts_blob");
                if (pBlob == null || pBlob.Length < 5)
                {
                    spReb3.SendMessage(0, "[RunicMechs] Данные конструкции корпуса не найдены!", EnumChatType.Notification);
                    return;
                }

                Block chiselBlk = World.GetBlock(new AssetLocation("game:chiseledblock"));
                int cx = (int)Math.Floor(Pos.X);
                int cy = (int)Math.Floor(Pos.Y);
                int cz = (int)Math.Floor(Pos.Z);

                var partsToPlace = new System.Collections.Generic.List<System.Tuple<BlockPos, string, byte[]>>();
                using (var ms = new System.IO.MemoryStream(pBlob))
                using (var br = new System.IO.BinaryReader(ms))
                {
                    int sizeX = br.ReadByte();
                    int sizeY = br.ReadByte();
                    int sizeZ = br.ReadByte();
                    int count = br.ReadUInt16();
                    int offX = sizeX / 2;
                    int offZ = sizeZ / 2;

                    for (int i = 0; i != count; i++)
                    {
                        int bx = br.ReadSByte();
                        int by = br.ReadSByte();
                        int bz = br.ReadSByte();
                        string codes = br.ReadString();
                        int len = br.ReadInt32();
                        byte[] rawCubs = len > 0 ? br.ReadBytes(len) : System.Array.Empty<byte>();
                        if (rawCubs.Length == 0) continue;

                        BlockPos targetPos = new BlockPos(cx + bx - offX, cy + by, cz + bz - offZ);
                        Block curBlk = World.BlockAccessor.GetBlock(targetPos);
                        if (curBlk != null && curBlk.Id != 0 && !curBlk.IsReplacableBy(chiselBlk))
                        {
                            spReb3.SendMessage(0, $"[RunicMechs] Недостаточно свободного места для раскладки блоков (мешает блок в {targetPos.X}, {targetPos.Y}, {targetPos.Z})!", EnumChatType.Notification);
                            return;
                        }
                        partsToPlace.Add(System.Tuple.Create(targetPos, codes, rawCubs));
                    }
                }

                System.Collections.ArrayList sockets = RunicMechsModSystem.GetHostJoints(this);
                if (sockets.Count > 0)
                {
                    var mInv = new MechAssemblyInventory(this, sockets, World.Api);
                    for (int i = 0; i != mInv.Count; i++)
                    {
                        if (!mInv[i].Empty)
                        {
                            ItemStack st = mInv[i].TakeOutWhole();
                            if (!spReb3.InventoryManager.TryGiveItemstack(st)) World.SpawnItemEntity(st, Pos.XYZ.Add(0, 0.8, 0));
                        }
                    }
                }

                foreach (var item in partsToPlace)
                {
                    BlockPos pos = item.Item1;
                    string codesCsv = item.Item2.Replace("runicgems:inlaid-runiccrystal-active", "runicgems:inlaid-runiccrystal");
                    byte[] rawCubs = item.Item3;

                    World.BlockAccessor.SetBlock(chiselBlk.BlockId, pos);
                    if (World.BlockAccessor.GetBlockEntity(pos) is Vintagestory.GameContent.BlockEntityChisel beChisel)
                    {
                        beChisel.OnBlockPlaced(null);
                        string[] codeArr = codesCsv.Split(new char[] { ';', ',', '|' }, System.StringSplitOptions.RemoveEmptyEntries);
                        int[] matIds = new int[codeArr.Length];
                        for (int m = 0; m != codeArr.Length; m++)
                        {
                            string matCode = ((string)codeArr.GetValue(m)).Trim();
                            Block mb = World.GetBlock(new AssetLocation(matCode));
                            matIds.SetValue(mb != null ? mb.BlockId : chiselBlk.BlockId, m);
                        }
                        int cubCount = rawCubs.Length / 4;
                        var cubList = new System.Collections.Generic.List<uint>(cubCount);
                        for (int c = 0; c != cubCount; c++) cubList.Add(System.BitConverter.ToUInt32(rawCubs, c * 4));

                        beChisel.BlockIds = matIds;
                        beChisel.VoxelCuboids = cubList;
                        var tree = new Vintagestory.API.Datastructures.TreeAttribute();
                        beChisel.ToTreeAttributes(tree);
                        beChisel.FromTreeAttributes(tree, World);
                        beChisel.RegenSelectionBoxes(World, spReb3);
                        beChisel.MarkDirty(true);
                        World.BlockAccessor.MarkBlockDirty(pos);
                    }
                }

                World.PlaySoundAt(new AssetLocation("game:sounds/block/chisel"), this, null, 1.0f, 16f, 0.9f);
                spReb3.SendMessage(0, "[RunicMechs] Резные блоки корпуса разложены на месте! Измените конструкцию и активируйте заново через чашу.", EnumChatType.Notification);
                Die(EnumDespawnReason.Removed);
                return;
            }
            if (packetid == 9912 && data != null && data.Length >= 14 && player != null && WatchedAttributes.GetLong("mech_pilot_id", 0) == player.Entity?.EntityId)
            {
                using (var ms = new MemoryStream(data))
                using (var br = new BinaryReader(ms))
                {
                    float fwd = br.ReadSingle();
                    float strafe = br.ReadSingle();
                    float yaw = br.ReadSingle();
                    bool sprint = br.ReadBoolean();
                    bool dismount = br.ReadBoolean();
                    if (dismount && player.Entity is EntityPlayer ep) { DismountPilot(ep); return; }
                    netFwd = GameMath.Clamp(fwd, -1f, 1f);
                    netStrafe = GameMath.Clamp(strafe, -1f, 1f);
                    netYaw = yaw;
                    netSprint = sprint;
                    hasNetInput = true;
                }
                return;
            }
            base.OnReceivedClientPacket(player, packetid, data);
        }
    }
}
