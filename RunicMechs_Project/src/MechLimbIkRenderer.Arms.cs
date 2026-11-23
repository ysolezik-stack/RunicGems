using System;
using RunicGems;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public partial class MechLimbIkRenderer
    {
        private void RenderArmChain(IStandardShaderProgram prog, IRenderAPI rpi, Vec3d cam, EntityChiselGolem host, MechLegChain ch, int armIdx, float cosY, float sinY, double bodyLift, MechSwayState bodySway, double curSpeed, bool isPowered)
        {
            int segCount = ch.Segments.Count;
            if (segCount == 0) return;

            bool hasRawParts = host.WatchedAttributes.HasAttribute("mech_parts_blob");
            float hostScale = GameMath.Clamp(host.WatchedAttributes.GetFloat("golemScale", 1.0f), 0.5f, 1.25f);

            double lx = hasRawParts ? ch.RootSocket.X : (ch.RootSocket.X - 0.5) * hostScale;
            double ly = hasRawParts ? ch.RootSocket.Y : ch.RootSocket.Y * hostScale;
            double lz = hasRawParts ? ch.RootSocket.Z : (ch.RootSocket.Z - 0.5) * hostScale;

            double sy = ly + bodySway.BobY - (lx * Math.Sin(bodySway.RollZ)) + (lz * Math.Sin(bodySway.PitchX));
            Vec3d shoulderWorld = new Vec3d(
                host.Pos.X + (lx * cosY + lz * sinY),
                host.Pos.Y + bodyLift + sy,
                host.Pos.Z + (-lx * sinY + lz * cosY)
            );

            double fwdX = Math.Sin(host.Pos.Yaw), fwdZ = Math.Cos(host.Pos.Yaw);
            double rightX = Math.Cos(host.Pos.Yaw), rightZ = -Math.Sin(host.Pos.Yaw);
            double sideSign = Math.Abs(lx) > 0.02 ? (lx > 0 ? 1.0 : -1.0) : ((armIdx % 2 == 0) ? -1.0 : 1.0);

            float[] lengths = new float[segCount];
            double totalArmLen = 0.0;
            int li = 0;
            foreach (MechSegmentNode sn in ch.Segments) { lengths.SetValue(sn.Length, li++); totalArmLen += sn.Length; }

            bool hasCargo = host.WatchedAttributes.HasAttribute("mech_cargo_blob");
            bool isDoubleCargo = host.WatchedAttributes.GetBool("mech_cargo_double", false);
            int dimX = Math.Max(1, host.WatchedAttributes.GetInt("mech_dimX", 1));

            double handLen = segCount >= 3 ? (float)lengths.GetValue(2) : 0.0;
            double upperArmTotal = Math.Max(0.25, totalArmLen - handLen);

            Vec3d wristTarget;
            Vec3d handDir;

            if (hasCargo)
            {
                double cargoFwd = dimX * 0.5 + 0.65;
                double gripHalfWidth = isDoubleCargo ? 0.88 : 0.48;
                Vec3d palmCenter = new Vec3d(
                    host.Pos.X + fwdX * cargoFwd + rightX * (sideSign * gripHalfWidth),
                    shoulderWorld.Y + 0.05,
                    host.Pos.Z + fwdZ * cargoFwd + rightZ * (sideSign * gripHalfWidth)
                );
                handDir = new Vec3d(fwdX - rightX * (sideSign * 0.25), -0.05, fwdZ - rightZ * (sideSign * 0.25));
                handDir.Normalize();
                wristTarget = new Vec3d(palmCenter.X - handDir.X * handLen, palmCenter.Y - handDir.Y * handLen, palmCenter.Z - handDir.Z * handLen);
            }
            else if (host.WatchedAttributes.GetBool("mech_arm_attack", false))
            {
                // Подбрасывающий удар снизу вверх (синхронный апперкот обеих рук!)
                double rawPhase = (capi.World.ElapsedMilliseconds * 0.0105) % (Math.PI * 2.0);
                double norm = rawPhase / (Math.PI * 2.0);
                double reachFwd, liftY, inward;

                if (norm < 0.36)
                {
                    // Фаза 1: Опускание и отведение назад для поддева
                    double t = norm / 0.36;
                    reachFwd = upperArmTotal * (0.28 - t * 0.12);
                    liftY = -0.22 - t * 0.35;
                    inward = sideSign * 0.18;
                }
                else if (norm < 0.65)
                {
                    // Фаза 2: Резкий выброс снизу вверх и вперёд (подбрасывание!)
                    double t = (norm - 0.36) / 0.29;
                    double snap = 1.0 - Math.Pow(1.0 - t, 3.0);
                    reachFwd = upperArmTotal * (0.16 + snap * 0.78);
                    liftY = -0.57 + snap * 1.15;
                    inward = sideSign * (0.18 - snap * 0.22);
                }
                else
                {
                    // Фаза 3: Возврат в сложенное положение
                    double t = (norm - 0.65) / 0.35;
                    reachFwd = upperArmTotal * (0.94 - t * 0.68);
                    liftY = 0.58 - t * 0.80;
                    inward = sideSign * (-0.04 + t * 0.16);
                }

                wristTarget = new Vec3d(
                    shoulderWorld.X + fwdX * reachFwd + rightX * inward,
                    Math.Max(host.Pos.Y + 0.15, shoulderWorld.Y + liftY),
                    shoulderWorld.Z + fwdZ * reachFwd + rightZ * inward
                );
                handDir = new Vec3d(fwdX, norm >= 0.36 && norm < 0.68 ? 0.65 : -0.2, fwdZ);
                handDir.Normalize();
            }
            else
            {
                // Научный манипулятор в покое: компактно поджат к передней стенке корпуса!
                double breath = Math.Sin(capi.World.ElapsedMilliseconds * 0.003 + sideSign) * 0.025;
                double foldedFwd = upperArmTotal * 0.26;
                double foldedDrop = upperArmTotal * 0.38;

                wristTarget = new Vec3d(
                    shoulderWorld.X + fwdX * foldedFwd + rightX * (sideSign * 0.08),
                    Math.Max(host.Pos.Y + 0.22, shoulderWorld.Y - foldedDrop + breath),
                    shoulderWorld.Z + fwdZ * foldedFwd + rightZ * (sideSign * 0.08)
                );
                // Кисти сложены перед грудью и смотрят вперёд-вниз на 45°
                handDir = new Vec3d(fwdX * 0.707 - rightX * (sideSign * 0.18), -0.707, fwdZ * 0.707 - rightZ * (sideSign * 0.18));
                handDir.Normalize();
            }

            DrawSolvedArmSegments(prog, rpi, cam, ch, shoulderWorld, wristTarget, handDir, lengths, handLen, rightX, rightZ, fwdX, fwdZ, sideSign, isPowered);
        }
    }
}
