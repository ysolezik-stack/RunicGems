using System.Collections;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public class JointPoint
    {
        public float X;
        public float Y;
        public float Z;
        public float Nx;
        public float Ny;
        public float Nz;
        public bool IsArm; public bool IsStorage;
    }

    public class MechSegmentNode
    {
        public byte[] GolemBlob;
        public float Scale = 1.0f;
        public JointPoint TopJoint;
        public JointPoint BottomJoint;
        public float Length = 0.65f;
    }

    public class MechLegChain
    {
        public int GaitGroup;
        public JointPoint RootSocket;
        public bool IsArm; public bool IsStorage;
        public readonly ArrayList Segments = new ArrayList();

        public Vec3d CurrentFootWorld;
        public Vec3d StepStartWorld;
        public Vec3d StepTargetWorld;
        public float StepProgress = 1.0f;
        public bool InitDone;
    }
}
