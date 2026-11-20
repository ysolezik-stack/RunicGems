using System;
using System.Collections;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
namespace RunicGems
{
public class LightningBoltPacket
{
public double X1, Y1, Z1, X2, Y2, Z2;
public byte[] ToBytes()
{
using (MemoryStream ms = new MemoryStream())
using (BinaryWriter bw = new BinaryWriter(ms))
{
bw.Write(X1); bw.Write(Y1); bw.Write(Z1);
bw.Write(X2); bw.Write(Y2); bw.Write(Z2);
return ms.ToArray();
}
}
public static LightningBoltPacket FromBytes(byte[] data)
{
using (MemoryStream ms = new MemoryStream(data))
using (BinaryReader br = new BinaryReader(ms))
{
return new LightningBoltPacket()
{
X1 = br.ReadDouble(), Y1 = br.ReadDouble(), Z1 = br.ReadDouble(),
X2 = br.ReadDouble(), Y2 = br.ReadDouble(), Z2 = br.ReadDouble()
};
}
}
}
public class ActiveBolt3D
{
public MeshRef Mesh;
public Vec3d Origin;
public float Age = 0f;
public float MaxAge = 0.45f;
}
public class LightningLineRenderer : IRenderer
{
private readonly ICoreClientAPI capi;
private readonly ArrayList bolts = new ArrayList();
private readonly Random rand = new Random();
private readonly Matrixf modelMat = new Matrixf();
public double RenderOrder => 0.5;
public int RenderRange => 64;
public LightningLineRenderer(ICoreClientAPI api)
{
capi = api;
capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "runicgems-lightning");
}
public void AddBolt(Vec3d from, Vec3d to)
{
Vec3d d = to.SubCopy(from);
Vec3d p0 = new Vec3d(0, 0, 0);
Vec3d p3 = d;
Vec3d p1 = new Vec3d(d.X * 0.33 + (rand.NextDouble() - 0.5) * 0.45, d.Y * 0.33 + 0.25, d.Z * 0.33 + (rand.NextDouble() - 0.5) * 0.45);
Vec3d p2 = new Vec3d(d.X * 0.66 + (rand.NextDouble() - 0.5) * 0.45, d.Y * 0.66 - 0.15, d.Z * 0.66 + (rand.NextDouble() - 0.5) * 0.45);
MeshData mesh = new MeshData(24, 36, false, false, true, false);
AddThickSegment(mesh, p0, p1, 0.045f);
AddThickSegment(mesh, p1, p2, 0.045f);
AddThickSegment(mesh, p2, p3, 0.045f);
MeshRef meshRef = capi.Render.UploadMesh(mesh);
bolts.Add(new ActiveBolt3D()
{
Mesh = meshRef,
Origin = from.Clone()
});
}
private void AddThickSegment(MeshData mesh, Vec3d a, Vec3d b, float r)
{
Vec3f dir = new Vec3f((float)(b.X - a.X), (float)(b.Y - a.Y), (float)(b.Z - a.Z));
if (dir.Length() < 0.001f) return;
dir.Normalize();
Vec3f up = Math.Abs(dir.Y) < 0.9f ? new Vec3f(0, 1, 0) : new Vec3f(1, 0, 0);
Vec3f side1 = dir.Cross(up).Normalize();
Vec3f side2 = dir.Cross(side1).Normalize();
AddRibbonQuad(mesh, a, b, side1 * r);
AddRibbonQuad(mesh, a, b, side2 * r);
}
private void AddRibbonQuad(MeshData mesh, Vec3d a, Vec3d b, Vec3f off)
{
int baseIdx = mesh.VerticesCount;
mesh.AddVertex((float)a.X - off.X, (float)a.Y - off.Y, (float)a.Z - off.Z, 0, 0);
mesh.AddVertex((float)a.X + off.X, (float)a.Y + off.Y, (float)a.Z + off.Z, 1, 0);
mesh.AddVertex((float)b.X + off.X, (float)b.Y + off.Y, (float)b.Z + off.Z, 1, 1);
mesh.AddVertex((float)b.X - off.X, (float)b.Y - off.Y, (float)b.Z - off.Z, 0, 1);
mesh.AddIndex(baseIdx);
mesh.AddIndex(baseIdx + 1);
mesh.AddIndex(baseIdx + 2);
mesh.AddIndex(baseIdx);
mesh.AddIndex(baseIdx + 2);
mesh.AddIndex(baseIdx + 3);
mesh.AddIndex(baseIdx);
mesh.AddIndex(baseIdx + 2);
mesh.AddIndex(baseIdx + 1);
mesh.AddIndex(baseIdx);
mesh.AddIndex(baseIdx + 3);
mesh.AddIndex(baseIdx + 2);
}
public void OnRenderFrame(float dt, EnumRenderStage stage)
{
if (bolts.Count == 0) return;
IRenderAPI rpi = capi.Render;
Vec3d cam = capi.World.Player.Entity.CameraPos;
IStandardShaderProgram prog = rpi.StandardShader;
prog.Use();
prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
prog.RgbaFogIn = rpi.FogColor;
prog.FogMinIn = rpi.FogMin;
prog.FogDensityIn = rpi.FogDensity;
prog.ExtraGlow = 255;
prog.ViewMatrix = rpi.CameraMatrixOriginf;
prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
for (int i = bolts.Count - 1; i >= 0; i--)
{
ActiveBolt3D b = bolts[i] as ActiveBolt3D;
if (b == null) { bolts.RemoveAt(i); continue; }
b.Age += dt;
if (b.Age >= b.MaxAge)
{
b.Mesh?.Dispose();
bolts.RemoveAt(i);
continue;
}
float fade = 1f - (b.Age / b.MaxAge);
prog.RgbaTint = new Vec4f(0.45f + 0.55f * fade, 0.95f, 1f, fade);
modelMat.Identity();
modelMat.Translate((float)(b.Origin.X - cam.X), (float)(b.Origin.Y - cam.Y), (float)(b.Origin.Z - cam.Z));
prog.ModelMatrix = modelMat.Values;
rpi.RenderMesh(b.Mesh);
}
prog.ExtraGlow = 0;
prog.Stop();
}
public void Dispose()
{
for (int i = 0; i < bolts.Count; i++)
{
(bolts[i] as ActiveBolt3D)?.Mesh?.Dispose();
}
bolts.Clear();
}
}
}