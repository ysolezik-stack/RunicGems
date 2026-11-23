using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public partial class MechLimbIkRenderer
    {
        private void DrawSolvedArmSegments(IStandardShaderProgram prog, IRenderAPI rpi, Vec3d cam, MechLegChain ch, Vec3d shoulderWorld, Vec3d wristTarget, Vec3d handDir, float[] lengths, double handLen, double rightX, double rightZ, double fwdX, double fwdZ, double sideSign, bool isPowered)
        {
            int segCount = ch.Segments.Count;
            Vec3d[] joints = new Vec3d[segCount + 1];
            Vec3d elbowPole = new Vec3d(rightX * (sideSign * 0.95) - fwdX * 0.75, 0.65, rightZ * (sideSign * 0.95) - fwdZ * 0.75);

            if (segCount >= 3)
            {
                float[] armUpperLens = new float[] { (float)lengths.GetValue(0), (float)lengths.GetValue(1) };
                Vec3d[] upperJoints = FabrikSolver.SolveChain(shoulderWorld, wristTarget, armUpperLens, elbowPole, null);
                joints.SetValue(upperJoints.GetValue(0), 0);
                joints.SetValue(upperJoints.GetValue(1), 1);
                joints.SetValue(upperJoints.GetValue(2), 2);
                Vec3d actualWrist = (Vec3d)upperJoints.GetValue(2);
                joints.SetValue(new Vec3d(actualWrist.X + handDir.X * handLen, actualWrist.Y + handDir.Y * handLen, actualWrist.Z + handDir.Z * handLen), 3);
            }
            else
            {
                Vec3d[] rawJoints = FabrikSolver.SolveChain(shoulderWorld, wristTarget, lengths, elbowPole, null);
                for (int j = 0; j < rawJoints.Length; j++) joints.SetValue(rawJoints.GetValue(j), j);
            }

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
                if (axisLen > 0.0001)
                {
                    axis.Normalize();
                    float[] axisArr = new float[] { (float)axis.X, (float)axis.Y, (float)axis.Z };
                    Mat4f.Rotate(mat.Values, mat.Values, angle, axisArr);
                }
                else if (dot < 0) mat.RotateX(GameMath.PI);

                mat.Scale(scale, fitScale, scale);
                mat.Translate(-topX, -topY, -topZ);

                RenderLimbSegmentMesh(prog, rpi, meshRef, mat);
            }
        }
    }
}
