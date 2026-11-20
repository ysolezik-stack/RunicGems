using System;
using System.Collections;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
namespace RunicGems
{
public class ChiselFrameData
{
public bool IsValid;
public string ErrorKey;
public int CapacityMl;
public int HeightBlocks = 1;
public string RunesCsv = "";
public byte[] CuboidsBytes;
public string BlockCodes = "";
public byte[] TopCuboidsBytes;
public string TopBlockCodes = "";
}
public static class ChiselGolemGeometry
{
public static ChiselFrameData AnalyzeFrame(ICoreAPI api, BlockEntityChisel be, bool requireCavity)
{
return AnalyzeDoubleFrame(api, be, null, requireCavity);
}
public static ChiselFrameData AnalyzeDoubleFrame(ICoreAPI api, BlockEntityChisel bottomBe, BlockEntityChisel topBe, bool requireCavity)
{
var res = new ChiselFrameData();
if (bottomBe == null || bottomBe.VoxelCuboids == null || bottomBe.VoxelCuboids.Count == 0)
{
res.ErrorKey = "Статуэтка пуста!";
return res;
}
bool isTall = topBe != null && topBe.VoxelCuboids != null && topBe.VoxelCuboids.Count > 0;
res.HeightBlocks = isTall ? 2 : 1;
int maxYSize = isTall ? 32 : 16;
var grid = new ArrayList(16 * maxYSize * 16);
for (int i = 0; i < 16 * maxYSize * 16; i++) grid.Add(-1);
int minX = 16, minY = maxYSize, minZ = 16, maxX = -1, maxY = -1, maxZ = -1;
FillGridFromBe(bottomBe, grid, 0, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
if (isTall)
{
FillGridFromBe(topBe, grid, 16, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
}
if (requireCavity)
{
var visited = new ArrayList(16 * maxYSize * 16);
for (int i = 0; i < 16 * maxYSize * 16; i++) visited.Add(false);
var q = new Queue();
for (int x = minX; x <= maxX; x++)
for (int y = minY; y <= maxY; y++)
for (int z = minZ; z <= maxZ; z++)
{
if (x == minX || x == maxX || z == minZ || z == maxZ || y == minY || y == maxY)
{
int idx = x + (z << 4) + (y << 8);
if ((int)grid[idx] == -1 && !(bool)visited[idx]) { visited[idx] = true; q.Enqueue(idx); }
}
}
while (q.Count > 0)
{
int cur = (int)q.Dequeue();
int cx = cur & 0xF, cz = (cur >> 4) & 0xF, cy = cur >> 8;
TryStep(cx + 1, cy, cz, minX, maxX, minY, maxY, minZ, maxZ, grid, visited, q);
TryStep(cx - 1, cy, cz, minX, maxX, minY, maxY, minZ, maxZ, grid, visited, q);
TryStep(cx, cy + 1, cz, minX, maxX, minY, maxY, minZ, maxZ, grid, visited, q);
TryStep(cx, cy - 1, cz, minX, maxX, minY, maxY, minZ, maxZ, grid, visited, q);
TryStep(cx, cy, cz + 1, minX, maxX, minY, maxY, minZ, maxZ, grid, visited, q);
TryStep(cx, cy, cz - 1, minX, maxX, minY, maxY, minZ, maxZ, grid, visited, q);
}
int cavityVoxels = 0;
for (int x = minX; x <= maxX; x++)
for (int y = minY; y <= maxY; y++)
for (int z = minZ; z <= maxZ; z++)
{
int idx = x + (z << 4) + (y << 8);
if ((int)grid[idx] == -1 && !(bool)visited[idx]) cavityVoxels++;
}
if (cavityVoxels < 4)
{
res.ErrorKey = "Внутри базовой статуэтки нужна герметичная полость для масла (минимум 4 вокселя)!";
return res;
}
res.CapacityMl = cavityVoxels * 12;
}
res.IsValid = true;
var foundRunes = new ArrayList();
ScanRunesInBlock(api, bottomBe, foundRunes);
if (isTall) ScanRunesInBlock(api, topBe, foundRunes);
var rsb = new StringBuilder();
for (int i = 0; i < foundRunes.Count; i++)
{
if (i > 0) rsb.Append(",");
rsb.Append(foundRunes[i]);
}
res.RunesCsv = rsb.ToString();
res.CuboidsBytes = new byte[bottomBe.VoxelCuboids.Count * 4];
Buffer.BlockCopy(bottomBe.VoxelCuboids.ToArray(), 0, res.CuboidsBytes, 0, res.CuboidsBytes.Length);
res.BlockCodes = BuildCodesString(api, bottomBe);
if (isTall)
{
res.TopCuboidsBytes = new byte[topBe.VoxelCuboids.Count * 4];
Buffer.BlockCopy(topBe.VoxelCuboids.ToArray(), 0, res.TopCuboidsBytes, 0, res.TopCuboidsBytes.Length);
res.TopBlockCodes = BuildCodesString(api, topBe);
}
return res;
}
private static string BuildCodesString(ICoreAPI api, BlockEntityChisel be)
{
var sb = new StringBuilder();
for (int i = 0; i < be.BlockIds.Length; i++)
{
if (i > 0) sb.Append("|");
Block b = api.World.GetBlock(be.BlockIds[i]);
sb.Append(b != null ? b.Code.ToString() : "game:air");
}
return sb.ToString();
}
private static void FillGridFromBe(BlockEntityChisel be, ArrayList grid, int yOffset, ref int minX, ref int minY, ref int minZ, ref int maxX, ref int maxY, ref int maxZ)
{
foreach (uint c in be.VoxelCuboids)
{
int x1 = (int)(c & 0xF), y1 = (int)((c >> 4) & 0xF) + yOffset, z1 = (int)((c >> 8) & 0xF);
int x2 = (int)((c >> 12) & 0xF) + 1, y2 = (int)((c >> 16) & 0xF) + 1 + yOffset, z2 = (int)((c >> 20) & 0xF) + 1;
int mat = (int)((c >> 24) & 0xFF);
if (x1 < minX) minX = x1; if (y1 < minY) minY = y1; if (z1 < minZ) minZ = z1;
if (x2 - 1 > maxX) maxX = x2 - 1; if (y2 - 1 > maxY) maxY = y2 - 1; if (z2 - 1 > maxZ) maxZ = z2 - 1;
for (int x = x1; x < x2; x++)
for (int y = y1; y < y2; y++)
for (int z = z1; z < z2; z++)
grid[x + (z << 4) + (y << 8)] = mat;
}
}
private static void TryStep(int x, int y, int z, int minX, int maxX, int minY, int maxY, int minZ, int maxZ, ArrayList grid, ArrayList vis, Queue q)
{
if (x < minX || x > maxX || y < minY || y > maxY || z < minZ || z > maxZ) return;
int idx = x + (z << 4) + (y << 8);
if ((int)grid[idx] == -1 && !(bool)vis[idx]) { vis[idx] = true; q.Enqueue(idx); }
}
private static void ScanRunesInBlock(ICoreAPI api, BlockEntityChisel be, ArrayList found)
{
if (be == null || be.VoxelCuboids == null || be.BlockIds == null) return;
int matIdx = -1;
for (int i = 0; i < be.BlockIds.Length; i++)
{
int bid = be.BlockIds.GetValue(i) is int id ? id : 0;
if (ChiselRuneScanner.IsRuneGlass(api.World.GetBlock(bid)?.Code?.ToString())) { matIdx = i; break; }
}
if (matIdx < 0) return;
byte[] vox = new byte[4096];
foreach (uint c in be.VoxelCuboids)
{
if ((int)((c >> 24) & 0xFF) != matIdx) continue;
int x1 = (int)(c & 0xF), y1 = (int)((c >> 4) & 0xF), z1 = (int)((c >> 8) & 0xF);
int x2 = (int)((c >> 12) & 0xF) + 1, y2 = (int)((c >> 16) & 0xF) + 1, z2 = (int)((c >> 20) & 0xF) + 1;
for (int x = x1; x < x2; x++)
for (int y = y1; y < y2; y++)
for (int z = z1; z < z2; z++)
vox[(y * 16 + z) * 16 + x] = 1;
}
for (int axis = 0; axis < 3; axis++)
{
for (int slice = 0; slice < 16; slice++)
{
bool[] fg = new bool[256];
bool anyGlass = false;
for (int u = 0; u < 16; u++)
{
for (int v = 0; v < 16; v++)
{
int x = axis == 0 ? slice : u;
int y = axis == 2 ? slice : v;
int z = axis == 1 ? slice : (axis == 0 ? u : v);
bool isG = (vox[(y * 16 + z) * 16 + x] == 1);
fg[v * 16 + u] = isG;
if (isG) anyGlass = true;
}
}
if (!anyGlass) continue;
ArrayList sliceHits = new ArrayList();
ChiselRuneScanner.ScanAllDynamicRunesOnSlice(fg, sliceHits);
foreach (string name in sliceHits)
{
if (!found.Contains(name)) found.Add(name);
}
}
}
}
}
}
