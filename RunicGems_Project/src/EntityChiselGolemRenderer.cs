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
private float animTime;
public EntityChiselGolemRenderer(Entity entity, ICoreClientAPI api) : base(entity, api)
{
}
private MultiTextureMeshRef GetFrameMesh(int frameIdx, bool isPowered)
{
int cacheKey = frameIdx + (isPowered ? 20 : 0);
if (frameMeshes.ContainsKey(cacheKey))
return frameMeshes[cacheKey] as MultiTextureMeshRef;
byte[] raw = entity.WatchedAttributes?.GetBytes("f_cuboids_" + frameIdx);
string codes = entity.WatchedAttributes?.GetString("f_codes_" + frameIdx, "");
if (raw == null || raw.Length == 0)
{
if (frameIdx != 0) return GetFrameMesh(0, isPowered);
return null;
}
byte[] topRaw = entity.WatchedAttributes?.GetBytes("f_top_cuboids_" + frameIdx);
string topCodes = entity.WatchedAttributes?.GetString("f_top_codes_" + frameIdx, "");
if (isPowered)
{
codes = codes.Replace("runicgems:runeglass-normal", "runicgems:runeglass-active");
topCodes = topCodes.Replace("runicgems:runeglass-normal", "runicgems:runeglass-active");
}
else
{
codes = codes.Replace("runicgems:runeglass-active", "runicgems:runeglass-normal");
topCodes = topCodes.Replace("runicgems:runeglass-active", "runicgems:runeglass-normal");
}
var tmpAttr = new TreeAttribute();
tmpAttr.SetBytes("cuboids", raw);
tmpAttr.SetString("blockCodes", codes);
if (topRaw != null && topRaw.Length > 0)
{
tmpAttr.SetBytes("topCuboids", topRaw);
tmpAttr.SetString("topBlockCodes", topCodes);
}
tmpAttr.SetInt("minX", 0);
tmpAttr.SetInt("maxX", 15);
tmpAttr.SetInt("minY", 0);
tmpAttr.SetInt("maxY", 15);
tmpAttr.SetInt("minZ", 0);
tmpAttr.SetInt("maxZ", 15);
MeshData mesh = ItemGolemHead.BuildHeadMesh(capi, tmpAttr);
if (mesh == null) return null;
mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0f, GameMath.PI, 0f);
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
bool isPowered = oilMl > 0 && frame != 4;
MultiTextureMeshRef meshRef = GetFrameMesh(frame, isPowered);
if (meshRef == null) return;
if (!isShadowPass)
{
animTime += dt;
}
float bobY = 0f;
float rollZ = 0f;
if (isPowered && frame != 5)
{
if (frame == 1 || frame == 2)
{
float phase = animTime * 7.5f;
bobY = Math.Abs((float)Math.Sin(phase)) * 0.045f;
rollZ = (float)Math.Sin(phase) * 0.065f;
}
else
{
bobY = ((float)Math.Sin(animTime * 2.4f) + 1f) * 0.018f;
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
modelMat.Translate(0f, heightBlocks * 0.5f, 0f);
modelMat.RotateZ(rollZ);
modelMat.Translate(0f, -heightBlocks * 0.5f, 0f);
}
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
Vec4f light = capi.World.BlockAccessor.GetLightRGBs((int)entity.Pos.X, (int)entity.Pos.Y + 1, (int)entity.Pos.Z);
IStandardShaderProgram prog = rpi.StandardShader;
prog.Use();
prog.RgbaTint = ColorUtil.WhiteArgbVec;
prog.RgbaAmbientIn = rpi.AmbientColor;
prog.RgbaLightIn = light;
prog.RgbaFogIn = rpi.FogColor;
prog.FogMinIn = rpi.FogMin;
prog.FogDensityIn = rpi.FogDensity;
prog.AlphaTest = 0.01f;
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
float carryY = heightBlocks >= 2 ? 0.95f : 0.42f;
modelMat.Identity();
modelMat.Translate(x, y + carryY, z);
modelMat.RotateY(entity.Pos.Yaw);
modelMat.Translate(0f, 0f, 0.45f);
modelMat.Scale(0.38f, 0.38f, 0.38f);
modelMat.Translate(-0.5f, -0.5f, -0.5f);
prog.ModelMatrix = modelMat.Values;
rpi.RenderMultiTextureMesh(itemRef, "tex");
}
}
prog.Stop();
}
public override void Dispose()
{
foreach (DictionaryEntry entry in frameMeshes)
{
(entry.Value as MultiTextureMeshRef)?.Dispose();
}
frameMeshes.Clear();
foreach (DictionaryEntry entry in carriedMeshes)
{
(entry.Value as MultiTextureMeshRef)?.Dispose();
}
carriedMeshes.Clear();
}
}
}
