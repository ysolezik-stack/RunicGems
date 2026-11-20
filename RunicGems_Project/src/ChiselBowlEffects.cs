using System;
using System.Collections;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
namespace RunicGems
{
public partial class BlockEntityChiselRuneBowl
{
private int bowlHumCounter;
private void RebuildRoomCache()
{
cachedRoomBlocks.Clear();
if (!syncRoomMode) return;
var q = new Queue();
BlockPos start = Pos.UpCopy();
q.Enqueue(start);
cachedRoomBlocks.Add(start);
cachedRoomBlocks.Add(Pos);
while (q.Count > 0 && cachedRoomBlocks.Count < 1400)
{
BlockPos cur = q.Dequeue() as BlockPos;
if (cur == null) continue;
foreach (BlockFacing f in BlockFacing.ALLFACES)
{
BlockPos n = cur.AddCopy(f);
if (cachedRoomBlocks.Contains(n)) continue;
if (Math.Abs(n.X - Pos.X) > 10 || Math.Abs(n.Z - Pos.Z) > 10 || n.Y < Pos.Y - 3 || n.Y > Pos.Y + 7) continue;
Block b = Api.World.BlockAccessor.GetBlock(n);
if (b == null || !b.SideSolid[f.Opposite.Index])
{
cachedRoomBlocks.Add(n);
q.Enqueue(n);
}
}
}
}
private bool IsTargetInZone(Vec3d targetPos)
{
if (activeBeams.Count > 0)
{
double maxLen = syncRadius * 2.2;
foreach (BlockFacing dir in activeBeams)
{
double dx = targetPos.X - (Pos.X + 0.5);
double dz = targetPos.Z - (Pos.Z + 0.5);
double along = dx * dir.Normali.X + dz * dir.Normali.Z;
double perp = Math.Abs(dx * dir.Normali.Z - dz * dir.Normali.X);
if (along >= 0.3 && along <= maxLen && perp <= 1.75 && Math.Abs(targetPos.Y - (Pos.Y + 0.5)) <= 2.5) return true;
}
return false;
}
if (syncRoomMode)
{
BlockPos bp = new BlockPos((int)Math.Floor(targetPos.X), (int)Math.Floor(targetPos.Y), (int)Math.Floor(targetPos.Z), 0);
return cachedRoomBlocks.Contains(bp) || cachedRoomBlocks.Contains(bp.UpCopy());
}
return targetPos.SquareDistanceTo(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5) <= syncRadius * syncRadius;
}
private void TrySiphon()
{
if (syncSiphon <= 0 || oilAmount >= MaxOil - 10) { syncSourceInfo = ""; return; }
int r = 5 + syncBlocks * 2;
BlockPos bestPos = null; BlockLiquidContainerBase bestCont = null; double bestDist = double.MaxValue;
for (int dx = -r; dx <= r; dx++) for (int dy = -3; dy <= 3; dy++) for (int dz = -r; dz <= r; dz++)
{
if (dx == 0 && dy == 0 && dz == 0) continue;
BlockPos p = Pos.AddCopy(dx, dy, dz);
if (Api.World.BlockAccessor.GetBlock(p) is not BlockLiquidContainerBase cont) continue;
ItemStack cnt = cont.GetContent(p);
if (cnt != null && cnt.StackSize > 0 && cnt.Collectible?.Code?.ToString() == "runicgems:temporaloil")
{ double d = dx * dx + dy * dy + dz * dz; if (d < bestDist) { bestDist = d; bestPos = p; bestCont = cont; } }
}
if (bestPos != null && bestCont != null)
{
int move = Math.Min(25, Math.Min(MaxOil - oilAmount, bestCont.GetContent(bestPos)?.StackSize ?? 0));
ItemStack taken = move > 0 ? bestCont.TryTakeContent(bestPos, move) : null;
if (taken != null && taken.StackSize > 0)
{
oilAmount += taken.StackSize;
syncSourceInfo = string.Format("Подпитка (Siphon): {0:0.0} м", Math.Sqrt(bestDist));
for (int i = 0; i <= 10; i++) { double t = i / 10.0; Vec3d pt = new Vec3d(bestPos.X + 0.5 + (Pos.X - bestPos.X) * t, bestPos.Y + 0.6 + (Pos.Y - bestPos.Y) * t + Math.Sin(t * Math.PI) * 0.35, bestPos.Z + 0.5 + (Pos.Z - bestPos.Z) * t); Api.World.SpawnParticles(1, ColorUtil.ToRgba(230, 45, 240, 210), pt, pt, new Vec3f(-0.02f, 0.02f, -0.02f), new Vec3f(0.02f, 0.05f, 0.02f), 0.45f, 0f, 0.2f, EnumParticleModel.Quad); }
}
}
else syncSourceInfo = "";
}
private bool HasWaterSourceNearPillar(ArrayList pillarBlocks)
{
ArrayList anchors = new ArrayList();
anchors.Add(Pos);
foreach (BlockPos bp in pillarBlocks) if (!anchors.Contains(bp)) anchors.Add(bp);
foreach (BlockPos basePos in anchors)
{
for (int dx = -3; dx <= 3; dx++)
for (int dy = -2; dy <= 2; dy++)
for (int dz = -3; dz <= 3; dz++)
{
BlockPos p = basePos.AddCopy(dx, dy, dz);
Block b = Api.World.BlockAccessor.GetBlock(p);
if (b != null && b.LiquidCode == "water") return true;
if (b is BlockLiquidContainerBase cont)
{
ItemStack cnt = cont.GetContent(p);
if (cnt != null && cnt.StackSize > 0 && (cnt.Collectible?.Code?.Path?.Contains("water") ?? false))
return true;
}
}
}
return false;
}
private void ApplyMistEffect(ArrayList pillarBlocks)
{
if (syncMist <= 0 || oilAmount < 1) return;
if (!HasWaterSourceNearPillar(pillarBlocks)) return;
int r = (int)Math.Ceiling(syncRoomMode ? 10 : syncRadius);
int watered = 0;
for (int dx = -r; dx <= r; dx++)
for (int dy = -3; dy <= 3; dy++)
for (int dz = -r; dz <= r; dz++)
{
BlockPos p = Pos.AddCopy(dx, dy, dz);
Vec3d center = new Vec3d(p.X + 0.5, p.Y + 0.85, p.Z + 0.5);
if (!IsTargetInZone(center)) continue;
if (Api.World.BlockAccessor.GetBlockEntity(p) is BlockEntityFarmland farm)
{
if (farm.MoistureLevel < 0.9f)
{
farm.WaterFarmland(0.25f, false);
watered++;
}
if (Api.World.Rand.NextDouble() < 0.18)
{
Api.World.SpawnParticles(1, ColorUtil.ToRgba(110, 195, 245, 255),
new Vec3d(p.X + 0.2, p.Y + 1.02, p.Z + 0.2),
new Vec3d(p.X + 0.8, p.Y + 1.15, p.Z + 0.8),
new Vec3f(-0.02f, 0.01f, -0.02f),
new Vec3f(0.02f, 0.03f, 0.02f),
1.4f, 0f, 0.45f, EnumParticleModel.Quad);
}
}
}
if (watered > 0 && Api.World.Rand.NextDouble() < 0.35)
{
oilAmount = Math.Max(0, oilAmount - 1);
}
}
private void ApplySealEffect()
{
if (syncSeal <= 0 || oilAmount < 3) return;
var riftSys = Api.ModLoader.GetModSystem("Vintagestory.GameContent.ModSystemRifts") as ModSystemRifts;
if (riftSys?.riftsById == null) return;
Vec3d bowlCenter = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
foreach (Rift rift in riftSys.riftsById.Values)
{
if (rift == null || rift.Position == null) continue;
bool inZone = IsTargetInZone(rift.Position) || rift.Position.DistanceTo(bowlCenter) <= (syncRadius + 3.0);
if (!inZone) continue;
if (rift.Size > 0.05f)
{
rift.Size = Math.Max(0f, rift.Size - 0.18f * syncSeal);
rift.DieAtTotalHours = Math.Min(rift.DieAtTotalHours, Api.World.Calendar.TotalHours + 0.02);
oilAmount = Math.Max(0, oilAmount - 3);
SpawnBoltBeam(bowlCenter, rift.Position.AddCopy(0, 0.8, 0));
Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-break"), rift.Position.X, rift.Position.Y, rift.Position.Z, null, 0.7f, 18f, 0.45f);
}
if (oilAmount < 3) break;
}
}
private void PreserveContainerAt(BlockPos bp)
{
if (Api.World.BlockAccessor.GetBlockEntity(bp) is BlockEntityContainer cont && cont.Inventory != null)
{
foreach (ItemSlot slot in cont.Inventory)
{
if (!slot.Empty && slot.Itemstack.Attributes != null && slot.Itemstack.Attributes.HasAttribute("transitionstate"))
{
slot.Itemstack.Attributes.RemoveAttribute("transitionstate");
slot.MarkDirty();
}
}
}
}
private void ApplyStorageEffect()
{
if (syncStorage <= 0 || oilAmount < 1) return;
int r = (int)Math.Ceiling(syncRoomMode ? 10 : syncRadius);
for (int dx = -r; dx <= r; dx++)
for (int dy = -3; dy <= 3; dy++)
for (int dz = -r; dz <= r; dz++)
{
BlockPos bp = Pos.AddCopy(dx, dy, dz);
if (IsTargetInZone(new Vec3d(bp.X + 0.5, bp.Y + 0.5, bp.Z + 0.5)))
{
PreserveContainerAt(bp);
}
}
if (Api.World.Rand.NextDouble() < 0.08)
{
oilAmount = Math.Max(0, oilAmount - 1);
}
}
private void ApplyClimateEffects()
{
if (syncHeat <= 0 && syncFrost <= 0) return;
foreach (IPlayer plr in Api.World.AllOnlinePlayers)
{
if (plr?.Entity == null || !IsTargetInZone(plr.Entity.Pos.XYZ)) continue;
var tempBh = plr.Entity.GetBehavior("bodytemperature") as EntityBehaviorBodyTemperature;
if (tempBh != null)
{
if (syncHeat > 0 && tempBh.CurBodyTemperature < 37.0f)
tempBh.CurBodyTemperature = Math.Min(37.0f, tempBh.CurBodyTemperature + 0.25f * syncHeat);
if (syncFrost > 0 && tempBh.CurBodyTemperature > 36.8f)
tempBh.CurBodyTemperature = Math.Max(36.8f, tempBh.CurBodyTemperature - 0.25f * syncFrost);
}
}
if (syncHeat > 0 && Api.World.Rand.NextDouble() < 0.35)
{
int r = (int)Math.Min(8, syncRadius);
BlockPos rp = Pos.AddCopy(Api.World.Rand.Next(-r, r + 1), Api.World.Rand.Next(-1, 3), Api.World.Rand.Next(-r, r + 1));
if (IsTargetInZone(new Vec3d(rp.X + 0.5, rp.Y + 0.5, rp.Z + 0.5)))
{
Block b = Api.World.BlockAccessor.GetBlock(rp);
string code = b?.Code?.Path ?? "";
if (code.StartsWith("snowlayer"))
{
Api.World.BlockAccessor.SetBlock(0, rp);
}
else if (code.Contains("lakeice"))
{
Block water = Api.World.GetBlock(new AssetLocation("game:water-still-7"));
if (water != null) Api.World.BlockAccessor.SetBlock(water.BlockId, rp);
}
}
}
if (syncFrost > 0 && syncRoomMode)
{
foreach (BlockPos bp in cachedRoomBlocks)
{
PreserveContainerAt(bp);
}
}
}
private void SpawnBoltBeam(Vec3d start, Vec3d hit)
{
Vec3d d = hit.SubCopy(start);
Vec3d m1 = start.AddCopy(d.X * 0.33 + (Api.World.Rand.NextDouble() - 0.5) * 0.5, d.Y * 0.33 + 0.25, d.Z * 0.33 + (Api.World.Rand.NextDouble() - 0.5) * 0.5);
Vec3d m2 = start.AddCopy(d.X * 0.66 + (Api.World.Rand.NextDouble() - 0.5) * 0.5, d.Y * 0.66 - 0.15, d.Z * 0.66 + (Api.World.Rand.NextDouble() - 0.5) * 0.5);
DrawBoltSegment(start, m1, 14);
DrawBoltSegment(m1, m2, 14);
DrawBoltSegment(m2, hit, 14);
var shards = new SimpleParticleProperties(
10, 14,
ColorUtil.ToRgba(255, 190, 255, 255),
hit, hit,
new Vec3f(-1.8f, 0.4f, -1.8f),
new Vec3f(1.8f, 2.4f, 1.8f),
0.45f, 0.55f, 0.12f, 0.24f,
EnumParticleModel.Cube
);
shards.WithTerrainCollision = true;
shards.VertexFlags = 255;
shards.Bounciness = 0.4f;
shards.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -220f);
Api.World.SpawnParticles(shards);
}
private void DrawBoltSegment(Vec3d a, Vec3d b, int count)
{
Vec3d seg = b.SubCopy(a);
for (int i = 0; i <= count; i++)
{
double t = i / (double)count;
Vec3d pt = new Vec3d(a.X + seg.X * t, a.Y + seg.Y * t, a.Z + seg.Z * t);
var outer = new SimpleParticleProperties(
1, 1,
ColorUtil.ToRgba(210, 45, 225, 255),
pt, pt,
new Vec3f(0f, 0f, 0f),
new Vec3f(0f, 0f, 0f),
0.32f, 0f, 0.26f, 0.32f,
EnumParticleModel.Quad
);
outer.VertexFlags = 255;
outer.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -210f);
outer.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -0.25f);
Api.World.SpawnParticles(outer);
var core = new SimpleParticleProperties(
1, 1,
ColorUtil.ToRgba(255, 235, 255, 255),
pt, pt,
new Vec3f(0f, 0f, 0f),
new Vec3f(0f, 0f, 0f),
0.26f, 0f, 0.13f, 0.16f,
EnumParticleModel.Quad
);
core.VertexFlags = 255;
core.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -255f);
Api.World.SpawnParticles(core);
}
}
private void OnServerTick(float dt)
{
PillarScanResult s = ChiselRuneScanner.ScanPillarBelow(Api.World, Pos);
syncBlocks = s.ActiveBlocksCount; syncRunes = s.TotalRunesCount;
syncAnchor = s.AnchorCount; syncLight = s.LightCount; syncSwift = s.SwiftCount; syncBolt = s.BoltCount; syncSiphon = s.SiphonCount;
syncMist = s.MistCount; syncSeal = s.SealCount; syncHeat = s.HeatCount; syncFrost = s.FrostCount; syncStorage = s.StorageCount;
syncDayOnly = s.DayOnly; syncNightOnly = s.NightOnly; syncRoomMode = s.RoomMode;
activeBeams = s.BeamDirections;
targetPositions = s.TargetPositions;
syncRadius = 6.0f + s.BonusRadius;
syncSymbols = s.RecognizedRunes.Count > 0 ? string.Join(" - ", s.RecognizedRunes.ToArray()) : "";
if (activeBeams.Count > 0)
{
ArrayList dirCodes = new ArrayList();
foreach (BlockFacing bf in activeBeams) dirCodes.Add(bf.Code);
syncAreaInfo = string.Format("Зона: Луч ({0}, {1:0.0} м)", string.Join("/", dirCodes.ToArray()), syncRadius * 2.2f);
}
else if (syncRoomMode) { RebuildRoomCache(); syncAreaInfo = string.Format("Зона: Помещение ({0} бл.)", cachedRoomBlocks.Count); }
else syncAreaInfo = string.Format("Зона: Сфера (R={0:0.0} м)", syncRadius);
TrySiphon();
UpdateLiquidVoxels();
bool hasOil = oilAmount > 0;
bool timeOk = IsTimeConditionMet();
SetPillarGlassGlow(hasOil && syncRunes > 0, s.ActiveBlockPositions);
byte newLight = (hasOil && syncLight > 0 && timeOk) ? GetActiveLightLevel() : (byte)0;
if (newLight != lastLightLevel) { lastLightLevel = newLight; MarkDirty(true); Api.World.BlockAccessor.MarkBlockDirty(Pos); }
if (!hasOil) { MarkDirty(true); return; }
bowlHumCounter++;
if (bowlHumCounter >= 4)
{
bowlHumCounter = 0;
Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/tempstorm-drain"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, 0.75f, 12f, 0.22f);
}
var stabSys = Api.ModLoader.GetModSystem("Vintagestory.GameContent.SystemTemporalStability") as SystemTemporalStability;
double stab = stabSys != null ? stabSys.GetTemporalStability(Pos) : 1.0;
if (Api.World.Rand.NextDouble() < 0.025 * (1.0 + (1.0 - Math.Clamp(stab, 0.0, 1.0)) * 4.0)) oilAmount--;
if (timeOk && oilAmount > 0 && (syncAnchor > 0 || syncLight > 0 || syncSwift > 0 || syncBolt > 0 || syncMist > 0 || syncSeal > 0 || syncHeat > 0 || syncFrost > 0 || syncStorage > 0))
{
bool passive = syncLight > 0 || syncHeat > 0 || syncFrost > 0 || syncStorage > 0;
foreach (IPlayer plr in Api.World.AllOnlinePlayers)
{
if (plr?.Entity == null) continue;
bool inR = IsTargetInZone(plr.Entity.Pos.XYZ);
if (inR && syncAnchor > 0) { double ps = plr.Entity.WatchedAttributes.GetDouble("temporalStability", 1.0); if (ps < 1.0) { plr.Entity.WatchedAttributes.SetDouble("temporalStability", Math.Min(1.0, ps + 0.003 * syncAnchor)); passive = true; } }
if (inR && syncSwift > 0) { plr.Entity.Stats.Set("walkspeed", "runicgems-swift", Math.Min(0.65f, 0.15f + syncSwift * 0.10f), true); passive = true; }
else plr.Entity.Stats.Remove("walkspeed", "runicgems-swift");
}
if (passive && Api.World.Rand.NextDouble() < 0.15) oilAmount--;
ApplyMistEffect(s.ActiveBlockPositions);
ApplySealEffect();
ApplyClimateEffects();
ApplyStorageEffect();
if (syncBolt > 0 && oilAmount >= 2)
{
Vec3d center = new Vec3d(Pos.X + 0.5, Pos.Y + 0.55, Pos.Z + 0.5);
float dmg = 1.5f + (syncBolt - 1) * 0.75f;
int shots = 0;
if (targetPositions.Count > 0)
{
foreach (BlockPos tp in targetPositions)
{
if (oilAmount < 2) break;
oilAmount -= 2; shots++;
Vec3d hitPt = new Vec3d(tp.X + 0.5, tp.Y + 0.55, tp.Z + 0.5);
SpawnBoltBeam(center, hitPt);
Entity[] nearTarget = Api.World.GetEntitiesAround(hitPt, 1.8f, 2.5f, e => e != null && e.Alive && e is not EntityItem && e is not EntityPlayer && e is not EntityChiselGolem);
foreach (Entity t in nearTarget)
{
t.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Block, Type = EnumDamageType.Electricity, SourcePos = hitPt }, dmg);
Vec3d push = t.Pos.XYZ.Sub(hitPt).Normalize(); t.Pos.Motion.Add(push.X * 0.25, 0.15, push.Z * 0.25);
}
}
}
else
{
float scanR = activeBeams.Count > 0 ? syncRadius * 2.3f : (syncRoomMode ? 12f : syncRadius);
Entity[] ents = Api.World.GetEntitiesAround(center, scanR, 8f, e => e != null && e.Alive && e is not EntityItem && e is not EntityPlayer && e is not EntityChiselGolem && IsTargetInZone(e.Pos.XYZ));
foreach (Entity t in ents)
{
if (oilAmount < 2) break;
oilAmount -= 2; shots++;
t.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Block, Type = EnumDamageType.Electricity, SourcePos = center }, dmg);
Vec3d push = t.Pos.XYZ.Sub(center).Normalize(); t.Pos.Motion.Add(push.X * 0.25, 0.15, push.Z * 0.25);
Vec3d hit = t.Pos.XYZ.Add(0, t.CollisionBox.Y2 * 0.5, 0);
SpawnBoltBeam(center, hit);
}
}
if (shots > 0) Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-break"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, true, 24f);
}
}
if (oilAmount < 0) oilAmount = 0;
MarkDirty(true);
}
}
}
