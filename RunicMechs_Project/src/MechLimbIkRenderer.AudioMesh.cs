using System.Collections;
using RunicGems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public partial class MechLimbIkRenderer
    {
        private static readonly Hashtable lastSoundTimeCache = new Hashtable();

        private void PlayMechFootstepSound(long hostId, Vec3d footPos, bool isSpider, double speed)
        {
            if (capi?.World == null || footPos == null) return;
            long nowMs = capi.World.ElapsedMilliseconds;
            long prevMs = lastSoundTimeCache[hostId] is long pm ? pm : 0L;
            long minIntervalMs = speed > 4.2 ? 220L : 300L;
            if (nowMs - prevMs < minIntervalMs) return;
            lastSoundTimeCache[hostId] = nowMs;

            float volume = isSpider ? 0.65f : 0.95f;
            float range = isSpider ? 22f : 32f;
            capi.World.PlaySoundAt(new AssetLocation("game:sounds/thud"), footPos.X, footPos.Y + 0.35, footPos.Z, null, true, range, volume);
        }

        private MultiTextureMeshRef GetSegMesh(byte[] blob, bool isPowered)
        {
            byte b0 = (byte)blob.GetValue(0);
            byte bLast = (byte)blob.GetValue(blob.Length - 1);
            int key = blob.Length ^ (b0 * 397) ^ (bLast * 17) ^ (isPowered ? 0x55AA : 0);
            if (meshCache.ContainsKey(key)) return meshCache[key] as MultiTextureMeshRef;

            GolemAnimSet set = GolemAnimCodec.Decompress(blob);
            if (set == null || set.BaseSlot == null || set.BaseSlot.IsEmpty) return null;

            string codes = (set.BottomCodes ?? "").Replace(isPowered ? "runicgems:inlaid-runiccrystal" : "runicgems:inlaid-runiccrystal-active", isPowered ? "runicgems:inlaid-runiccrystal-active" : "runicgems:inlaid-runiccrystal");
            string topCodes = (set.TopCodes ?? "").Replace(isPowered ? "runicgems:inlaid-runiccrystal" : "runicgems:inlaid-runiccrystal-active", isPowered ? "runicgems:inlaid-runiccrystal-active" : "runicgems:inlaid-runiccrystal");

            var attr = new TreeAttribute();
            if (set.BaseSlot.BottomCuboids != null && set.BaseSlot.BottomCuboids.Length > 0) { attr.SetBytes("cuboids", set.BaseSlot.BottomCuboids); attr.SetString("blockCodes", codes); }
            if (set.BaseSlot.TopCuboids != null && set.BaseSlot.TopCuboids.Length > 0) { attr.SetBytes("topCuboids", set.BaseSlot.TopCuboids); attr.SetString("topBlockCodes", topCodes); }
            attr.SetInt("minX", 0); attr.SetInt("maxX", 15); attr.SetInt("minY", 0); attr.SetInt("maxY", 15); attr.SetInt("minZ", 0); attr.SetInt("maxZ", 15);

            MeshData mesh = ItemGolemHead.BuildHeadMesh(capi, attr);
            if (mesh == null) return null;

            GuiDialogGolemAnimator.ApplyChiselWorldUV(mesh);
            mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0f, GameMath.PI, 0f);
            GuiDialogGolemAnimator.ApplyCleanFaceShading(mesh);

            MultiTextureMeshRef mRef = capi.Render.UploadMultiTextureMesh(mesh);
            meshCache[key] = mRef;
            return mRef;
        }
    }
}
