using Vintagestory.API.Common.Entities;
using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public partial class EntityRunicMech
    {
        private readonly Matrixf srvBodyMat = new Matrixf(); private bool netCrouch; private float currentCrouchMult = 1.0f;

        private bool IsSupportBelow(double yCheck)
        {
            double r = Math.Max(0.25, lastBoxW * 0.35);
            int by = (int)Math.Floor(yCheck);
            double[] offsets = new double[] { 0.0, -r, r };
            foreach (double ox in offsets)
                foreach (double oz in offsets)
                {
                    Block b = World.BlockAccessor.GetBlock(new BlockPos((int)Math.Floor(Pos.X + ox), by, (int)Math.Floor(Pos.Z + oz)));
                    if (b?.CollisionBoxes != null && b.CollisionBoxes.Length > 0) return true;
                }
            return false;
        }

        private void UpdatePiloting(EntityPlayer pilot, float dt)
        {
            if (dt <= 0.0001f || dt > 0.25f) return;

            if (World.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (Api as ICoreClientAPI) ?? (World.Api as ICoreClientAPI);
                if (capi != null && capi.World.Player?.Entity == pilot)
                {
                    bool[] keys = capi.Input.KeyboardKeyState;
                    bool w = keys != null && keys.Length > (int)GlKeys.W && keys[(int)GlKeys.W];
                    bool s = keys != null && keys.Length > (int)GlKeys.S && keys[(int)GlKeys.S];
                    bool a = keys != null && keys.Length > (int)GlKeys.A && keys[(int)GlKeys.A];
                    bool d = keys != null && keys.Length > (int)GlKeys.D && keys[(int)GlKeys.D];
                    bool sp = keys != null && keys.Length > (int)GlKeys.ShiftLeft && keys[(int)GlKeys.ShiftLeft];
                    bool jmp = keys != null && keys.Length > (int)GlKeys.Space && keys[(int)GlKeys.Space]; bool cr = keys != null && keys.Length > (int)GlKeys.ControlLeft && keys[(int)GlKeys.ControlLeft];
                    pilot.Controls.LeftMouseDown = false; bool grabKey = RunicMechsModSystem.IsMechLeftMouseHeld;
                    if (grabKey)
                    {

                        grabHoldTimer += dt;
                        if (grabHoldTimer >= 0.75f && !isArmAttacking)
                        {
                            isArmAttacking = true;
                            capi.Network.SendEntityPacket(EntityId, 9917, new byte[] { 1 });
                        }
                    }
                    else
                    {
                        if (wasGrabPressed && grabHoldTimer < 0.40f)
                        {
                            BlockSelection bs = capi.World.Player.CurrentBlockSelection;
                            if (bs == null) { BlockSelection bSel = null; EntitySelection eSel = null; capi.World.RayTraceForSelection(capi.World.Player.Entity.Pos.XYZ.Add(0, 1.5, 0), capi.World.Player.Entity.Pos.Yaw, capi.World.Player.Entity.Pos.Pitch, 8.0f, ref bSel, ref eSel); bs = bSel; }
                            BlockPos aim = bs?.Position;
                            using (var gms = new MemoryStream()) using (var gbw = new BinaryWriter(gms)) { gbw.Write(aim != null); if (aim != null) { gbw.Write(aim.X); gbw.Write(aim.Y); gbw.Write(aim.Z); } capi.Network.SendEntityPacket(EntityId, 9916, gms.ToArray()); }
                        }
                        if (isArmAttacking)
                        {
                            isArmAttacking = false;
                            capi.Network.SendEntityPacket(EntityId, 9917, new byte[] { 0 });
                        }
                        grabHoldTimer = 0f;
                    }
                    wasGrabPressed = grabKey;

                    float fwd = (w ? 1f : 0f) - (s ? 1f : 0f);
                    float strafe = (a ? 1f : 0f) - (d ? 1f : 0f);
                    using (var ms = new MemoryStream())
                    using (var bw = new BinaryWriter(ms))
                    {
                        bw.Write(fwd); bw.Write(strafe); bw.Write(pilot.Pos.Yaw); bw.Write(sp); bw.Write(jmp); bw.Write(cr);
                        capi.Network.SendEntityPacket(EntityId, 9912, ms.ToArray()); if (jmp) { pilot.Controls.FloorSitting = false; pilot.StopAnimation("sitflooridle"); pilot.SetCollisionBox(0.6f, 1.85f); pilot.SetSelectionBox(0.6f, 1.85f); }
                    }
                }
                return;
            }

            if (World.Side == EnumAppSide.Server)
            {
                pilot.WatchedAttributes.SetLong("mech_riding_id", EntityId);
                if (armAttackCooldown > 0f) armAttackCooldown -= dt;
                if (isArmAttacking && armAttackCooldown <= 0f && !WatchedAttributes.HasAttribute("mech_cargo_blob") && WatchedAttributes.GetInt("mech_arm_count", 0) >= 1)
                {
                    armAttackCooldown = 0.65f;
                    float reach = Math.Max(1.8f, WatchedAttributes.GetFloat("mech_arm_reach", 1.8f));
                    double hitX = Pos.X + Math.Sin(Pos.Yaw) * (reach * 0.75);
                    double hitY = Pos.Y + GetCurrentLegLift() + 0.8;
                    double hitZ = Pos.Z + Math.Cos(Pos.Yaw) * (reach * 0.75);
                    World.PlaySoundAt(new AssetLocation("game:sounds/player/strike"), hitX, hitY, hitZ, null, true, 18f, 0.9f);
                    Entity[] targets = World.GetEntitiesAround(new Vec3d(hitX, hitY, hitZ), reach * 0.65f, 2.2f, e => e != null && e.Alive && e.EntityId != EntityId && e.EntityId != pilot.EntityId && e is EntityAgent);
                    foreach (Entity t in targets)
                    {
                        t.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Entity, SourceEntity = this, Type = EnumDamageType.BluntAttack }, 2.0f); t.Pos.Motion.Y = 0.52; t.Pos.Motion.X += Math.Sin(Pos.Yaw) * 0.35; t.Pos.Motion.Z += Math.Cos(Pos.Yaw) * 0.35; t.OnGround = false;
                    }
                }
                float targetCrouch = (hasNetInput && netCrouch) ? 0.52f : 1.0f; currentCrouchMult += (targetCrouch - currentCrouchMult) * Math.Min(1.0f, dt * 8.0f); WatchedAttributes.SetFloat("mech_crouch_mult", currentCrouchMult); float targetYaw = hasNetInput ? netYaw : pilot.Pos.Yaw;
                Pos.Yaw += GameMath.Clamp(GameMath.AngleRadDistance(Pos.Yaw, targetYaw), -2.8f * dt, 2.8f * dt);
                #pragma warning disable 0618
                ServerPos.Yaw = Pos.Yaw;
                #pragma warning restore 0618

                double fwdIn = hasNetInput ? netFwd : 0.0;
                double sideIn = hasNetInput ? netStrafe : 0.0;
                bool isSprinting = hasNetInput && netSprint;
                double moveX = Math.Sin(Pos.Yaw) * fwdIn + Math.Cos(Pos.Yaw) * sideIn;
                double moveZ = Math.Cos(Pos.Yaw) * fwdIn - Math.Sin(Pos.Yaw) * sideIn;
                double len = Math.Sqrt(moveX * moveX + moveZ * moveZ);

                if (len > 0.01)
                {
                    moveX /= len; moveZ /= len;
                    WatchedAttributes.SetBool("mech_sprinting", isSprinting); WatchedAttributes.SetBool("mech_strafing", Math.Abs(sideIn) > 0.05);
                    currentSprintMult += ((isSprinting ? 2.15 : 1.0) - currentSprintMult) * Math.Min(1.0, dt * 6.5);
                    double swiftMult = GetActiveRunes().Contains("Swift") ? 1.28 : 1.0; double speed = GameMath.Clamp(lastBoxH * 1.55, 2.6, 5.6) * currentSprintMult * swiftMult;
                    double step = speed * dt;
                    double checkR = lastBoxW * 0.55 + 0.35;
                    int baseX = (int)Math.Floor(Pos.X + moveX * checkR);
                    int baseY = (int)Math.Floor(Pos.Y + 0.05);
                    int baseZ = (int)Math.Floor(Pos.Z + moveZ * checkR);
                    int bodyClearance = Math.Max(1, WatchedAttributes.GetInt("mech_dimY", 1));

                    int foundStepUp = -1;
                    for (int h = 0; h <= 3; h++)
                    {
                        bool blocked = false;
                        for (int cy = 0; cy < bodyClearance; cy++)
                        {
                            Block b = World.BlockAccessor.GetBlock(new BlockPos(baseX, baseY + h + cy, baseZ));
                            if (b?.CollisionBoxes != null && b.CollisionBoxes.Length > 0) { blocked = true; break; }
                        }
                        if (!blocked) { foundStepUp = h; break; }
                    }

                    if (foundStepUp >= 0)
                    {
                        double nx = Pos.X + moveX * step, nz = Pos.Z + moveZ * step;
                        double ny = foundStepUp > 0 ? Math.Min(baseY + foundStepUp, Pos.Y + Math.Max(step * 2.4, 7.5 * dt)) : Pos.Y;
                        if (foundStepUp > 0) verticalVel = 0.0;
                        Pos.SetPos(nx, ny, nz);
                        #pragma warning disable 0618
                        ServerPos.SetPos(nx, ny, nz);
                        #pragma warning restore 0618
                    }
                }
                else
                {
                    currentSprintMult += (1.0 - currentSprintMult) * Math.Min(1.0, dt * 8.0);
                    WatchedAttributes.SetBool("mech_sprinting", false); WatchedAttributes.SetBool("mech_strafing", false);
                }

                int checkGy = (int)Math.Floor(Pos.Y - 0.05);
                if (!IsSupportBelow(Pos.Y - 0.05) || (Pos.Y - (checkGy + 1.0)) > 0.08)
                {
                    verticalVel = Math.Max(-35.0, verticalVel - 28.0 * dt);
                    double nextY = Pos.Y + verticalVel * dt;
                    int ly = (int)Math.Floor(nextY);
                    if (IsSupportBelow(nextY) && nextY <= ly + 1.0) { nextY = ly + 1.0; verticalVel = 0.0; }
                    Pos.SetPos(Pos.X, nextY, Pos.Z);
                    #pragma warning disable 0618
                    ServerPos.SetPos(Pos.X, nextY, Pos.Z);
                    #pragma warning restore 0618
                }
                else verticalVel = 0.0;

                Pos.Motion.Set(0, 0, 0);
                anchorX = Pos.X; anchorZ = Pos.Z;

                float slx = WatchedAttributes.GetFloat("mech_seat_lx", 0f);
                float sly = WatchedAttributes.GetFloat("mech_seat_ly", -1f);
                float slz = WatchedAttributes.GetFloat("mech_seat_lz", 0f);
                int dimY = Math.Max(1, WatchedAttributes.GetInt("mech_dimY", 1));
                float lift = GetCurrentLegLift();
                float localY = sly >= 0f ? sly : (float)dimY;

                srvBodyMat.Identity();
                srvBodyMat.RotateY(Pos.Yaw + WatchedAttributes.GetFloat("mech_mesh_yaw_offset", GameMath.PI));
                float[] vOut = Mat4f.MulWithVec4(srvBodyMat.Values, new float[] { slx, localY, slz, 1f });

                double wx = Pos.X + (float)vOut.GetValue(0);
                double wy = Pos.Y + lift + (float)vOut.GetValue(1);
                double wz = Pos.Z + (float)vOut.GetValue(2);

                pilot.Pos.SetPos(wx, wy, wz);
                pilot.Pos.Motion.Set(0, 0, 0);
                pilot.OnGround = true;
                #pragma warning disable 0618
                // no server jitter
                #pragma warning restore 0618
            }
        }
    }
}
