using System;
using System.Collections;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
namespace RunicGems
{
public class EntityChiselGolemRenderer : EntityRenderer
{
private readonly Hashtable frameMeshes = new Hashtable();
private readonly Hashtable carriedMeshes = new Hashtable();
private readonly Matrixf modelMat = new Matrixf();
private readonly Matrixf mvpMat = new Matrixf();
private uint cachedBlobHash;
private GolemAnimSet cachedSet;
private float animTime;
public EntityChiselGolemRenderer(Entity entity, ICoreClientAPI api) : base(entity, api)
{
}
                

        private static uint ComputeBytesHash(byte[] data)
{
uint h = 2166136261u;
for (int i = 0; i != data.Length; i++)
{
h ^= (byte)data.GetValue(i);
h *= 16777619u;
}
return h == 0u ? 1u : h;
}
private GolemAnimSet GetAnimSet()
{
byte[] blob = entity.WatchedAttributes?.GetBytes("animBlob");
if (blob == null || blob.Length == 0) return null;
uint sig = ComputeBytesHash(blob);
if (sig != cachedBlobHash || cachedSet == null)
{
DisposeFrameMeshes();
cachedSet = GolemAnimCodec.Decompress(blob);
cachedBlobHash = sig;
}
return cachedSet;
}
private int ResolveGlobalSlot(GolemAnimSet set, int legacyFrame)
{
int state = 0;
if (legacyFrame == 1 || legacyFrame == 2) state = 1;
else if (legacyFrame == 3) state = 2;
else if (legacyFrame == 5) state = 3;
else if (legacyFrame == 4) state = 4;
int start = (int)GolemAnimCodec.StateStartIndex.GetValue(state);
int count = (int)GolemAnimCodec.StateSlotCount.GetValue(state);
var valid = new ArrayList();
for (int i = 0; i != count; i++)
{
GolemAnimSlot s = set.GetSlot(start + i);
if (s != null && !s.IsEmpty) valid.Add(start + i);
}
if (valid.Count == 0) return 0;
if (valid.Count == 1) return (int)valid[0];
int fps = GameMath.Clamp(entity.WatchedAttributes.GetInt("animFps", set.AnimFps), 1, 20);
int step = (int)(animTime * fps) % valid.Count;
return (int)valid[step];
}
private MultiTextureMeshRef GetDynamicSlotMesh(GolemAnimSet set, int globalSlot, bool isPowered)
{
int cacheKey = globalSlot + (isPowered ? 100 : 0);
if (frameMeshes.ContainsKey(cacheKey))
return frameMeshes[cacheKey] as MultiTextureMeshRef;
GolemAnimSlot slot = set.GetSlot(globalSlot);
if (slot == null || slot.IsEmpty) slot = set.BaseSlot;
if (slot == null || slot.IsEmpty) return null;
string codes = set.BottomCodes ?? "";
string topCodes = set.TopCodes ?? "";
if (isPowered)
{
codes = codes.Replace("runicgems:inlaid-runiccrystal", "runicgems:inlaid-runiccrystal-active");
topCodes = topCodes.Replace("runicgems:inlaid-runiccrystal", "runicgems:inlaid-runiccrystal-active");
}
else
{
codes = codes.Replace("runicgems:inlaid-runiccrystal-active", "runicgems:inlaid-runiccrystal");
topCodes = topCodes.Replace("runicgems:inlaid-runiccrystal-active", "runicgems:inlaid-runiccrystal");
}
var tmpAttr = new TreeAttribute();
if (slot.BottomCuboids != null && slot.BottomCuboids.Length != 0)
{
tmpAttr.SetBytes("cuboids", slot.BottomCuboids);
tmpAttr.SetString("blockCodes", codes);
}
if (slot.TopCuboids != null && slot.TopCuboids.Length != 0)
{
tmpAttr.SetBytes("topCuboids", slot.TopCuboids);
tmpAttr.SetString("topBlockCodes", topCodes);
}
tmpAttr.SetInt("minX", 0); tmpAttr.SetInt("maxX", 15);
tmpAttr.SetInt("minY", 0); tmpAttr.SetInt("maxY", 15);
tmpAttr.SetInt("minZ", 0); tmpAttr.SetInt("maxZ", 15);
MeshData mesh = ItemGolemHead.BuildHeadMesh(capi, tmpAttr);
if (mesh == null) return null;
// Накладываем бесшовные чизл-текстуры ДО разворота модели!
GuiDialogGolemAnimator.ApplyChiselWorldUV(mesh);
mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0f, GameMath.PI, 0f);
            GuiDialogGolemAnimator.ApplyCleanFaceShading(mesh);
MultiTextureMeshRef meshRef = capi.Render.UploadMultiTextureMesh(mesh);
frameMeshes[cacheKey] = meshRef;
return meshRef;
}
private MultiTextureMeshRef GetLegacyFrameMesh(int frameIdx, bool isPowered)
{
int cacheKey = frameIdx + (isPowered ? 20 : 0);
if (frameMeshes.ContainsKey(cacheKey))
return frameMeshes[cacheKey] as MultiTextureMeshRef;
byte[] raw = entity.WatchedAttributes?.GetBytes("f_cuboids_" + frameIdx);
string codes = entity.WatchedAttributes?.GetString("f_codes_" + frameIdx, "");
if (raw == null || raw.Length == 0)
{
if (frameIdx != 0) return GetLegacyFrameMesh(0, isPowered);
return null;
}
byte[] topRaw = entity.WatchedAttributes?.GetBytes("f_top_cuboids_" + frameIdx);
string topCodes = entity.WatchedAttributes?.GetString("f_top_codes_" + frameIdx, "");
if (isPowered)
{
codes = codes.Replace("runicgems:inlaid-runiccrystal", "runicgems:inlaid-runiccrystal-active");
topCodes = topCodes.Replace("runicgems:inlaid-runiccrystal", "runicgems:inlaid-runiccrystal-active");
}
else
{
codes = codes.Replace("runicgems:inlaid-runiccrystal-active", "runicgems:inlaid-runiccrystal");
topCodes = topCodes.Replace("runicgems:inlaid-runiccrystal-active", "runicgems:inlaid-runiccrystal");
}
var tmpAttr = new TreeAttribute();
tmpAttr.SetBytes("cuboids", raw);
tmpAttr.SetString("blockCodes", codes);
if (topRaw != null && topRaw.Length != 0)
{
tmpAttr.SetBytes("topCuboids", topRaw);
tmpAttr.SetString("topBlockCodes", topCodes);
}
tmpAttr.SetInt("minX", 0); tmpAttr.SetInt("maxX", 15);
tmpAttr.SetInt("minY", 0); tmpAttr.SetInt("maxY", 15);
tmpAttr.SetInt("minZ", 0); tmpAttr.SetInt("maxZ", 15);
MeshData mesh = ItemGolemHead.BuildHeadMesh(capi, tmpAttr);
if (mesh == null) return null;
GuiDialogGolemAnimator.ApplyChiselWorldUV(mesh);
mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0f, GameMath.PI, 0f);
            GuiDialogGolemAnimator.ApplyCleanFaceShading(mesh);
MultiTextureMeshRef meshRef = capi.Render.UploadMultiTextureMesh(mesh);
frameMeshes[cacheKey] = meshRef;
return meshRef;
}
private MultiTextureMeshRef GetCarriedMesh(string code, string itemClass)
{
if (string.IsNullOrEmpty(code)) return null;
string key = itemClass + ":" + code;
if (carriedMeshes.ContainsKey(key))
return carriedMeshes[key] as MultiTextureMeshRef;
MeshData mesh = null;
AssetLocation loc = new AssetLocation(code);
if (itemClass == "block")
{
Block b = capi.World.GetBlock(loc);
if (b != null) capi.Tesselator.TesselateBlock(b, out mesh);
}
else
{
Item it = capi.World.GetItem(loc);
if (it != null) capi.Tesselator.TesselateItem(it, out mesh);
}
if (mesh == null) return null;
MultiTextureMeshRef meshRef = capi.Render.UploadMultiTextureMesh(mesh);
carriedMeshes[key] = meshRef;
return meshRef;
}
public override void DoRender3DOpaque(float dt, bool isShadowPass)
{
int frame = entity.WatchedAttributes.GetInt("curFrame", 4);
int oilMl = entity.WatchedAttributes.GetInt("oilMl", 0);
int heightBlocks = entity.WatchedAttributes.GetInt("heightBlocks", 1);
bool isCorrupted = entity.WatchedAttributes.GetBool("isCorrupted", false);
float scale = GameMath.Clamp(entity.WatchedAttributes.GetFloat("golemScale", 1.0f), 0.5f, 1.25f);
bool isPowered = (oilMl != 0 || isCorrupted) && frame != 4;
if (!isShadowPass) animTime += dt;
GolemAnimSet animSet = GetAnimSet();
MultiTextureMeshRef meshRef = animSet != null
? GetDynamicSlotMesh(animSet, ResolveGlobalSlot(animSet, frame), isPowered)
: GetLegacyFrameMesh(frame, isPowered);
if (meshRef == null) return;
float bobY = 0f;
float rollZ = 0f;
if (isPowered && frame != 5)
{
if (frame == 1 || frame == 2)
{
float phase = animTime * 7.5f;
bobY = Math.Abs((float)Math.Sin(phase)) * 0.045f * scale;
rollZ = (float)Math.Sin(phase) * 0.065f;
}
else
{
bobY = ((float)Math.Sin(animTime * 2.4f) + 1f) * 0.018f * scale;
}
}
IRenderAPI rpi = capi.Render;
Vec3d camPos = capi.World.Player.Entity.CameraPos;
float x = (float)(entity.Pos.X - camPos.X);
float y = (float)(entity.Pos.Y - camPos.Y) + bobY;
float z = (float)(entity.Pos.Z - camPos.Z);
modelMat.Identity();
modelMat.Translate(x, y, z);
modelMat.RotateY(entity.Pos.Yaw);
if (rollZ != 0f)
{
modelMat.Translate(0f, heightBlocks * 0.5f * scale, 0f);
modelMat.RotateZ(rollZ);
modelMat.Translate(0f, -heightBlocks * 0.5f * scale, 0f);
}
modelMat.Scale(scale, scale, scale);
modelMat.Translate(-0.5f, 0f, -0.5f);
if (isShadowPass)
{
IShaderProgram shadowProg = rpi.CurrentActiveShader;
if (shadowProg != null && shadowProg.HasUniform("mvpMatrix"))
{
mvpMat.Set(rpi.CurrentShadowProjectionMatrix).Mul(modelMat);
shadowProg.UniformMatrix("mvpMatrix", mvpMat.Values);
rpi.RenderMultiTextureMesh(meshRef, "tex2d");
}
return;
}
Vec4f light = capi.World.BlockAccessor.GetLightRGBs((int)Math.Floor(entity.Pos.X), (int)Math.Floor(entity.Pos.Y + 0.5), (int)Math.Floor(entity.Pos.Z));
IStandardShaderProgram prog = rpi.StandardShader;
prog.Use();
prog.RgbaTint = isCorrupted ? new Vec4f(1.0f, 0.58f, 0.32f, 1.0f) : ColorUtil.WhiteArgbVec;
prog.RgbaAmbientIn = rpi.AmbientColor;
prog.RgbaLightIn = light;
prog.RgbaFogIn = rpi.FogColor;
prog.FogMinIn = rpi.FogMin;
prog.FogDensityIn = rpi.FogDensity;
prog.AlphaTest = 0.01f;
            prog.NormalShaded = 0;
prog.ModelMatrix = modelMat.Values;
prog.ViewMatrix = rpi.CameraMatrixOriginf;
prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
rpi.RenderMultiTextureMesh(meshRef, "tex");
string carriedCode = entity.WatchedAttributes.GetString("carriedCode", "");
string carriedClass = entity.WatchedAttributes.GetString("carriedClass", "");
if (!string.IsNullOrEmpty(carriedCode))
{
MultiTextureMeshRef itemRef = GetCarriedMesh(carriedCode, carriedClass);
if (itemRef != null)
{
float carryY = (heightBlocks == 2 ? 0.95f : 0.42f) * scale;
modelMat.Identity();
modelMat.Translate(x, y + carryY, z);
modelMat.RotateY(entity.Pos.Yaw);
modelMat.Translate(0f, 0f, 0.45f * scale);
modelMat.Scale(0.38f * scale, 0.38f * scale, 0.38f * scale);
modelMat.Translate(-0.5f, -0.5f, -0.5f);
prog.ModelMatrix = modelMat.Values;
rpi.RenderMultiTextureMesh(itemRef, "tex");
}
}
int mCnt = entity.WatchedAttributes.GetInt("mounted_storage_count", 0);
            if (mCnt > 0)
            {
                string mCode = entity.GetType().Name == "EntityChiselGolem" ? entity.WatchedAttributes.GetString("mounted_storage_code_0", "") : "";
                Block mb = !string.IsNullOrEmpty(mCode) ? capi.World.GetBlock(new AssetLocation(mCode)) : null;
                if (mb != null)
                {
                    MeshData mm = capi.TesselatorManager.GetDefaultBlockMesh(mb);
                    if (mm != null)
                    {
                        MultiTextureMeshRef mr = capi.Render.UploadMultiTextureMesh(mm);
                        Vec3d cam = capi.World.Player.Entity.CameraPos; Matrixf sm = new Matrixf();
                        sm.Translate((float)(entity.Pos.X - cam.X), (float)(entity.Pos.Y - cam.Y), (float)(entity.Pos.Z - cam.Z));
                        sm.RotateY(entity.Pos.Yaw);
                        float stX = entity.WatchedAttributes.HasAttribute("golem_st_x") ? (entity.WatchedAttributes.GetFloat("golem_st_x", 0f) + entity.WatchedAttributes.GetFloat("golem_st_nx", 0f) * 0.28f) : 0f; float stY = entity.WatchedAttributes.HasAttribute("golem_st_y") ? (entity.WatchedAttributes.GetFloat("golem_st_y", 0.45f) + entity.WatchedAttributes.GetFloat("golem_st_ny", 0f) * 0.28f - 0.18f) : 0.45f; float stZ = entity.WatchedAttributes.HasAttribute("golem_st_z") ? (entity.WatchedAttributes.GetFloat("golem_st_z", -0.42f) + entity.WatchedAttributes.GetFloat("golem_st_nz", 0f) * 0.28f) : -0.42f; sm.Translate(stX, stY, stZ);
                        sm.Scale(0.58f, 0.58f, 0.58f);
                        sm.Translate(-0.5f, 0f, -0.5f);
                        prog.ModelMatrix = sm.Values;
                        rpi.RenderMultiTextureMesh(mr, "tex");
                        mr?.Dispose();
                    }
                }
            }
            prog.Stop();
}
private void DisposeFrameMeshes()
{
foreach (DictionaryEntry entry in frameMeshes)
{
(entry.Value as MultiTextureMeshRef)?.Dispose();
}
frameMeshes.Clear();
}
public override void Dispose()
{
DisposeFrameMeshes();
foreach (DictionaryEntry entry in carriedMeshes)
{
(entry.Value as MultiTextureMeshRef)?.Dispose();
}
carriedMeshes.Clear();
}
}
}












