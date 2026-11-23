using System;
using RunicGems;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RunicMechs
{
    public partial class EntityRunicMech : EntityChiselGolem
    {
        private float lastBoxW = -1f;
        private float lastBoxH = -1f;
        private double anchorX;
        private double anchorZ;
        private bool hasAnchor;

        public override void OnGameTick(float dt)
        {
            long pilotId = WatchedAttributes.GetLong("mech_pilot_id", 0);
            EntityPlayer pilot = pilotId != 0 ? World.GetEntityById(pilotId) as EntityPlayer : null;

            base.OnGameTick(dt);

            if (pilot != null && pilot.Alive)
            {
                Controls.Forward = false;
                Controls.Jump = false;
                Controls.WalkVector.Set(0, 0, 0);
                Controls.StopAllMovement();

                UpdatePiloting(pilot, dt);
                UpdateMechHitbox();
                ApplySolidBodyCollision(pilot.EntityId);
                return;
            }
            else if (pilotId != 0 && World.Side == EnumAppSide.Server)
            {
                WatchedAttributes.SetLong("mech_pilot_id", 0);
                WatchedAttributes.SetBool("mech_sprinting", false);
                WatchedAttributes.SetBool("mech_strafing", false);
            }

            if (World.Side == EnumAppSide.Server && !OnGround)
            {
                Block under = World.BlockAccessor.GetBlock(new BlockPos((int)Math.Floor(Pos.X), (int)Math.Floor(Pos.Y - 0.1), (int)Math.Floor(Pos.Z)));
                if (under == null || under.CollisionBoxes == null || under.CollisionBoxes.Length == 0)
                {
                    Pos.Motion.Y = Math.Max(-2.4, Pos.Motion.Y - 2.2 * dt);
                    #pragma warning disable 0618
                    ServerPos.Motion.Y = Pos.Motion.Y;
                    #pragma warning restore 0618
                }
            }

            UpdateMechHitbox();
            ApplySolidBodyCollision(0);
            if (World.Side == EnumAppSide.Server) ApplyFootDrivenLocomotion(dt);
        }

        private bool IsHitNearMountedStorage(Vec3d hitPos)
        {
            int cnt = WatchedAttributes.GetInt("mounted_storage_count", 0);
            if (cnt <= 0 || hitPos == null) return false;

            float lift = GetCurrentLegLift();
            float yawAngle = Pos.Yaw + WatchedAttributes.GetFloat("mech_mesh_yaw_offset", GameMath.PI);
            float cosY = (float)Math.Cos(yawAngle);
            float sinY = (float)Math.Sin(yawAngle);

            for (int i = 0; i < cnt && i < 2; i++)
            {
                bool hasPt = WatchedAttributes.HasAttribute("mech_st_x_" + i);
                float lx = hasPt ? (WatchedAttributes.GetFloat("mech_st_x_" + i, 0f) + WatchedAttributes.GetFloat("mech_st_nx_" + i, 0f) * 0.36f) : ((i == 0 ? -1f : 1f) * (WatchedAttributes.GetInt("mech_dimX", 1) * 0.5f + 0.35f));
                float ly = hasPt ? (WatchedAttributes.GetFloat("mech_st_y_" + i, 0.35f) + WatchedAttributes.GetFloat("mech_st_ny_" + i, 0f) * 0.36f) : 0.65f;
                float lz = hasPt ? (WatchedAttributes.GetFloat("mech_st_z_" + i, 0f) + WatchedAttributes.GetFloat("mech_st_nz_" + i, 0f) * 0.36f) : -0.25f;

                double wx = Pos.X + (lx * cosY + lz * sinY);
                double wy = Pos.Y + lift + ly;
                double wz = Pos.Z + (-lx * sinY + lz * cosY);

                double dx = hitPos.X - wx, dy = hitPos.Y - wy, dz = hitPos.Z - wz;
                if (dx * dx + dy * dy + dz * dz <= 0.65 * 0.65) return true;
            }
            return false;
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (mode == EnumInteractMode.Interact)
            {
                string code = itemslot?.Itemstack?.Collectible?.Code?.ToString() ?? "";
                if (code == "runicmechs:mechactivator")
                {
                    if (World.Side == EnumAppSide.Server)
                    {
                        IServerPlayer sp = (byEntity as EntityPlayer)?.Player as IServerPlayer;
                        Api.ModLoader.GetModSystem<RunicMechsModSystem>()?.OpenAssemblyForPlayer(sp, this);
                    }
                    return;
                }

                int maxStore = GetActiveRunes().Contains("Storage") ? 2 : 0;
                if (maxStore > 0 && byEntity is EntityPlayer spPlr)
                {
                    IServerPlayer srvPlr = spPlr.Player as IServerPlayer;
                    if (itemslot?.Itemstack != null && GolemStorageHelper.IsAttachableStorage(itemslot.Itemstack))
                    {
                        if (World.Side == EnumAppSide.Server) GolemStorageHelper.TryInteractStorage(this, srvPlr, itemslot, maxStore);
                        return;
                    }
                    if ((itemslot == null || itemslot.Empty) && WatchedAttributes.GetInt("mounted_storage_count", 0) > 0)
                    {
                        if (byEntity.Controls.Sneak || IsHitNearMountedStorage(hitPosition))
                        {
                            if (World.Side == EnumAppSide.Server) GolemStorageHelper.TryInteractStorage(this, srvPlr, itemslot, maxStore);
                            return;
                        }
                    }
                }

                if (byEntity != null && byEntity.Controls.Sneak)
                {
                    if (World.Side == EnumAppSide.Server)
                    {
                        ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(0, "[RunicMechs] Меха нельзя подобрать. Разборка ног — Рунным ключом.", EnumChatType.Notification);
                    }
                    return;
                }

                if (code != "runicmechs:mechactivator" && !code.Contains("temporal") && byEntity is EntityPlayer plr)
                {
                    if (World.Side == EnumAppSide.Server)
                    {
                        long curPilot = WatchedAttributes.GetLong("mech_pilot_id", 0);
                        if (curPilot == 0 && plr.WatchedAttributes.GetLong("mech_riding_id", 0) == 0)
                        {
                            WatchedAttributes.SetLong("mech_pilot_id", plr.EntityId);
                            plr.WatchedAttributes.SetLong("mech_riding_id", EntityId);
                            (plr.Player as IServerPlayer)?.SendMessage(0, "[RunicMechs] Пилот в кресле! ЛКМ — взять/поставить груз, удержание ЛКМ — удар, Пробел — слезть.", EnumChatType.Notification);
                        }
                        else if (curPilot == plr.EntityId)
                        {
                            DismountPilot(plr);
                        }
                    }
                    return;
                }
            }

            base.OnInteract(byEntity, itemslot, hitPosition, mode);
        }

        public override string GetInfoText()
        {
            string mode = WatchedAttributes.GetString("mech_gait", "auto");
            string modeRu = mode == "spider" ? "Паук (Spider IK)" : (mode == "biped" ? "Двуногий шагоход (Biped IK)" : "Авто");
            string runes = GetActiveRunes();
            string runeInfo = string.IsNullOrEmpty(runes) ? "Нет" : runes;
            float reach = WatchedAttributes.GetFloat("mech_arm_reach", 0f);
            string armInfo = reach > 0.1f ? $"\nДальность рук: {reach:F2} бл. (ЛКМ: взять/поставить, зажать ЛКМ: удар)" : "";
            return base.GetInfoText() + "\n[Мех] Походка: " + modeRu + "\nАктивные руны: " + runeInfo + armInfo + "\nПКМ по кувшину / Shift+ПКМ: Открыть хранилище\nПКМ по ядру: Сесть за управление";
        }
    }
}
