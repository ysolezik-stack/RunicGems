using System;
using System.Collections;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
namespace RunicGems
{
public partial class BlockEntityChiselRuneBowl
{
private ArrayList lastLitPositions = new ArrayList();
public override void OnBlockRemoved()
{
if (Api?.Side == EnumAppSide.Server) SetPillarGlassGlow(false, new ArrayList());
base.OnBlockRemoved();
}
private void UpdateLiquidVoxels()
{
if (Api == null || VoxelCuboids == null || BlockIds == null) return;
Block oilBlock = Api.World.GetBlock(new AssetLocation("runicgems:runeglass-liquid"));
if (oilBlock == null) return;
int oilMatIdx = -1;
for (int i = 0; i < BlockIds.Length; i++)
if ((BlockIds.GetValue(i) is int id ? id : 0) == oilBlock.BlockId) { oilMatIdx = i; break; }
if (!ChiselBowlGeometry.ValidateBowlShape(this, Api.World, out int minY, out int maxRimY, out var cols, out int capItems))
{
if (oilAmount > 0) { oilAmount = 0; Api.World.PlaySoundAt(new AssetLocation("game:effect/water-pour"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5); }
return;
}
MaxOil = capItems;
if (oilAmount > MaxOil) oilAmount = MaxOil;
int maxDepth = Math.Max(1, maxRimY - minY);
int targetLayers = oilAmount <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling((oilAmount / (float)MaxOil) * maxDepth));
if (targetLayers == lastRenderedOilLayer) return;
lastRenderedOilLayer = targetLayers;
if (oilMatIdx >= 0) VoxelCuboids.RemoveAll(c => (int)((c >> 24) & 0xFF) == oilMatIdx);
if (targetLayers > 0)
{
if (oilMatIdx < 0)
{
oilMatIdx = BlockIds.Length;
int[] newIds = new int[BlockIds.Length + 1];
Array.Copy(BlockIds, newIds, BlockIds.Length);
newIds[oilMatIdx] = oilBlock.BlockId;
BlockIds = newIds;
}
int topOilY = Math.Min(maxRimY - 1, minY + targetLayers - 1);
foreach (int col in cols)
{
uint x = (uint)(col / 16), z = (uint)(col % 16), y1 = (uint)minY, y2 = (uint)topOilY, mat = (uint)oilMatIdx;
VoxelCuboids.Add(x | (y1 << 4) | (z << 8) | (x << 12) | (y2 << 16) | (z << 20) | (mat << 24));
}
}
MarkDirty(true);
Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
Api.World.BlockAccessor.MarkBlockDirty(Pos);
}
private void SetPillarGlassGlow(bool active, ArrayList currentTargets)
{
Block norm = Api.World.GetBlock(new AssetLocation("runicgems:inlaid-runiccrystal"));
Block act = Api.World.GetBlock(new AssetLocation("runicgems:inlaid-runiccrystal-active"));
if (norm == null || act == null) return;
ArrayList all = new ArrayList();
foreach (BlockPos p in lastLitPositions) if (!all.Contains(p)) all.Add(p);
foreach (BlockPos p in currentTargets) if (!all.Contains(p)) all.Add(p);
foreach (BlockPos bPos in all)
{
bool shouldGlow = active && currentTargets.Contains(bPos);
Block target = shouldGlow ? act : norm;
if (Api.World.BlockAccessor.GetBlockEntity(bPos) is not BlockEntityChisel ch || ch.BlockIds == null) continue;
bool changed = false;
for (int i = 0; i < ch.BlockIds.Length; i++)
{
int bid = ch.BlockIds.GetValue(i) is int id ? id : 0;
if (ChiselRuneScanner.IsRuneCrystalInlay(Api.World.GetBlock(bid)?.Code?.ToString()) && bid != target.BlockId)
{
ch.BlockIds.SetValue(target.BlockId, i);
changed = true;
}
}
if (changed)
{
ch.MarkDirty(true);
Api.World.BlockAccessor.MarkBlockEntityDirty(bPos);
Api.World.BlockAccessor.MarkBlockDirty(bPos);
}
}
lastLitPositions = new ArrayList();
foreach (BlockPos p in currentTargets) lastLitPositions.Add(p);
}
private void OnClientTick(float dt)
{
if (oilAmount <= 0) return;
SimpleParticleProperties mist = new SimpleParticleProperties(1, 2, ColorUtil.ToRgba(140, 34, 235, 200), new Vec3d(Pos.X + 0.3, Pos.Y + 0.5, Pos.Z + 0.3), new Vec3d(Pos.X + 0.7, Pos.Y + 0.6, Pos.Z + 0.7), new Vec3f(-0.03f, 0.08f, -0.03f), new Vec3f(0.03f, 0.18f, 0.03f), 1.6f, 0f, 0.35f, 0.7f, EnumParticleModel.Quad);
mist.SelfPropelled = true; mist.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -80f);
Api.World.SpawnParticles(mist);
}
public override void ToTreeAttributes(ITreeAttribute tree)
{
base.ToTreeAttributes(tree);
tree.SetInt("oil", oilAmount); tree.SetInt("maxoil", MaxOil);
tree.SetInt("sb", syncBlocks); tree.SetInt("sr", syncRunes);
tree.SetInt("sa", syncAnchor); tree.SetInt("sl", syncLight); tree.SetInt("ss", syncSwift); tree.SetInt("sbl", syncBolt); tree.SetInt("ssi", syncSiphon);
tree.SetInt("smist", syncMist); tree.SetInt("sseal", syncSeal); tree.SetInt("sheat", syncHeat); tree.SetInt("sfrost", syncFrost);
tree.SetFloat("srad", syncRadius); tree.SetBool("sd", syncDayOnly); tree.SetBool("sn", syncNightOnly);
tree.SetString("ssym", syncSymbols); tree.SetString("ssrc", syncSourceInfo);
}
public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor w)
{
base.FromTreeAttributes(tree, w);
oilAmount = tree.GetInt("oil"); MaxOil = tree.GetInt("maxoil", 100);
syncBlocks = tree.GetInt("sb"); syncRunes = tree.GetInt("sr");
syncAnchor = tree.GetInt("sa"); syncLight = tree.GetInt("sl"); syncSwift = tree.GetInt("ss"); syncBolt = tree.GetInt("sbl"); syncSiphon = tree.GetInt("ssi");
syncMist = tree.GetInt("smist"); syncSeal = tree.GetInt("sseal"); syncHeat = tree.GetInt("sheat"); syncFrost = tree.GetInt("sfrost");
syncRadius = tree.GetFloat("srad", 6f); syncDayOnly = tree.GetBool("sd"); syncNightOnly = tree.GetBool("sn");
syncSymbols = tree.GetString("ssym", ""); syncSourceInfo = tree.GetString("ssrc", "");
if (w?.Side == EnumAppSide.Client)
{
MarkDirty(true);
w.BlockAccessor.MarkBlockDirty(Pos);
}
}
public override void GetBlockInfo(IPlayer plr, StringBuilder dsc)
{
base.GetBlockInfo(plr, dsc);
dsc.AppendLine(string.Format("Темпоральное масло: {0:0.00} / {1:0.00} л", oilAmount / 100f, MaxOil / 100f));
if (!string.IsNullOrEmpty(syncSourceInfo)) dsc.AppendLine(syncSourceInfo);
dsc.AppendLine(string.Format("Рунная сеть: {0} блоков | Рун: {1}", syncBlocks, syncRunes));
if (syncRunes > 0)
{
dsc.AppendLine("Символы: " + syncSymbols);
if (oilAmount <= 0) { dsc.AppendLine("Статус: Нет темпорального масла"); return; }
if (!IsTimeConditionMet()) { dsc.AppendLine("Статус: Ожидает подходящего времени суток"); return; }
ArrayList effs = new ArrayList();
if (syncAnchor > 0) effs.Add("Якорь");
if (syncLight > 0) effs.Add("Свечение");
if (syncSwift > 0) effs.Add("Ускорение");
if (syncBolt > 0) effs.Add("Молния");
if (syncSiphon > 0) effs.Add("Подпитка");
if (syncMist > 0) effs.Add("Туман");
if (syncSeal > 0) effs.Add("Печать разломов");
if (syncHeat > 0) effs.Add("Нагрев");
if (syncFrost > 0) effs.Add("Охлаждение");
dsc.AppendLine(effs.Count > 0 ? string.Format("Активно (R={0:0.0} м): {1}", syncRadius, string.Join(", ", effs.ToArray())) : "Только модификаторы");
}
}
}
}

