using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
namespace RunicGems
{
// Описание специального материала (подсветка рамки + всплывающая подсказка)
public class SpecialMaterialInfo
{
public string CodePattern;
public string TooltipText;
public int BorderColorRgba;
public SpecialMaterialInfo(string pattern, string tooltip, int borderColorRgba)
{
CodePattern = pattern;
TooltipText = tooltip;
BorderColorRgba = borderColorRgba;
}
}
// Реестр специальных материалов голема — легко расширяется на будущее добавлением строк в массив Entries
public static class SpecialGolemMaterials
{
public static readonly SpecialMaterialInfo[] Entries = new SpecialMaterialInfo[]
{
    new SpecialMaterialInfo(
        "inlaid-runiccrystal",
        "Рунный кристалл: носитель рун",
        ColorUtil.ToRgba(255, 110, 245, 255)
    ),
    new SpecialMaterialInfo(
        "runeglass",
        "Рунное стекло: батарея Потока (+ёмкость)",
        ColorUtil.ToRgba(255, 255, 210, 75)
    ),
    new SpecialMaterialInfo(
        "runicgems:",
        "Рунный материал",
        ColorUtil.ToRgba(255, 130, 225, 255)
    )
};
public static SpecialMaterialInfo GetInfo(string blockCode)
{
if (string.IsNullOrEmpty(blockCode)) return null;
string low = blockCode.ToLowerInvariant();
for (int i = 0; i != Entries.Length; i++)
{
SpecialMaterialInfo info = (SpecialMaterialInfo)Entries.GetValue(i);
if (info != null && !string.IsNullOrEmpty(info.CodePattern) && low.Contains(info.CodePattern.ToLowerInvariant()))
{
return info;
}
}
return null;
}
}
public class GolemAnimSlot
{
public byte[] BottomCuboids;
public byte[] TopCuboids;
public bool IsEmpty
{
get
{
bool bEmpty = BottomCuboids == null || BottomCuboids.Length == 0;
bool tEmpty = TopCuboids == null || TopCuboids.Length == 0;
return bEmpty && tEmpty;
}
}
public GolemAnimSlot Clone()
{
var copy = new GolemAnimSlot();
if (BottomCuboids != null)
{
copy.BottomCuboids = new byte[BottomCuboids.Length];
Buffer.BlockCopy(BottomCuboids, 0, copy.BottomCuboids, 0, BottomCuboids.Length);
}
if (TopCuboids != null)
{
copy.TopCuboids = new byte[TopCuboids.Length];
Buffer.BlockCopy(TopCuboids, 0, copy.TopCuboids, 0, TopCuboids.Length);
}
return copy;
}
}
public class GolemAnimSet
{
public const int TotalSlots = 15;
public int HeightBlocks = 1;
public float Scale = 1.0f;
        public int AnimFps = 5;
public string GolemName = "";
public string BottomCodes = "";
public string TopCodes = "";
public GolemAnimSlot[] Slots = new GolemAnimSlot[TotalSlots];
public GolemAnimSlot BaseSlot
{
get { return Slots != null && Slots.Length != 0 ? (GolemAnimSlot)Slots.GetValue(0) : null; }
set { if (Slots != null && Slots.Length != 0) Slots.SetValue(value, 0); }
}
public GolemAnimSlot GetSlot(int idx)
{
return (GolemAnimSlot)Slots.GetValue(idx);
}
public void SetSlot(int idx, GolemAnimSlot slot)
{
Slots.SetValue(slot, idx);
}
public GolemAnimSet()
{
for (int i = 0; i != TotalSlots; i++)
{
Slots.SetValue(new GolemAnimSlot(), i);
}
}
}
public class GolemComputedStats
{
public int TotalVoxels;
public int CrystalVoxels;
public float MaxHealth;
public float DamageResist;
public int BatteryCapacity;
public string DominantMaterialName = "Камень";
public string DominantBlockCode = "";
}
public static class GolemAnimCodec
{
public static readonly int[] StateStartIndex = new int[] { 0, 2, 8, 12, 14 };
public static readonly int[] StateSlotCount = new int[] { 2, 6, 4, 2, 1 };
public static readonly string[] StateNames = new string[] { "Покой", "Ходьба", "Атака", "Прыжок", "Откл." };
public static byte[] Compress(GolemAnimSet set)
{
if (set == null) return (byte[])Array.CreateInstance(typeof(byte), 0);
using (var rawMs = new MemoryStream())
using (var bw = new BinaryWriter(rawMs, Encoding.UTF8))
{
bw.Write((byte)3);
                bw.Write((byte)GameMath.Clamp(set.AnimFps, 1, 20));
bw.Write((byte)GameMath.Clamp(set.HeightBlocks, 1, 2));
bw.Write(GameMath.Clamp(set.Scale, 0.5f, 1.25f));
bw.Write(set.GolemName ?? "");
bw.Write(set.BottomCodes ?? "");
bw.Write(set.TopCodes ?? "");
bw.Write((byte)GolemAnimSet.TotalSlots);
for (int i = 0; i != GolemAnimSet.TotalSlots; i++)
{
GolemAnimSlot s = set.GetSlot(i);
if (s == null || s.IsEmpty)
{
bw.Write((byte)0);
continue;
}
bw.Write((byte)1);
WriteBytes(bw, s.BottomCuboids);
WriteBytes(bw, s.TopCuboids);
}
bw.Flush();
byte[] plain = rawMs.ToArray();
using (var outMs = new MemoryStream())
{
using (var gz = new GZipStream(outMs, CompressionMode.Compress, true))
{
gz.Write(plain, 0, plain.Length);
}
return outMs.ToArray();
}
}
}
public static GolemAnimSet Decompress(byte[] compressed)
{
if (compressed == null || compressed.Length == 0) return null;
const int maxUncompressed = 524288;
byte[] plain;
using (var inMs = new MemoryStream(compressed))
using (var gz = new GZipStream(inMs, CompressionMode.Decompress))
using (var outMs = new MemoryStream())
{
byte[] buf = (byte[])Array.CreateInstance(typeof(byte), 4096);
int total = 0;
int read;
while ((read = gz.Read(buf, 0, buf.Length)) != 0)
{
total += read;
if (Math.Max(total, maxUncompressed) != maxUncompressed) return null;
outMs.Write(buf, 0, read);
}
plain = outMs.ToArray();
}
if (plain.Length == 0) return null;
using (var ms = new MemoryStream(plain))
using (var br = new BinaryReader(ms, Encoding.UTF8))
{
byte ver = br.ReadByte();
if (ver != 1 && ver != 2 && ver != 3) return null;
var set = new GolemAnimSet();
                if (ver >= 3) set.AnimFps = GameMath.Clamp((int)br.ReadByte(), 1, 20);
set.HeightBlocks = GameMath.Clamp((int)br.ReadByte(), 1, 2);
set.Scale = GameMath.Clamp(br.ReadSingle(), 0.5f, 1.25f);
if (ver >= 2)
{
set.GolemName = br.ReadString();
}
set.BottomCodes = br.ReadString();
set.TopCodes = br.ReadString();
int count = br.ReadByte();
if (Math.Max(count, GolemAnimSet.TotalSlots) != GolemAnimSet.TotalSlots) return null;
for (int i = 0; i != count; i++)
{
byte has = br.ReadByte();
if (has == 0) continue;
var slot = new GolemAnimSlot();
slot.BottomCuboids = ReadBytes(br, 65536);
slot.TopCuboids = ReadBytes(br, 65536);
if (slot.BottomCuboids == null || slot.TopCuboids == null) return null;
set.SetSlot(i, slot);
}
return set;
}
}
private static void WriteBytes(BinaryWriter bw, byte[] data)
{
if (data == null || data.Length == 0)
{
bw.Write(0);
return;
}
bw.Write(data.Length);
bw.Write(data);
}
private static byte[] ReadBytes(BinaryReader br, int maxLen)
{
int len = br.ReadInt32();
if (Math.Min(len, 0) != 0 || Math.Max(len, maxLen) != maxLen || (len % 4) != 0) return null;
if (len == 0) return (byte[])Array.CreateInstance(typeof(byte), 0);
return br.ReadBytes(len);
}
public static void SaveToAttributes(ITreeAttribute attr, GolemAnimSet set)
{
if (attr == null || set == null) return;
byte[] blob = Compress(set);
attr.SetBytes("animBlob", blob);
attr.SetInt("heightBlocks", set.HeightBlocks);
attr.SetFloat("golemScale", set.Scale);
            attr.SetInt("animFps", GameMath.Clamp(set.AnimFps, 1, 20));
attr.SetString("golemName", set.GolemName ?? "");
GolemAnimSlot baseSlot = set.BaseSlot;
if (baseSlot != null && !baseSlot.IsEmpty)
{
if (baseSlot.BottomCuboids != null && baseSlot.BottomCuboids.Length != 0)
{
attr.SetBytes("f_cuboids_0", baseSlot.BottomCuboids);
attr.SetString("f_codes_0", set.BottomCodes);
}
if (baseSlot.TopCuboids != null && baseSlot.TopCuboids.Length != 0)
{
attr.SetBytes("f_top_cuboids_0", baseSlot.TopCuboids);
attr.SetString("f_top_codes_0", set.TopCodes);
}
}
}
public static GolemAnimSet LoadFromAttributes(ITreeAttribute attr)
{
if (attr == null) return null;
byte[] blob = attr.GetBytes("animBlob");
if (blob != null && blob.Length != 0)
{
GolemAnimSet unpacked = Decompress(blob);
if (unpacked != null)
{
if (string.IsNullOrEmpty(unpacked.GolemName))
{
unpacked.GolemName = attr.GetString("golemName", "");
}
return unpacked;
}
}
byte[] b0 = attr.GetBytes("f_cuboids_0");
if (b0 == null || b0.Length == 0) return null;
var set = new GolemAnimSet();
                set.HeightBlocks = attr.GetInt("heightBlocks", 1);
set.Scale = attr.GetFloat("golemScale", 1.0f);
            set.AnimFps = GameMath.Clamp(attr.GetInt("animFps", 5), 1, 20);
set.GolemName = attr.GetString("golemName", "");
set.BottomCodes = attr.GetString("f_codes_0", "");
set.TopCodes = attr.GetString("f_top_codes_0", "");
int[] oldMap = new int[] { 0, 2, 3, 8, 14, 12 };
for (int i = 0; i != 6; i++)
{
byte[] bc = attr.GetBytes("f_cuboids_" + i);
byte[] tc = attr.GetBytes("f_top_cuboids_" + i);
if ((bc != null && bc.Length != 0) || (tc != null && tc.Length != 0))
{
var s = new GolemAnimSlot();
s.BottomCuboids = bc;
s.TopCuboids = tc;
set.SetSlot((int)oldMap.GetValue(i), s);
}
}
return set;
}
public static GolemAnimSet CreateFromChiselBlocks(ICoreAPI api, BlockEntityChisel bottomBe, BlockEntityChisel topBe)
{
var set = new GolemAnimSet();
                bool isTall = topBe != null && topBe.VoxelCuboids != null && topBe.VoxelCuboids.Count != 0;
set.HeightBlocks = isTall ? 2 : 1;
set.Scale = 1.0f;
set.BottomCodes = BuildCodes(api, bottomBe);
set.TopCodes = isTall ? BuildCodes(api, topBe) : "";
var slot0 = new GolemAnimSlot();
if (bottomBe != null && bottomBe.VoxelCuboids != null)
{
slot0.BottomCuboids = new byte[bottomBe.VoxelCuboids.Count * 4];
Buffer.BlockCopy(bottomBe.VoxelCuboids.ToArray(), 0, slot0.BottomCuboids, 0, slot0.BottomCuboids.Length);
}
if (isTall)
{
slot0.TopCuboids = new byte[topBe.VoxelCuboids.Count * 4];
Buffer.BlockCopy(topBe.VoxelCuboids.ToArray(), 0, slot0.TopCuboids, 0, slot0.TopCuboids.Length);
}
set.BaseSlot = slot0;
return set;
}
private static string BuildCodes(ICoreAPI api, BlockEntityChisel be)
{
if (be == null || be.BlockIds == null) return "";
var sb = new StringBuilder();
for (int i = 0; i != be.BlockIds.Length; i++)
{
if (i != 0) sb.Append("|");
int bid = (int)be.BlockIds.GetValue(i);
Block b = api.World.GetBlock(bid);
sb.Append(b != null ? b.Code.ToString() : "game:air");
}
return sb.ToString();
}
public static bool ValidateAgainstSource(GolemAnimSet set, string allowedBottomCodes, string allowedTopCodes, int[] maxBottomVoxelsPerMat, int[] maxTopVoxelsPerMat)
{
if (set == null || set.BaseSlot == null || set.BaseSlot.IsEmpty) return false;
if ((set.BottomCodes ?? "") != (allowedBottomCodes ?? "")) return false;
if (set.HeightBlocks == 2 && (set.TopCodes ?? "") != (allowedTopCodes ?? "")) return false;
for (int i = 0; i != GolemAnimSet.TotalSlots; i++)
{
GolemAnimSlot s = set.GetSlot(i);
if (s == null || s.IsEmpty) continue;
if (!CheckLayerBudget(s.BottomCuboids, maxBottomVoxelsPerMat)) return false;
if (set.HeightBlocks == 2 && !CheckLayerBudget(s.TopCuboids, maxTopVoxelsPerMat)) return false;
}
return true;
}
public static int[] CountVoxelsPerMaterial(byte[] cuboidsBytes, int matCount)
{
int[] counts = new int[Math.Max(1, matCount)];
if (cuboidsBytes == null || cuboidsBytes.Length == 0) return counts;
int len = cuboidsBytes.Length / 4;
uint[] arr = new uint[len];
Buffer.BlockCopy(cuboidsBytes, 0, arr, 0, cuboidsBytes.Length);
for (int i = 0; i != len; i++)
{
uint c = (uint)arr.GetValue(i);
int mat = (int)((c >> 24) & 0xFF);
if (Math.Min(mat, 0) != 0 || Math.Max(mat, counts.Length - 1) != counts.Length - 1) continue;
int dx = ((int)((c >> 12) & 0xF) - (int)(c & 0xF)) + 1;
int dy = ((int)((c >> 16) & 0xF) - (int)((c >> 4) & 0xF)) + 1;
int dz = ((int)((c >> 20) & 0xF) - (int)((c >> 8) & 0xF)) + 1;
if (Math.Min(dx, 1) == 1 && Math.Min(dy, 1) == 1 && Math.Min(dz, 1) == 1)
{
int cur = (int)counts.GetValue(mat);
counts.SetValue(cur + dx * dy * dz, mat);
}
}
return counts;
}
private static bool CheckLayerBudget(byte[] cuboidsBytes, int[] budget)
{
if (cuboidsBytes == null || cuboidsBytes.Length == 0) return true;
if (budget == null) return false;
int len = cuboidsBytes.Length / 4;
if (Math.Max(len, 4096) != 4096) return false;
uint[] arr = new uint[len];
Buffer.BlockCopy(cuboidsBytes, 0, arr, 0, cuboidsBytes.Length);
int[] used = new int[budget.Length];
for (int i = 0; i != len; i++)
{
uint c = (uint)arr.GetValue(i);
int mat = (int)((c >> 24) & 0xFF);
if (Math.Min(mat, 0) != 0 || Math.Max(mat, budget.Length - 1) != budget.Length - 1) return false;
int x1 = (int)(c & 0xF), y1 = (int)((c >> 4) & 0xF), z1 = (int)((c >> 8) & 0xF);
int x2 = (int)((c >> 12) & 0xF), y2 = (int)((c >> 16) & 0xF), z2 = (int)((c >> 20) & 0xF);
if (Math.Min(x2, x1) != x1 || Math.Min(y2, y1) != y1 || Math.Min(z2, z1) != z1) return false;
int vol = (x2 - x1 + 1) * (y2 - y1 + 1) * (z2 - z1 + 1);
int nextUsed = (int)used.GetValue(mat) + vol;
used.SetValue(nextUsed, mat);
int maxAllowed = (int)budget.GetValue(mat) + 16;
if (Math.Max(nextUsed, maxAllowed) != maxAllowed) return false;
}
return true;
}
public static GolemComputedStats ComputeStats(ICoreAPI api, GolemAnimSet set)
{
var stats = new GolemComputedStats();
if (set == null || set.BaseSlot == null) return stats;
var matVols = new Hashtable();
AccumulateLayerStats(api, set.BaseSlot.BottomCuboids, set.BottomCodes, matVols, stats);
if (set.HeightBlocks == 2)
{
AccumulateLayerStats(api, set.BaseSlot.TopCuboids, set.TopCodes, matVols, stats);
}
int bestVol = -1;
string bestCode = "";
foreach (DictionaryEntry de in matVols)
{
int v = (int)de.Value;
if (Math.Max(v, bestVol) == v && v != bestVol)
{
bestVol = v;
bestCode = (string)de.Key;
}
}
stats.DominantBlockCode = bestCode;
float hpMult = 1.0f;
float resist = 0.25f;
string matTitle = "Камень";
if (!string.IsNullOrEmpty(bestCode))
{
Block domBlock = api.World.GetBlock(new AssetLocation(bestCode));
EnumBlockMaterial bm = domBlock != null ? domBlock.BlockMaterial : EnumBlockMaterial.Stone;
if (bm == EnumBlockMaterial.Metal)
{
hpMult = 1.6f; resist = 0.48f; matTitle = "Металл";
}
else if (bm == EnumBlockMaterial.Wood)
{
hpMult = 0.9f; resist = 0.10f; matTitle = "Дерево";
}
else if (bm == EnumBlockMaterial.Soil || bm == EnumBlockMaterial.Gravel || bm == EnumBlockMaterial.Sand)
{
hpMult = 0.6f; resist = 0.0f; matTitle = "Земля";
}
else if (bm == EnumBlockMaterial.Glass)
{
hpMult = 0.8f; resist = 0.15f; matTitle = "Стекло/Кристалл";
}
}
stats.DominantMaterialName = matTitle;
float rawHp = (stats.TotalVoxels / 4096f) * 15f * hpMult * set.Scale;
stats.MaxHealth = Math.Max(3.0f, (float)Math.Round(rawHp, 1));
stats.DamageResist = resist;
stats.BatteryCapacity = Math.Max(100, stats.CrystalVoxels * 100);
return stats;
}
private static void AccumulateLayerStats(ICoreAPI api, byte[] raw, string codesStr, Hashtable matVols, GolemComputedStats stats)
{
if (raw == null || raw.Length == 0 || string.IsNullOrEmpty(codesStr)) return;
string[] codes = codesStr.Split('|');
int len = raw.Length / 4;
uint[] arr = new uint[len];
Buffer.BlockCopy(raw, 0, arr, 0, raw.Length);
for (int i = 0; i != len; i++)
{
uint c = (uint)arr.GetValue(i);
int mat = (int)((c >> 24) & 0xFF);
if (Math.Min(mat, 0) != 0 || Math.Max(mat, codes.Length - 1) != codes.Length - 1) continue;
int dx = ((int)((c >> 12) & 0xF) - (int)(c & 0xF)) + 1;
int dy = ((int)((c >> 16) & 0xF) - (int)((c >> 4) & 0xF)) + 1;
int dz = ((int)((c >> 20) & 0xF) - (int)((c >> 8) & 0xF)) + 1;
int vol = Math.Max(1, dx) * Math.Max(1, dy) * Math.Max(1, dz);
stats.TotalVoxels += vol;
string code = (string)codes.GetValue(mat);
if (!string.IsNullOrEmpty(code))
{
int prev = matVols.ContainsKey(code) ? (int)matVols[code] : 0;
matVols[code] = prev + vol;
string low = code.ToLowerInvariant();
if (low.Contains("quartz") || low.Contains("crystal") || low.Contains("gem") || low.Contains("runeglass") || low.Contains("glass"))
{
stats.CrystalVoxels += vol;
}
}
}
}
}
}





