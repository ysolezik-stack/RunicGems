using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public partial class MechLimbIkRenderer
    {
        private void SolveAndDrawChain(IStandardShaderProgram prog, IRenderAPI rpi, Vec3d cam, MechLegChain ch, Vec3d rootWorld, double fwdX, double fwdZ, double outX, double outZ, double dirX, double dirZ, bool isSpider, bool isPowered, bool isStrafing)
        {
            int segCount = ch.Segments.Count;
            float[] lengths = new float[segCount];
            int li = 0;
            foreach (MechSegmentNode sn in ch.Segments) lengths.SetValue(sn.Length, li++);

            float footLen = segCount >= 2 ? (float)lengths.GetValue(segCount - 1) : 0f;
            Vec3d ankleTarget = segCount >= 2
                ? new Vec3d(ch.CurrentFootWorld.X, ch.CurrentFootWorld.Y + footLen, ch.CurrentFootWorld.Z)
                : ch.CurrentFootWorld;

            float[] upperLengths = segCount >= 2 ? new float[segCount - 1] : lengths;
            if (segCount >= 2)
            {
                for (int u = 0; u < segCount - 1; u++) upperLengths.SetValue(lengths.GetValue(u), u);
            }

            Vec3d kneePole;
            Vec3d anklePole = null;

            if (isSpider)
            {
                kneePole = new Vec3d(dirX * 1.25, 1.55, dirZ * 1.25);
                if (segCount >= 3) anklePole = new Vec3d(dirX * 1.45, 0.35, dirZ * 1.45);
            }
            else if (segCount <= 2)
            {
                kneePole = new Vec3d(fwdX * 0.95 + outX * 0.15, 0.15, fwdZ * 0.95 + outZ * 0.15);
            }
            else
            {
                kneePole = new Vec3d(fwdX * 0.90 + outX * 0.15, 0.15, fwdZ * 0.90 + outZ * 0.15);
                anklePole = new Vec3d(-fwdX * 0.85 + outX * 0.12, -0.10, -fwdZ * 0.85 + outZ * 0.12);
            }

            Vec3d[] upperJoints = FabrikSolver.SolveChain(rootWorld, ankleTarget, upperLengths, kneePole, anklePole);
            Vec3d[] joints = new Vec3d[segCount + 1];
            for (int j = 0; j < upperJoints.Length; j++) joints.SetValue(upperJoints.GetValue(j), j);
            if (segCount >= 2) joints.SetValue(ch.CurrentFootWorld.Clone(), segCount);

            for (int s = 0; s < segCount; s++)
            {
                MechSegmentNode seg = (MechSegmentNode)ch.Segments[s];
                if (seg == null || seg.GolemBlob == null) continue;
                MultiTextureMeshRef meshRef = GetSegMesh(seg.GolemBlob, isPowered);
                if (meshRef == null) continue;

                Vec3d pStart = (Vec3d)joints.GetValue(s);
                Vec3d pEnd = (Vec3d)joints.GetValue(s + 1);
                Vec3d worldDir = pEnd.SubCopy(pStart);
                double segDist = worldDir.Length();
                if (segDist < 0.001) continue;
                worldDir.Normalize();

                float topX = seg.TopJoint != null ? (1f - seg.TopJoint.X) : 0.5f;
                float topY = seg.TopJoint != null ? seg.TopJoint.Y : 1.0f;
                float topZ = seg.TopJoint != null ? (1f - seg.TopJoint.Z) : 0.5f;

                float botX = seg.BottomJoint != null ? (1f - seg.BottomJoint.X) : 0.5f;
                float botY = seg.BottomJoint != null ? seg.BottomJoint.Y : 0.0f;
                float botZ = seg.BottomJoint != null ? (1f - seg.BottomJoint.Z) : 0.5f;

                Vec3d localBone = new Vec3d(botX - topX, botY - topY, botZ - topZ);
                double localLen = localBone.Length();
                if (localLen < 0.05) { localBone.Set(0, -1, 0); localLen = 1.0; }
                else localBone.Normalize();

                Vec3d axis = new Vec3d(
                    localBone.Y * worldDir.Z - localBone.Z * worldDir.Y,
                    localBone.Z * worldDir.X - localBone.X * worldDir.Z,
                    localBone.X * worldDir.Y - localBone.Y * worldDir.X
                );
                double axisLen = axis.Length();
                double dot = GameMath.Clamp(localBone.X * worldDir.X + localBone.Y * worldDir.Y + localBone.Z * worldDir.Z, -1.0, 1.0);
                float angle = (float)Math.Acos(dot);

                float scale = seg.Scale > 0.05f ? seg.Scale : 1.0f;
                float fitScale = (float)(segDist / localLen);

                Matrixf mat = new Matrixf();
                mat.Translate((float)(pStart.X - cam.X), (float)(pStart.Y - cam.Y), (float)(pStart.Z - cam.Z));

                bool isFootSeg = (segCount >= 2 && s == segCount - 1);
                if (isFootSeg && !isStrafing)
                {
                    float bodyYaw = (float)Math.Atan2(fwdX, fwdZ);
                    mat.RotateY(bodyYaw + GameMath.PI);
                }
                else if (axisLen > 0.0001)
                {
                    axis.Normalize();
                    float[] axisArr = new float[] { (float)axis.X, (float)axis.Y, (float)axis.Z };
                    Mat4f.Rotate(mat.Values, mat.Values, angle, axisArr);
                }
                else if (dot < 0)
                {
                    mat.RotateX(GameMath.PI);
                }
                mat.Scale(scale, fitScale, scale);
                mat.Translate(-topX, -topY, -topZ);

                RenderLimbSegmentMesh(prog, rpi, meshRef, mat);
            }
        }
    }
}
