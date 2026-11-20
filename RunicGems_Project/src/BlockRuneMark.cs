using System.Collections;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
namespace RunicGems
{
public class BlockRuneMark : Block
{
private static readonly Cuboidf[] EmptyBoxes = new Cuboidf[] { };
public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
{
return EmptyBoxes;
}
public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
{
return EmptyBoxes;
}
public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
{
if (api is not ICoreClientAPI capi) return;
string type = Variant["type"] ?? "point1";
var shape = BuildRuneShape(type);
capi.Tesselator.TesselateShape(this, shape, out MeshData customMesh);
if (customMesh != null) sourceMesh = customMesh;
}
private Shape BuildRuneShape(string type)
{
var elems = new ArrayList();
if (type == "point1")
{
elems.Add(MakeBar(7.2, 3.0, 8.8, 13.0));
}
else if (type == "point2")
{
elems.Add(MakeBar(5.2, 3.0, 6.6, 13.0));
elems.Add(MakeBar(9.4, 3.0, 10.8, 13.0));
}
else if (type == "point3")
{
elems.Add(MakeBar(4.0, 3.0, 5.2, 13.0));
elems.Add(MakeBar(7.4, 3.0, 8.6, 13.0));
elems.Add(MakeBar(10.8, 3.0, 12.0, 13.0));
}
else if (type == "pickup")
{
elems.Add(MakeBar(7.3, 4.0, 8.7, 13.0));
elems.Add(MakeBar(5.0, 4.5, 11.0, 5.8));
elems.Add(MakeBar(6.0, 3.2, 10.0, 4.5));
}
else
{
elems.Add(MakeBar(7.3, 3.0, 8.7, 12.0));
elems.Add(MakeBar(5.0, 10.2, 11.0, 11.5));
elems.Add(MakeBar(6.0, 11.5, 10.0, 12.8));
}
var arr = new ShapeElement[elems.Count];
elems.CopyTo(arr);
return new Shape { Elements = arr };
}
private ShapeElement MakeBar(double x1, double z1, double x2, double z2)
{
var el = new ShapeElement
{
From = new double[] { x1, 0.05, z1 },
To = new double[] { x2, 0.25, z2 }
};
var face = new ShapeElementFace { Texture = "all", Glow = 120 };
el.FacesResolved = new ShapeElementFace[] { face, face, face, face, face, face };
return el;
}
}
}