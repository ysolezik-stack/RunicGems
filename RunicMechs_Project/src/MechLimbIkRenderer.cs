using System;
using System.Collections;
using RunicGems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public partial class MechLimbIkRenderer : IRenderer
    {
        private readonly ICoreClientAPI capi;
        private readonly Hashtable meshCache = new Hashtable();
        private readonly Hashtable activeChainsCache = new Hashtable();
        private readonly Matrixf mat = new Matrixf();
        private readonly Matrixf shadowMvp = new Matrixf();
        private float animTime;
        private bool isShadowPass;

        public double RenderOrder => 0.5;
        public int RenderRange => 64;

        public MechLimbIkRenderer(ICoreClientAPI capi) { this.capi = capi; }
        public void Dispose() { foreach (DictionaryEntry e in meshCache) (e.Value as MultiTextureMeshRef)?.Dispose(); meshCache.Clear(); activeChainsCache.Clear(); }

        private void RenderLimbSegmentMesh(IStandardShaderProgram prog, IRenderAPI rpi, MultiTextureMeshRef meshRef, Matrixf modelMat)
        {
            if (isShadowPass)
            {
                IShaderProgram sh = rpi.CurrentActiveShader;
                if (sh != null)
                {
                    shadowMvp.Set(rpi.CurrentShadowProjectionMatrix).Mul(modelMat.Values);
                    sh.UniformMatrix("mvpMatrix", shadowMvp.Values);
                    rpi.RenderMultiTextureMesh(meshRef, "tex2d");
                }
            }
            else
            {
                prog.ModelMatrix = modelMat.Values;
                rpi.RenderMultiTextureMesh(meshRef, "tex");
            }
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            EntityPlayer plr = capi.World.Player?.Entity;
            if (plr == null) return;

            isShadowPass = false; if (stage != EnumRenderStage.Opaque) return;
            if (!isShadowPass) animTime += dt;

            float scanRange = isShadowPass ? 36f : 48f;
            Entity[] near = capi.World.GetEntitiesAround(plr.Pos.XYZ, scanRange, 24f, e => e is EntityChiselGolem && e.WatchedAttributes.HasAttribute("mech_chains_v2"));
            if (near == null || near.Length == 0) return;

            IRenderAPI rpi = capi.Render;
            Vec3d cam = plr.CameraPos;
            IStandardShaderProgram prog = rpi.StandardShader;

            if (!isShadowPass)
            {
                prog.Use();
                if (rpi.CurrentActiveShader is IStandardShaderProgram stdProg) stdProg.RgbaTint = ColorUtil.WhiteArgbVec;
                prog.RgbaAmbientIn = rpi.AmbientColor;
                prog.RgbaFogIn = rpi.FogColor;
                prog.FogMinIn = rpi.FogMin;
                prog.FogDensityIn = rpi.FogDensity;
                prog.AlphaTest = 0.01f;
                prog.NormalShaded = 0;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            }

            for (int i = 0; i != near.Length; i++)
            {
                EntityChiselGolem host = near[i] as EntityChiselGolem;
                if (host == null || !host.Alive) continue;

                byte[] blob = host.WatchedAttributes.GetBytes("mech_chains_v2");
                if (blob == null || blob.Length == 0) continue;

                string key = host.EntityId + ":" + blob.Length + ":" + blob.GetValue(0) + ":" + blob.GetValue(blob.Length - 1);
                ArrayList chains = activeChainsCache[key] as ArrayList;
                if (chains == null)
                {
                    chains = MechChainCodec.Decode(blob);
                    activeChainsCache[key] = chains;
                }
                if (chains.Count == 0) continue;

                RenderHostChains(prog, rpi, cam, host, chains, isShadowPass ? 0f : dt);
            }

            if (!isShadowPass) prog.Stop();
        }
    }
}
