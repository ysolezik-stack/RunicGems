using System;
using System.Collections;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public partial class MechLimbIkRenderer
    {
        private static readonly Hashtable liveChainsCache = new Hashtable();

        public static ArrayList GetLiveChainsForHost(long entityId) => liveChainsCache[entityId] as ArrayList;

        private void UpdateInertialStep(long hostId, ArrayList chains, int chainIdx, MechLegChain ch, Vec3d idealFoot, double groundRefY, bool isSpider, double velX, double velZ, float idleTime, float dt)
        {
            if (dt <= 0.0001f) return;
            if (dt > 0.15f) dt = 0.15f;

            double legLen = 0.0;
            foreach (MechSegmentNode sn in ch.Segments) legLen += sn.Length;

            double curSpeed = GameMath.Clamp(Math.Sqrt(velX * velX + velZ * velZ), 0.0, 12.0);
            double dx = ch.CurrentFootWorld.X - idealFoot.X;
            double dz = ch.CurrentFootWorld.Z - idealFoot.Z;
            double distErr = Math.Sqrt(dx * dx + dz * dz);

            if (distErr > Math.Max(4.5, legLen * 2.8) || Math.Abs(ch.CurrentFootWorld.Y - idealFoot.Y) > Math.Max(3.5, legLen * 1.6))
            {
                ch.CurrentFootWorld = idealFoot.Clone();
                ch.StepTargetWorld = idealFoot.Clone();
                ch.StepStartWorld = idealFoot.Clone();
                ch.StepProgress = 1.0f;
                return;
            }

            bool isIdleAlign = idleTime > 0.35f;
            double baseThresh = Math.Max(isSpider ? 0.68 : 0.64, legLen * (isSpider ? 0.42 : 0.40));
            double stepThresh = isIdleAlign ? 0.14 : Math.Max(0.38, baseThresh - Math.Min(0.18, curSpeed * 0.02));
            if (!isIdleAlign && !isSpider && ch.GaitGroup == 0) stepThresh *= 0.85;

            // Строгая последовательность: второй шаг начинается только после ПОЛНОГО завершения первого (StepProgress >= 1.0f)
            bool canStep = true;
            if (chains.Count >= 2)
            {
                int other = -1;
                foreach (MechLegChain och in chains)
                {
                    other++;
                    if (other == chainIdx) continue;
                    if (och.StepProgress < 1.0f && (!isSpider || isIdleAlign || och.GaitGroup != ch.GaitGroup))
                    {
                        canStep = false;
                        break;
                    }
                }
            }

            if (ch.StepProgress >= 1.0f && distErr > stepThresh && canStep)
            {
                ch.StepStartWorld = ch.CurrentFootWorld.Clone();
                if (isIdleAlign || curSpeed < 0.15)
                {
                    ch.StepTargetWorld = idealFoot.Clone();
                }
                else
                {
                    double dirX = velX / curSpeed;
                    double dirZ = velZ / curSpeed;
                    double leadDist = Math.Min(legLen * 0.95, (stepThresh * 0.75) + (curSpeed * 0.16));
                    ch.StepTargetWorld = new Vec3d(
                        idealFoot.X + dirX * leadDist,
                        idealFoot.Y,
                        idealFoot.Z + dirZ * leadDist
                    );
                }
                ch.StepTargetWorld.Y = FindGroundY(ch.StepTargetWorld.X, groundRefY, ch.StepTargetWorld.Z);
                ch.StepProgress = 0f;
            }

            if (ch.StepProgress < 1.0f)
            {
                float speedBoost = (float)Math.Min(4.8, curSpeed * 0.48);
                float stepSpeed = isIdleAlign ? 3.8f : ((isSpider ? 5.2f : 4.6f) + speedBoost);
                ch.StepProgress = Math.Min(1.0f, ch.StepProgress + dt * stepSpeed);
                float t = ch.StepProgress;
                float s = t * t * t * (t * (t * 6f - 15f) + 10f);
                double liftHeight = isIdleAlign ? Math.Max(0.16, legLen * 0.11) : Math.Max(isSpider ? 0.34 : 0.32, legLen * (isSpider ? 0.22 : 0.20));
                double lift = Math.Sin(t * Math.PI) * liftHeight;

                ch.CurrentFootWorld.X = ch.StepStartWorld.X + (ch.StepTargetWorld.X - ch.StepStartWorld.X) * s;
                ch.CurrentFootWorld.Z = ch.StepStartWorld.Z + (ch.StepTargetWorld.Z - ch.StepStartWorld.Z) * s;
                ch.CurrentFootWorld.Y = ch.StepStartWorld.Y + (ch.StepTargetWorld.Y - ch.StepStartWorld.Y) * s + lift;

                if (ch.StepProgress >= 1.0f)
                {
                    ch.CurrentFootWorld.Y = ch.StepTargetWorld.Y;
                    if (!isIdleAlign)
                    {
                        PlayMechFootstepSound(hostId, ch.StepTargetWorld, isSpider, curSpeed);
                    }
                }
            }
        }

        private double FindGroundY(double wx, double groundRefY, double wz)
        {
            int bx = (int)Math.Floor(wx), bz = (int)Math.Floor(wz), startY = (int)Math.Floor(groundRefY + 1.5);
            BlockPos tmpPos = new BlockPos(bx, startY, bz);
            for (int y = startY; y >= startY - 6; y--)
            {
                tmpPos.Y = y;
                Block b = capi.World.BlockAccessor.GetBlock(tmpPos);
                if (b != null && b.Id != 0 && b.CollisionBoxes != null && b.CollisionBoxes.Length > 0)
                {
                    Cuboidf box = (Cuboidf)b.CollisionBoxes.GetValue(0);
                    return y + box.Y2;
                }
            }
            return Math.Floor(groundRefY);
        }
    }
}
