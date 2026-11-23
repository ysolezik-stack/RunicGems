using System;
using System.Collections;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public class MechCameraAnchorRenderer : IRenderer
    {
        private readonly ICoreClientAPI capi;
        private readonly Matrixf bodyMat = new Matrixf();
        private bool wasPiloting;

        public MechCameraAnchorRenderer(ICoreClientAPI capi) { this.capi = capi; }
        public double RenderOrder => 0.05;
        public int RenderRange => 128;

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Before) return;
            EntityPlayer plr = capi.World?.Player?.Entity;
            if (plr == null) return;

            long mechId = plr.WatchedAttributes.GetLong("mech_riding_id", 0);
            if (mechId == 0)
            {
                foreach (Entity e in capi.World.LoadedEntities.Values)
                {
                    if (e is EntityRunicMech m && m.WatchedAttributes.GetLong("mech_pilot_id", 0) == plr.EntityId) { mechId = m.EntityId; break; }
                }
            }

            if (mechId == 0)
            {
                if (wasPiloting)
                {
                    plr.SetCollisionBox(0.6f, 1.85f);
                    plr.Controls.FloorSitting = false;
                    wasPiloting = false;
                }
                return;
            }

            EntityRunicMech mech = capi.World.GetEntityById(mechId) as EntityRunicMech;
            if (mech == null || !mech.Alive) return;
            wasPiloting = true;

            float slx = mech.WatchedAttributes.GetFloat("mech_seat_lx", 0f);
            float sly = mech.WatchedAttributes.GetFloat("mech_seat_ly", -1f);
            float slz = mech.WatchedAttributes.GetFloat("mech_seat_lz", 0f);
            int dimY = Math.Max(1, mech.WatchedAttributes.GetInt("mech_dimY", 1));
            float lift = mech.GetCurrentLegLift();
            float localY = sly >= 0f ? sly : (float)dimY;

            ArrayList live = MechLimbIkRenderer.GetLiveChainsForHost(mech.EntityId);
            MechSwayState sway = MechBodySway.Compute(mech, live);

            bodyMat.Identity();
            bodyMat.RotateY(mech.Pos.Yaw + mech.WatchedAttributes.GetFloat("mech_mesh_yaw_offset", GameMath.PI));
            bodyMat.RotateX(sway.PitchX);
            bodyMat.RotateZ(sway.RollZ);
            bodyMat.Translate(slx, localY, slz);

            float[] vOut = Mat4f.MulWithVec4(bodyMat.Values, new float[] { 0f, 0f, 0f, 1f });

            double wx = mech.Pos.X + (float)vOut.GetValue(0);
            double wy = mech.Pos.Y + lift + sway.BobY + (float)vOut.GetValue(1);
            double wz = mech.Pos.Z + (float)vOut.GetValue(2);

            plr.Pos.SetPos(wx, wy, wz);
            plr.Pos.Motion.Set(0, 0, 0);
            plr.OnGround = true;
            plr.Controls.FloorSitting = true;
            plr.SetCollisionBox(0f, 0f);
            plr.BodyYaw = mech.Pos.Yaw;
            if (plr.AnimManager != null)
            {
                if (plr.AnimManager.IsAnimationActive("sitfloorend")) plr.StopAnimation("sitfloorend");
                if (!plr.AnimManager.IsAnimationActive("sitflooridle")) plr.StartAnimation("sitflooridle");
            }
        }

        public void Dispose() { }
    }
}
