using System;
using System.Collections;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
namespace RunicGems
{
public enum RuneSymbol { None, Start, End, Day, Night, Anchor, Light, Swift, Bolt, Siphon, Relay, Room, Beam, Mist, Seal, Heat, Frost, Follow, Patrol, Guard, Carry, Roam, Storage, Target }
public class PillarScanResult
{
public int ActiveBlocksCount, RuneGlassBlocksCount, TotalRunesCount;
public int AnchorCount, LightCount, SwiftCount, BoltCount, SiphonCount, RelayCount;
public int MistCount, SealCount, HeatCount, FrostCount, StorageCount;
public float BonusRadius = 0f;
public ArrayList RecognizedRunes = new ArrayList();
public ArrayList ActiveBlockPositions = new ArrayList();
public ArrayList BeamDirections = new ArrayList();
public ArrayList TargetPositions = new ArrayList();
public bool DayOnly, NightOnly, RoomMode;
}
public static class ChiselRuneScanner
{
public static readonly BlockFacing[] HorFaces = new BlockFacing[] { BlockFacing.NORTH, BlockFacing.EAST, BlockFacing.SOUTH, BlockFacing.WEST };
public static bool IsRuneGlass(string code) => code == "runicgems:runeglass-normal" || code == "runicgems:runeglass-active";
public static string MatchDynamicRuneSlice(bool[] fg, int axis = -1)
{
ArrayList entries = CustomRuneRegistry.GetEntries();
foreach (CustomRuneEntry ent in entries)
{
if (ent == null || string.IsNullOrEmpty(ent.Bits)) continue;
if (axis >= 0 && !IsPlaneAllowed(ent.Plane, axis)) continue;
int sz = ent.Size;
if (sz != 4 && sz != 8 && sz != 16) continue;
if (ent.Bits.Length < sz * sz) continue;
int maxOff = 16 - sz;
for (int su = 0; su <= maxOff; su++)
{
for (int sv = 0; sv <= maxOff; sv++)
{
if (sz < 16 && !MarginOkSize(fg, su, sv, sz)) continue;
if (MatchPatchAnyOrientation(fg, su, sv, sz, ent.Bits))
return ent.Name;
}
}
}
return null;
}
public static void ScanAllDynamicRunesOnSlice(bool[] fg, ArrayList outNames, int axis = -1)
{
ArrayList entries = CustomRuneRegistry.GetEntries();
foreach (CustomRuneEntry ent in entries)
{
if (ent == null || string.IsNullOrEmpty(ent.Bits)) continue;
if (axis >= 0 && !IsPlaneAllowed(ent.Plane, axis)) continue;
int sz = ent.Size;
if (sz != 4 && sz != 8 && sz != 16) continue;
if (ent.Bits.Length < sz * sz) continue;
int maxOff = 16 - sz;
for (int su = 0; su <= maxOff; su++)
{
for (int sv = 0; sv <= maxOff; sv++)
{
if (sz < 16 && !MarginOkSize(fg, su, sv, sz)) continue;
if (MatchPatchAnyOrientation(fg, su, sv, sz, ent.Bits))
{
outNames.Add(ent.Name);
}
}
}
}
}
private static bool IsPlaneAllowed(string plane, int axis)
{
if (string.IsNullOrEmpty(plane) || plane == "Any") return true;
if (plane == "Hor") return axis == 2;
if (plane == "Ver") return axis == 0 || axis == 1;
return true;
}
private static bool MarginOkSize(bool[] g, int u0, int v0, int sz)
{
for (int du = -1; du <= sz; du++)
for (int dv = -1; dv <= sz; dv++)
{
if (du >= 0 && du < sz && dv >= 0 && dv < sz) continue;
int u = u0 + du, v = v0 + dv;
if (u >= 0 && u < 16 && v >= 0 && v < 16 && g[v * 16 + u]) return false;
}
return true;
}
private static bool MatchPatchAnyOrientation(bool[] fg, int u0, int v0, int sz, string targetBits)
{
bool[] cur = new bool[sz * sz];
for (int v = 0; v < sz; v++)
for (int u = 0; u < sz; u++)
cur[v * sz + u] = fg[(v0 + v) * 16 + (u0 + u)];
for (int rot = 0; rot < 4; rot++)
{
if (EqualsBits(cur, sz, targetBits)) return true;
if (EqualsBits(MirrorGrid(cur, sz), sz, targetBits)) return true;
cur = RotateGrid90(cur, sz);
}
return false;
}
private static bool EqualsBits(bool[] g, int sz, string bits)
{
int len = sz * sz;
for (int i = 0; i < len; i++)
if (g[i] != (bits[i] == '1')) return false;
return true;
}
private static bool[] RotateGrid90(bool[] g, int sz)
{
bool[] r = new bool[sz * sz];
for (int v = 0; v < sz; v++)
for (int u = 0; u < sz; u++)
r[u * sz + (sz - 1 - v)] = g[v * sz + u];
return r;
}
private static bool[] MirrorGrid(bool[] g, int sz)
{
bool[] r = new bool[sz * sz];
for (int v = 0; v < sz; v++)
for (int u = 0; u < sz; u++)
r[v * sz + (sz - 1 - u)] = g[v * sz + u];
return r;
}
public static PillarScanResult ScanPillarBelow(IWorldAccessor world, BlockPos pos)
{
PillarScanResult res = new PillarScanResult();
var queue = new Queue();
var visited = new ArrayList();
for (int dy = 0; dy <= 3; dy++)
{
BlockPos p = pos.DownCopy(dy);
if (world.BlockAccessor.GetBlockEntity(p) is BlockEntityChisel) { visited.Add(p); queue.Enqueue(p); }
else if (dy > 0) break;
}
while (queue.Count > 0 && visited.Count <= 12)
{
BlockPos cur = queue.Dequeue() as BlockPos;
if (cur == null) continue;
if (world.BlockAccessor.GetBlockEntity(cur) is not BlockEntityChisel ch) continue;
int before = res.RecognizedRunes.Count;
ArrayList relayFaces = ScanBlock(world, ch, res);
if (cur != pos || res.RecognizedRunes.Count > before) { res.ActiveBlocksCount++; res.ActiveBlockPositions.Add(cur); }
for (int rf = 0; rf < relayFaces.Count; rf++)
{
int fIdx = (int)relayFaces[rf];
int[] sideFaces = new int[] { (fIdx + 3) % 4, (fIdx + 1) % 4 };
foreach (int sf in sideFaces)
{
BlockPos next = cur.AddCopy(HorFaces[sf]);
if (!visited.Contains(next) && world.BlockAccessor.GetBlockEntity(next) is BlockEntityChisel) { visited.Add(next); queue.Enqueue(next); }
}
}
}
if (res.RecognizedRunes.Count > 64) res.RecognizedRunes.RemoveRange(64, res.RecognizedRunes.Count - 64);
res.TotalRunesCount = res.RecognizedRunes.Count;
res.BonusRadius = res.RuneGlassBlocksCount * 2.0f;
return res;
}
private static ArrayList ScanBlock(IWorldAccessor world, BlockEntityChisel ch, PillarScanResult res)
{
var relayFaces = new ArrayList();
if (ch.VoxelCuboids == null || ch.BlockIds == null) return relayFaces;
int matIdx = -1;
for (int i = 0; i < ch.BlockIds.Length; i++)
{
int bid = ch.BlockIds.GetValue(i) is int id ? id : 0;
if (IsRuneGlass(world.GetBlock(bid)?.Code?.ToString())) { matIdx = i; res.RuneGlassBlocksCount++; break; }
}
if (matIdx < 0) return relayFaces;
byte[] vox = new byte[4096];
foreach (uint c in ch.VoxelCuboids)
{
int x1 = (int)(c & 0xF), y1 = (int)((c >> 4) & 0xF), z1 = (int)((c >> 8) & 0xF);
int x2 = (int)((c >> 12) & 0xF) + 1, y2 = (int)((c >> 16) & 0xF) + 1, z2 = (int)((c >> 20) & 0xF) + 1;
byte val = ((int)((c >> 24) & 0xFF) == matIdx) ? (byte)2 : (byte)1;
for (int x = x1; x < x2; x++) for (int y = y1; y < y2; y++) for (int z = z1; z < z2; z++) vox[(y * 16 + z) * 16 + x] = val;
}
for (int f = 0; f < 4; f++)
{
bool[] fg = new bool[256];
for (int u = 0; u < 16; u++) for (int v = 0; v < 16; v++)
{
int x = f == 0 ? u : (f == 1 ? 15 : (f == 2 ? 15 - u : 0));
int z = f == 0 ? 0 : (f == 1 ? u : (f == 2 ? 15 : 15 - u));
fg[v * 16 + u] = (vox[(v * 16 + z) * 16 + x] == 2);
}
if (MatchLargeRelay14x14(fg)) { relayFaces.Add(f); res.RelayCount++; res.RecognizedRunes.Add(RuneSymbol.Relay); }
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
bool isG = (vox[(y * 16 + z) * 16 + x] == 2);
fg[v * 16 + u] = isG;
if (isG) anyGlass = true;
}
}
if (!anyGlass) continue;
ArrayList foundNames = new ArrayList();
ScanAllDynamicRunesOnSlice(fg, foundNames, axis);
foreach (string name in foundNames)
{
if (!Enum.TryParse(name, true, out RuneSymbol sym) || sym == RuneSymbol.None) continue;
res.RecognizedRunes.Add(sym);
if (sym == RuneSymbol.Anchor) res.AnchorCount++;
if (sym == RuneSymbol.Light) res.LightCount++;
if (sym == RuneSymbol.Swift) res.SwiftCount++;
if (sym == RuneSymbol.Bolt) res.BoltCount++;
if (sym == RuneSymbol.Siphon) res.SiphonCount++;
if (sym == RuneSymbol.Mist) res.MistCount++;
if (sym == RuneSymbol.Seal) res.SealCount++;
if (sym == RuneSymbol.Heat) res.HeatCount++;
if (sym == RuneSymbol.Frost) res.FrostCount++;
if (sym == RuneSymbol.Storage) res.StorageCount++;
if (sym == RuneSymbol.Target && !res.TargetPositions.Contains(ch.Pos)) res.TargetPositions.Add(ch.Pos.Copy());
if (sym == RuneSymbol.Day) res.DayOnly = true;
if (sym == RuneSymbol.Night) res.NightOnly = true;
if (sym == RuneSymbol.Room) res.RoomMode = true;
if (sym == RuneSymbol.Beam && axis < 2)
{
BlockFacing beamFace = axis == 0
? (slice < 8 ? BlockFacing.WEST : BlockFacing.EAST)
: (slice < 8 ? BlockFacing.NORTH : BlockFacing.SOUTH);
if (!res.BeamDirections.Contains(beamFace)) res.BeamDirections.Add(beamFace);
}
}
}
}
return relayFaces;
}
private static bool MatchLargeRelay14x14(bool[] g)
{
for (int i = 0; i < 16; i++) if (g[i] || g[15 * 16 + i] || g[i * 16] || g[i * 16 + 15]) return false;
for (int u = 1; u <= 14; u++) for (int v = 1; v <= 14; v++)
{
bool isBorder = (u == 1 || u == 14 || v == 1 || v == 14);
bool isCross = (u == 7 || u == 8 || v == 7 || v == 8);
if (g[v * 16 + u] != (isBorder || isCross)) return false;
}
return true;
}
}
}
