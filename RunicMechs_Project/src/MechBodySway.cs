using System;
using System.Collections;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public struct MechSwayState
    {
        public float BobY;
        public float PitchX;
        public float RollZ;
        public long LastMs;
    }

    public static class MechBodySway
    {
        private static readonly Dictionary<long, MechSwayState> smoothCache = new Dictionary<long, MechSwayState>();

        public static MechSwayState Compute(Entity host, ArrayList chains)
        {
            if (host == null || chains == null || chains.Count == 0) return default;

            long nowMs = host.World.ElapsedMilliseconds;
            smoothCache.TryGetValue(host.EntityId, out MechSwayState cur);
            float dt = cur.LastMs == 0 ? 0.016f : GameMath.Clamp((nowMs - cur.LastMs) * 0.001f, 0.001f, 0.05f);
            cur.LastMs = nowMs;

            float yaw = host.Pos.Yaw;
            double cosY = Math.Cos(yaw);
            double sinY = Math.Sin(yaw);

            double sumY = 0;
            int count = 0;
            double frontY = 0, frontW = 0, backY = 0, backW = 0;
            double leftY = 0, leftW = 0, rightY = 0, rightW = 0;

            foreach (MechLegChain ch in chains)
            {
                if (ch.CurrentFootWorld == null) continue;
                double dx = ch.CurrentFootWorld.X - host.Pos.X;
                double dz = ch.CurrentFootWorld.Z - host.Pos.Z;
                double fy = ch.CurrentFootWorld.Y;

                double localFwd = dx * sinY + dz * cosY;
                double localLeft = dx * cosY - dz * sinY;

                sumY += fy;
                count++;

                if (localFwd >= 0.05) { frontY += fy * localFwd; frontW += localFwd; }
                else if (localFwd <= -0.05) { double w = -localFwd; backY += fy * w; backW += w; }

                if (localLeft >= 0.05) { leftY += fy * localLeft; leftW += localLeft; }
                else if (localLeft <= -0.05) { double w = -localLeft; rightY += fy * w; rightW += w; }
            }

            float targetPitchX = 0f;
            float targetRollZ = 0f;
            float targetBob = 0f;

            if (count > 0)
            {
                double avgY = sumY / count;
                float rawBob = GameMath.Clamp((float)((avgY - host.Pos.Y) * 0.10), -0.12f, 0.12f);
                targetBob = Math.Abs(rawBob) < 0.015f ? 0f : rawBob;
            }

            if (frontW > 0.01 && backW > 0.01)
            {
                float rawPitch = GameMath.Clamp((float)Math.Atan2((frontY / frontW) - (backY / backW), 1.8) * 0.25f, -0.11f, 0.11f);
                targetPitchX = Math.Abs(rawPitch) < 0.012f ? 0f : rawPitch;
            }

            if (leftW > 0.01 && rightW > 0.01)
            {
                float rawRoll = GameMath.Clamp((float)Math.Atan2((rightY / rightW) - (leftY / leftW), 1.8) * 0.25f, -0.11f, 0.11f);
                targetRollZ = Math.Abs(rawRoll) < 0.012f ? 0f : rawRoll;
            }

            float alpha = 1f - (float)Math.Exp(-dt * 4.5);
            cur.BobY += (targetBob - cur.BobY) * alpha;
            cur.PitchX += (targetPitchX - cur.PitchX) * alpha;
            cur.RollZ += (targetRollZ - cur.RollZ) * alpha;

            if (Math.Abs(cur.BobY) < 0.002f && targetBob == 0f) cur.BobY = 0f;
            if (Math.Abs(cur.PitchX) < 0.002f && targetPitchX == 0f) cur.PitchX = 0f;
            if (Math.Abs(cur.RollZ) < 0.002f && targetRollZ == 0f) cur.RollZ = 0f;

            smoothCache[host.EntityId] = cur;
            return cur;
        }
    }
}
