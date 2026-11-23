using System;
using System.Collections;
using RunicGems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public partial class EntityRunicMechRenderer : EntityRenderer
    {
        private class MechPartMesh
        {
            public MultiTextureMeshRef Mesh;
            public float OffX, OffY, OffZ;
        }

        private readonly ArrayList partMeshes = new ArrayList();
        private readonly Hashtable blockMeshCache = new Hashtable();
        private readonly Matrixf mat = new Matrixf();
        private readonly Matrixf mvp = new Matrixf();
        private uint cachedPartsHash;
        private bool cachedPowered;
        private long lastSeatedPilotId;

        public EntityRunicMechRenderer(Entity entity, ICoreClientAPI api) : base(entity, api) { }

        private static uint ComputeHash(byte[] data, bool powered)
        {
            uint h = powered ? 2166136261u : 16777619u;
            for (int i = 0; i != data.Length; i++)
            {
                h ^= (byte)data.GetValue(i);
                h *= 16777619u;
            }
            return h == 0u ? 1u : h;
        }

        private MultiTextureMeshRef GetCachedBlockMesh(string blockCode)
        {
            if (string.IsNullOrEmpty(blockCode)) return null;
            if (blockMeshCache.ContainsKey(blockCode)) return blockMeshCache[blockCode] as MultiTextureMeshRef;

            Block blk = capi.World.GetBlock(new AssetLocation(blockCode));
            if (blk == null) { blockMeshCache[blockCode] = null; return null; }

            MeshData md = capi.TesselatorManager.GetDefaultBlockMesh(blk);
            MultiTextureMeshRef mRef = md != null ? capi.Render.UploadMultiTextureMesh(md) : null;
            blockMeshCache[blockCode] = mRef;
            return mRef;
        }

        public override void DoRender3DOpaque(float dt, bool isShadowPass)
        {
            EntityRunicMech mech = entity as EntityRunicMech;
            if (mech == null) return;

            Vec3d cam = capi.World.Player.Entity.CameraPos;
            double dSq = (mech.Pos.X - cam.X) * (mech.Pos.X - cam.X) + (mech.Pos.Z - cam.Z) * (mech.Pos.Z - cam.Z);
            if (isShadowPass && dSq > 48.0 * 48.0) return;

            byte[] partsBlob = mech.WatchedAttributes?.GetBytes("mech_parts_blob");
            if (partsBlob == null || partsBlob.Length == 0) return;

            bool isPowered = mech.WatchedAttributes.GetInt("oilMl", 0) > 0 && mech.WatchedAttributes.GetInt("curFrame", 4) != 4;
            uint h = ComputeHash(partsBlob, isPowered);
            if (h != cachedPartsHash || isPowered != cachedPowered || partMeshes.Count == 0)
            {
                ClearMeshes();
                BuildMultiBlockParts(partsBlob, isPowered);
                cachedPartsHash = h;
                cachedPowered = isPowered;
            }

            float lift = mech.GetCurrentLegLift();
            ArrayList liveChains = MechLimbIkRenderer.GetLiveChainsForHost(mech.EntityId);
            MechSwayState sway = MechBodySway.Compute(mech, liveChains);

            IRenderAPI rpi = capi.Render;
            IStandardShaderProgram stdProg = isShadowPass ? null : rpi.PreparedStandardShader((int)mech.Pos.X, (int)(mech.Pos.Y + lift + 0.5), (int)mech.Pos.Z);
            IShaderProgram prog = isShadowPass ? rpi.CurrentActiveShader : stdProg;
            if (stdProg != null)
            {
                stdProg.ViewMatrix = rpi.CameraMatrixOriginf;
                stdProg.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            }

            float yawAngle = mech.Pos.Yaw + mech.WatchedAttributes.GetFloat("mech_mesh_yaw_offset", GameMath.PI);

            for (int i = 0; i != partMeshes.Count; i++)
            {
                MechPartMesh item = partMeshes[i] as MechPartMesh;
                if (item?.Mesh == null) continue;

                mat.Identity();
                mat.Translate((float)(mech.Pos.X - cam.X), (float)(mech.Pos.Y + lift + sway.BobY - cam.Y), (float)(mech.Pos.Z - cam.Z));
                mat.RotateY(yawAngle);
                mat.RotateX(sway.PitchX);
                mat.RotateZ(sway.RollZ);
                mat.Translate(item.OffX, item.OffY, item.OffZ);

                if (stdProg != null) stdProg.ModelMatrix = mat.Values;
                else if (prog != null)
                {
                    mvp.Set(rpi.CurrentShadowProjectionMatrix).Mul(mat.Values);
                    prog.UniformMatrix("mvpMatrix", mvp.Values);
                }
                rpi.RenderMultiTextureMesh(item.Mesh, isShadowPass ? "tex2d" : "tex");
            }

            RenderMountedStorageAndCargo(prog, rpi, cam, mech, lift, sway, yawAngle, isShadowPass);

            if (stdProg != null) stdProg.Stop();
            if (isShadowPass) return;

            UpdateSeatedPilotTransform(mech, lift, sway, yawAngle);
        }
    }
}
