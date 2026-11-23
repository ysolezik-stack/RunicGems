using System;
using System.Collections;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public partial class EntityRunicMech
    {
        private void ApplyFootDrivenLocomotion(float dt)
        {
            if (dt <= 0.0001f || dt > 0.25f) return;

            byte[] data = WatchedAttributes.GetBytes("mech_chains_v2");
            if (data == null || data.Length == 0) { hasAnchor = false; return; }

            ArrayList chains = MechChainCodec.Decode(data);
            int legCount = 0;
            foreach (MechLegChain c in chains) if (!c.IsArm) legCount++;
            if (legCount == 0) { hasAnchor = false; return; }

            int frame = WatchedAttributes.GetInt("curFrame", 4);
            bool isPowered = WatchedAttributes.GetInt("oilMl", 0) > 0 && frame != 4;

            if (!hasAnchor)
            {
                anchorX = Pos.X;
                anchorZ = Pos.Z;
                hasAnchor = true;
            }

            if (!isPowered)
            {
                Pos.Motion.X = 0;
                Pos.Motion.Z = 0;
                Pos.X = anchorX;
                Pos.Z = anchorZ;
                #pragma warning disable 0618
                ServerPos.X = anchorX;
                ServerPos.Z = anchorZ;
                ServerPos.Motion.X = 0;
                ServerPos.Motion.Z = 0;
                #pragma warning restore 0618
                return;
            }

            anchorX = Pos.X;
            anchorZ = Pos.Z;
        }

        private void UpdateMechHitbox()
        {
            int dimX = Math.Max(1, WatchedAttributes.GetInt("mech_dimX", 1));
            int dimY = Math.Max(1, WatchedAttributes.GetInt("mech_dimY", 1));
            int dimZ = Math.Max(1, WatchedAttributes.GetInt("mech_dimZ", 1));

            float legLift = 0f;
            byte[] data = WatchedAttributes.GetBytes("mech_chains_v2");
            if (data != null && data.Length > 0)
            {
                ArrayList chains = MechChainCodec.Decode(data);
                int legCount = 0;
                float maxLen = 0f;
                foreach (MechLegChain ch in chains)
                {
                    if (ch.IsArm) continue;
                    legCount++;
                    float sum = 0f;
                    foreach (MechSegmentNode seg in ch.Segments) sum += seg.Length;
                    if (sum > maxLen) maxLen = sum;
                }
                if (legCount > 0)
                {
                    string gaitMode = WatchedAttributes.GetString("mech_gait", "auto");
                    bool isSpider = gaitMode == "spider" || (gaitMode == "auto" && legCount >= 3);
                    legLift = maxLen * (isSpider ? 0.36f : 0.62f);
                }
            }

            float w = Math.Max(dimX, dimZ) * 0.92f;
            float h = dimY + legLift;

            if (Math.Abs(w - lastBoxW) > 0.05f || Math.Abs(h - lastBoxH) > 0.05f)
            {
                lastBoxW = w;
                lastBoxH = h;
                SetCollisionBox(w, h);
                SetSelectionBox(w, h);
            }
        }

        private void ApplySolidBodyCollision(long ignoreEntityId)
        {
            if (lastBoxW <= 0.1f || lastBoxH <= 0.1f) return;
            double halfW = lastBoxW * 0.5;
            double minY = Pos.Y;
            double maxY = Pos.Y + lastBoxH;

            Entity[] near = World.GetEntitiesAround(Pos.XYZ, lastBoxW + 1.5f, lastBoxH + 1.5f, e => e != null && e.Alive && e.EntityId != EntityId && e.EntityId != ignoreEntityId && e is EntityAgent);
            foreach (Entity other in near)
            {
                double dx = other.Pos.X - Pos.X;
                double dz = other.Pos.Z - Pos.Z;
                double deckR = halfW + other.CollisionBox.X2 * 0.35;
                if (Math.Abs(dx) < deckR && Math.Abs(dz) < deckR && other.Pos.Y >= maxY - 0.35 && other.Pos.Y <= maxY + 0.2 && other.Pos.Motion.Y <= 0.05)
                {
                    other.Pos.Y = maxY;
                    other.Pos.Motion.Y = 0;
                    other.OnGround = true;
                    continue;
                }
                if (other.Pos.Y >= maxY - 0.1 || other.Pos.Y + other.CollisionBox.Y2 <= minY + 0.1) continue;

                
                double minDist = halfW + other.CollisionBox.X2 * 0.5;

                if (Math.Abs(dx) < minDist && Math.Abs(dz) < minDist)
                {
                    double penX = minDist - Math.Abs(dx);
                    double penZ = minDist - Math.Abs(dz);
                    if (penX < penZ)
                        other.Pos.X += (dx >= 0 ? penX : -penX);
                    else
                        other.Pos.Z += (dz >= 0 ? penZ : -penZ);
                }
            }
        }
    }
}
