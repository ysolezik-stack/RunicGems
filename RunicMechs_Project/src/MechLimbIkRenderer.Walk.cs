using System;
using System.Collections;
using RunicGems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public partial class MechLimbIkRenderer
    {
        private readonly Hashtable hostPrevPos = new Hashtable();
        private readonly Hashtable hostSmoothVel = new Hashtable();
        private readonly Hashtable hostIdleTimer = new Hashtable();

        private void RenderHostChains(IStandardShaderProgram prog, IRenderAPI rpi, Vec3d cam, EntityChiselGolem host, ArrayList chains, float dt)
        {
            string gaitMode = host.WatchedAttributes.GetString("mech_gait", "auto");
            bool isSpider = gaitMode == "spider" || (gaitMode == "auto" && chains.Count >= 3);
            bool isStrafing = host.WatchedAttributes.GetBool("mech_strafing", false);
            int frame = host.WatchedAttributes.GetInt("curFrame", 4);
            bool isPowered = host.WatchedAttributes.GetInt("oilMl", 0) > 0 && frame != 4;
            float hostScale = GameMath.Clamp(host.WatchedAttributes.GetFloat("golemScale", 1.0f), 0.5f, 1.25f);

            Vec3d prevPos = hostPrevPos[host.EntityId] as Vec3d;
            Vec3d vel = hostSmoothVel[host.EntityId] as Vec3d;
            if (prevPos == null || vel == null)
            {
                prevPos = new Vec3d(host.Pos.X, host.Pos.Y, host.Pos.Z);
                vel = new Vec3d(0, 0, 0);
                hostPrevPos[host.EntityId] = prevPos;
                hostSmoothVel[host.EntityId] = vel;
            }
            if (dt > 0.0005f && dt < 0.25f)
            {
                double ivx = (host.Pos.X - prevPos.X) / dt;
                double ivz = (host.Pos.Z - prevPos.Z) / dt;
                double alpha = Math.Min(1.0, dt * 8.0);
                vel.X += (ivx - vel.X) * alpha;
                vel.Z += (ivz - vel.Z) * alpha;

                double moveDist = Math.Sqrt((host.Pos.X - prevPos.X) * (host.Pos.X - prevPos.X) + (host.Pos.Z - prevPos.Z) * (host.Pos.Z - prevPos.Z));
                float idleT = hostIdleTimer[host.EntityId] is float ft ? ft : 0f;
                hostIdleTimer[host.EntityId] = moveDist < 0.003 ? (idleT + dt) : 0f;

                prevPos.Set(host.Pos.X, host.Pos.Y, host.Pos.Z);
            }
            float idleTime = hostIdleTimer[host.EntityId] is float it ? it : 0f;

            if (!isShadowPass) prog.RgbaLightIn = capi.World.BlockAccessor.GetLightRGBs((int)Math.Floor(host.Pos.X), (int)Math.Floor(host.Pos.Y + 0.5), (int)Math.Floor(host.Pos.Z));
            float bodyYaw = host.Pos.Yaw + host.WatchedAttributes.GetFloat("mech_mesh_yaw_offset", GameMath.PI);
            float cosY = (float)Math.Cos(bodyYaw);
            float sinY = (float)Math.Sin(bodyYaw);

            float maxLegLen = 0f;
            foreach (MechLegChain cLen in chains)
            {
                float sLen = 0f;
                if (cLen.IsArm) continue; foreach (MechSegmentNode sg in cLen.Segments) sLen += sg.Length;
                if (sLen > maxLegLen) maxLegLen = sLen;
            }
            double bodyLift = (host is EntityRunicMech) ? maxLegLen * (isSpider ? 0.36 : 0.62) : 0.0;
            liveChainsCache[host.EntityId] = chains;
            MechSwayState bodySway = MechBodySway.Compute(host, chains);

            int idx = -1; int armIdx = -1;
            foreach (MechLegChain ch in chains)
            {
                idx++;
                if (ch.Segments.Count == 0) continue; if (ch.IsArm) { armIdx++; double spd = Math.Sqrt(vel.X * vel.X + vel.Z * vel.Z); RenderArmChain(prog, rpi, cam, host, ch, armIdx, cosY, sinY, bodyLift, bodySway, spd, isPowered); continue; }

                bool hasRawParts = host.WatchedAttributes.HasAttribute("mech_parts_blob");
                double lx = hasRawParts ? ch.RootSocket.X : (ch.RootSocket.X - 0.5) * hostScale;
                double ly = hasRawParts ? ch.RootSocket.Y : ch.RootSocket.Y * hostScale;
                double lz = hasRawParts ? ch.RootSocket.Z : (ch.RootSocket.Z - 0.5) * hostScale;

                double baseHipX = host.Pos.X + (lx * cosY + lz * sinY);
                double baseHipZ = host.Pos.Z + (-lx * sinY + lz * cosY);

                double sy = ly + bodySway.BobY - (lx * Math.Sin(bodySway.RollZ)) + (lz * Math.Sin(bodySway.PitchX));
                Vec3d rootWorld = new Vec3d(baseHipX, host.Pos.Y + bodyLift + sy, baseHipZ);

                double sideSign = Math.Abs(lx) > 0.02 ? (lx > 0 ? 1.0 : -1.0) : ((idx % 2 == 0) ? -1.0 : 1.0);
                double rawNx = isSpider ? lx : sideSign;
                double rawNz = isSpider ? lz : 0.0;
                if (Math.Abs(rawNx) + Math.Abs(rawNz) < 0.02)
                {
                    rawNx = sideSign;
                    rawNz = (idx < chains.Count / 2) ? 0.5 : -0.5;
                }
                double nLen = Math.Sqrt(rawNx * rawNx + rawNz * rawNz);
                if (nLen > 0.001) { rawNx /= nLen; rawNz /= nLen; }

                double outX = rawNx * cosY + rawNz * sinY;
                double outZ = -rawNx * sinY + rawNz * cosY;
                double fwdX = Math.Sin(host.Pos.Yaw);
                double fwdZ = Math.Cos(host.Pos.Yaw);

                double chainLen = 0.0;
                foreach (MechSegmentNode sLenNode in ch.Segments) chainLen += sLenNode.Length;

                double dirX = outX;
                double dirZ = outZ;
                if (isSpider)
                {
                    double fanSign = (lz >= 0 ? 1.0 : -1.0) * (lx >= 0 ? -1.0 : 1.0);
                    double fanRad = 0.35 * fanSign;
                    double cF = Math.Cos(fanRad), sF = Math.Sin(fanRad);
                    dirX = outX * cF - outZ * sF;
                    dirZ = outX * sF + outZ * cF;
                }

                double spread = isSpider ? Math.Max(1.55, chainLen * 0.72) : 0.28;
                double idealX = baseHipX + dirX * spread;
                double idealZ = baseHipZ + dirZ * spread;
                Vec3d idealFoot = new Vec3d(idealX, FindGroundY(idealX, host.Pos.Y, idealZ), idealZ);

                int gaitGroup = isSpider ? (((lx < 0) == (lz >= 0)) ? 0 : 1) : (idx % 2);
                ch.GaitGroup = gaitGroup;

                if (!ch.InitDone)
                {
                    ch.CurrentFootWorld = idealFoot.Clone();
                    ch.StepTargetWorld = idealFoot.Clone();
                    ch.StepStartWorld = idealFoot.Clone();
                    ch.StepProgress = 1.0f;
                    ch.InitDone = true;
                }

                UpdateInertialStep(host.EntityId, chains, idx, ch, idealFoot, host.Pos.Y, isSpider, vel.X, vel.Z, idleTime, dt);
                SolveAndDrawChain(prog, rpi, cam, ch, rootWorld, fwdX, fwdZ, outX, outZ, dirX, dirZ, isSpider, isPowered, isStrafing);
            }
        }
    }
}
