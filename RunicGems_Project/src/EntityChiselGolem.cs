using System;
using System.Collections;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
namespace RunicGems
{
public class EntityChiselGolem : EntityAgent
{
private float tickAccum;
private float stepAnimAccum;
private float humSoundAccum;
private float meleeCooldown;
private float jumpCooldown;
private float jumpPoseTimer;
private float stuckTimer;
private float roamWaitTimer;
private Vec3d lastStuckPos;
private float avoidSide = 1f;
private bool walkToggle;
private float actionPoseTimer;
private int patrolIndex = 1;
private ItemStack carriedStack;
private Vec3d moveTarget;
private float moveSpeed = 0.011f;
private int mistWaterCharge;
public long ForcedTargetId;
public int HeightBlocks
{
get => WatchedAttributes.GetInt("heightBlocks", 1);
set { WatchedAttributes.SetInt("heightBlocks", value); WatchedAttributes.MarkPathDirty("heightBlocks"); }
}
public int CapacityMl
{
get => WatchedAttributes.GetInt("capacityMl", 100);
set { WatchedAttributes.SetInt("capacityMl", value); WatchedAttributes.MarkPathDirty("capacityMl"); }
}
public int OilMl
{
get => WatchedAttributes.GetInt("oilMl", 0);
set { WatchedAttributes.SetInt("oilMl", value); WatchedAttributes.MarkPathDirty("oilMl"); }
}
public int CurrentFrame
{
get => WatchedAttributes.GetInt("curFrame", 4);
set
{
if (WatchedAttributes.GetInt("curFrame", -1) != value)
{
WatchedAttributes.SetInt("curFrame", value);
WatchedAttributes.MarkPathDirty("curFrame");
}
}
}
public bool IsWaiting
{
get => WatchedAttributes.GetBool("isWaiting", false);
set { WatchedAttributes.SetBool("isWaiting", value); WatchedAttributes.MarkPathDirty("isWaiting"); }
}
public string OwnerUid
{
get => WatchedAttributes.GetString("ownerUid", "");
set { WatchedAttributes.SetString("ownerUid", value); WatchedAttributes.MarkPathDirty("ownerUid"); }
}
public string CarriedInfo
{
get => WatchedAttributes.GetString("carriedInfo", "");
set { WatchedAttributes.SetString("carriedInfo", value); WatchedAttributes.MarkPathDirty("carriedInfo"); }
}
public string CarriedCode
{
get => WatchedAttributes.GetString("carriedCode", "");
set { WatchedAttributes.SetString("carriedCode", value); WatchedAttributes.MarkPathDirty("carriedCode"); }
}
public string CarriedClass
{
get => WatchedAttributes.GetString("carriedClass", "");
set { WatchedAttributes.SetString("carriedClass", value); WatchedAttributes.MarkPathDirty("carriedClass"); }
}
public bool IsPowered => OilMl > 0 && Alive;
public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
{
base.Initialize(properties, api, InChunkIndex3d);
ApplyHeightBounds();
}
private void ApplyHeightBounds()
{
if (HeightBlocks >= 2)
{
SetCollisionBox(0.85f, 1.85f);
SetSelectionBox(0.85f, 1.85f);
}
else
{
SetCollisionBox(0.75f, 0.95f);
SetSelectionBox(0.75f, 0.95f);
}
}
public override bool ReceiveDamage(DamageSource damageSource, float damage)
{
if (World.Side != EnumAppSide.Server) return false;
if (OilMl <= 0)
{
ClearMobAggro(damageSource?.SourceEntity);
return false;
}
if (damage > 0f)
{
if (damageSource?.SourceEntity != null && !(damageSource.SourceEntity is EntityPlayer) && !(damageSource.SourceEntity is EntityChiselGolem))
{
ForcedTargetId = damageSource.SourceEntity.EntityId;
}
int loss = Math.Max(15, (int)Math.Round(damage * 25f));
OilMl = Math.Max(0, OilMl - loss);
World.PlaySoundAt(new AssetLocation("game:sounds/effect/anvilhit"), this, null);
if (OilMl == 0)
{
DeactivateAndDropAggro();
ClearMobAggro(damageSource?.SourceEntity);
}
}
return base.ReceiveDamage(damageSource, 0f);
}
public void LoadFrames(ArrayList framesList, string ownerUid)
{
object[] arr = framesList.ToArray();
ChiselFrameData f0 = arr.GetValue(0) as ChiselFrameData;
CapacityMl = f0 != null ? f0.CapacityMl : 100;
HeightBlocks = f0 != null ? f0.HeightBlocks : 1;
OilMl = 0;
OwnerUid = ownerUid ?? "";
CurrentFrame = 4;
for (int i = 0; i < 6 && i < arr.Length; i++)
{
ChiselFrameData fd = arr.GetValue(i) as ChiselFrameData;
if (fd == null) continue;
WatchedAttributes.SetBytes("f_cuboids_" + i, fd.CuboidsBytes);
WatchedAttributes.SetString("f_codes_" + i, fd.BlockCodes);
WatchedAttributes.SetString("f_runes_" + i, fd.RunesCsv ?? "");
if (fd.TopCuboidsBytes != null && fd.TopCuboidsBytes.Length > 0)
{
WatchedAttributes.SetBytes("f_top_cuboids_" + i, fd.TopCuboidsBytes);
WatchedAttributes.SetString("f_top_codes_" + i, fd.TopBlockCodes ?? "");
}
}
RebuildLegacyGolemData();
ApplyHeightBounds();
WatchedAttributes.MarkAllDirty();
}
public string GetActiveRunes()
{
string baseRunes = WatchedAttributes.GetString("f_runes_0", "");
if (CurrentFrame == 3)
{
string actionRunes = WatchedAttributes.GetString("f_runes_3", "");
if (!string.IsNullOrEmpty(actionRunes)) return baseRunes + "," + actionRunes;
}
return baseRunes;
}
private bool HasRuneAnyFrame(string runeName)
{
return WatchedAttributes.GetString("f_runes_0", "").Contains(runeName)
|| WatchedAttributes.GetString("f_runes_3", "").Contains(runeName);
}
private void HaltMovement()
{
moveTarget = null;
stuckTimer = 0f;
Controls.Forward = false;
Controls.Jump = false;
Controls.WalkVector.Set(0, 0, 0);
Controls.StopAllMovement();
}
private void ClearMobAggro(Entity mob)
{
if (mob == null) return;
var emo = mob.GetBehavior("emotionstates") as EntityBehaviorEmotionStates;
emo?.ClearStates();
var taskAi = mob.GetBehavior("taskai") as EntityBehaviorTaskAI;
if (taskAi?.TaskManager != null)
{
taskAi.TaskManager.StopTasks();
}
}
private void DeactivateAndDropAggro()
{
HaltMovement();
ForcedTargetId = 0;
CurrentFrame = 4;
World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-break"), this, null);
Entity[] nearby = World.GetEntitiesAround(Pos.XYZ, 14f, 6f, e => e != null && e.Alive && e != this && e is EntityAgent && !(e is EntityPlayer));
foreach (Entity mob in nearby)
{
ClearMobAggro(mob);
}
}
private bool IsHostileTarget(Entity e)
{
if (e == null || !e.Alive || e == this || e is EntityPlayer || e is EntityChiselGolem) return false;
if (e is not EntityAgent) return false;
if (e.EntityId == ForcedTargetId) return true;
string path = e.Code?.Path ?? "";
if (path.Contains("drifter") || path.Contains("wolf") || path.Contains("bear") || path.Contains("hyena") || path.Contains("locust") || path.Contains("bell") || path.Contains("sawblade") || path.Contains("bowtorn") || path.Contains("shiver") || path.Contains("eidolon") || path.Contains("tainted"))
return true;
return false;
}
private void ProvokeEnemy(Entity target)
{
if (target == null || !IsPowered) return;
target.Notify("attacked", this);
var taskAi = target.GetBehavior("taskai") as EntityBehaviorTaskAI;
if (taskAi?.TaskManager != null)
{
taskAi.TaskManager.OnEntityHurt(new DamageSource { Source = EnumDamageSource.Entity, SourceEntity = this, Type = EnumDamageType.BluntAttack }, 0.4f);
}
}
private bool IsBlockSolidAt(double wx, double wy, double wz)
{
BlockPos bp = new BlockPos((int)Math.Floor(wx), (int)Math.Floor(wy), (int)Math.Floor(wz));
Block b = World.BlockAccessor.GetBlock(bp);
return b != null && b.Id != 0 && b.CollisionBoxes != null && b.CollisionBoxes.Length > 0;
}
private void NavigateWithJumpAndAvoid(double targetX, double targetZ, float dt)
{
double dx = targetX - Pos.X;
double dz = targetZ - Pos.Z;
float desiredYaw = (float)Math.Atan2(dx, dz);
double fwdX = Math.Sin(desiredYaw);
double fwdZ = Math.Cos(desiredYaw);
double checkDist = HeightBlocks >= 2 ? 0.72 : 0.62;
double frontX = Pos.X + fwdX * checkDist;
double frontZ = Pos.Z + fwdZ * checkDist;
bool blockedFoot = IsBlockSolidAt(frontX, Pos.Y + 0.15, frontZ);
bool blockedHead = IsBlockSolidAt(frontX, Pos.Y + 1.15, frontZ);
bool blockedTop = HeightBlocks >= 2 && IsBlockSolidAt(frontX, Pos.Y + 2.05, frontZ);
if (blockedFoot && !blockedHead && !blockedTop && OnGround && jumpCooldown <= 0f)
{
jumpCooldown = 0.65f;
jumpPoseTimer = 0.45f;
Controls.Jump = true;
Pos.Motion.Y = HeightBlocks >= 2 ? 0.44 : 0.41;
World.PlaySoundAt(new AssetLocation("game:sounds/walk/stone1"), this, null, 0.7f, 14f, 0.55f);
}
else
{
Controls.Jump = false;
}
if (lastStuckPos == null) lastStuckPos = Pos.XYZ.Clone();
stuckTimer += dt;
if (stuckTimer >= 0.45f)
{
double movedDist = Pos.XYZ.HorizontalSquareDistanceTo(lastStuckPos);
if (movedDist < 0.015)
{
if (World.Rand.NextDouble() < 0.35) avoidSide = -avoidSide;
}
else
{
stuckTimer = 0f;
}
lastStuckPos = Pos.XYZ.Clone();
}
float moveYaw = desiredYaw;
if ((blockedFoot && (blockedHead || blockedTop)) || stuckTimer >= 0.45f)
{
float leftYaw = desiredYaw + 1.15f;
float rightYaw = desiredYaw - 1.15f;
bool leftBlocked = IsBlockSolidAt(Pos.X + Math.Sin(leftYaw) * checkDist, Pos.Y + 0.25, Pos.Z + Math.Cos(leftYaw) * checkDist);
bool rightBlocked = IsBlockSolidAt(Pos.X + Math.Sin(rightYaw) * checkDist, Pos.Y + 0.25, Pos.Z + Math.Cos(rightYaw) * checkDist);
if (!leftBlocked && rightBlocked) avoidSide = 1f;
else if (!rightBlocked && leftBlocked) avoidSide = -1f;
moveYaw = desiredYaw + avoidSide * 1.18f;
}
Pos.Yaw = moveYaw;
Controls.WalkVector.Set(Math.Sin(moveYaw) * moveSpeed, 0, Math.Cos(moveYaw) * moveSpeed);
Controls.Forward = true;
}
private BlockEntityChiselRuneBowl FindNearestActiveBowl(int searchR)
{
BlockPos c = Pos.AsBlockPos;
BlockEntityChiselRuneBowl best = null;
double bestDist = double.MaxValue;
for (int dx = -searchR; dx <= searchR; dx++)
{
for (int dy = -4; dy <= 4; dy++)
{
for (int dz = -searchR; dz <= searchR; dz++)
{
BlockPos p = c.AddCopy(dx, dy, dz);
if (World.BlockAccessor.GetBlockEntity(p) is BlockEntityChiselRuneBowl bowl && bowl.IsActiveForGolems)
{
double d = dx * dx + dy * dy + dz * dz;
if (d < bestDist && d <= (bowl.ActiveEffectRadius + 4.0) * (bowl.ActiveEffectRadius + 4.0))
{
bestDist = d;
best = bowl;
}
}
}
}
}
return best;
}
private BlockPos FindNearestTargetRuneBlock(int searchR)
{
BlockPos c = Pos.AsBlockPos;
BlockPos best = null;
double bestDist = double.MaxValue;
for (int dx = -searchR; dx <= searchR; dx++)
{
for (int dy = -3; dy <= 3; dy++)
{
for (int dz = -searchR; dz <= searchR; dz++)
{
BlockPos p = c.AddCopy(dx, dy, dz);
if (World.BlockAccessor.GetBlockEntity(p) is BlockEntityChisel)
{
PillarScanResult sr = ChiselRuneScanner.ScanPillarBelow(World, p);
if (sr.TargetPositions.Count > 0)
{
BlockPos tp = sr.TargetPositions[0] as BlockPos;
double d = Pos.XYZ.SquareDistanceTo(tp.X + 0.5, tp.Y + 0.5, tp.Z + 0.5);
if (d < bestDist) { bestDist = d; best = tp; }
}
}
}
}
}
return best;
}
public override void OnGameTick(float dt)
{
base.OnGameTick(dt);
if (World.Side != EnumAppSide.Server || !Alive) return;
if (!IsPowered)
{
HaltMovement();
CurrentFrame = 4;
tickAccum += dt;
if (tickAccum >= 1.0f)
{
tickAccum = 0f;
Entity[] closeMobs = World.GetEntitiesAround(Pos.XYZ, 4f, 3f, e => e != null && e.Alive && e != this && e is EntityAgent && !(e is EntityPlayer));
foreach (Entity mob in closeMobs) ClearMobAggro(mob);
}
return;
}
if (meleeCooldown > 0f) meleeCooldown -= dt;
if (jumpCooldown > 0f) jumpCooldown -= dt;
if (jumpPoseTimer > 0f) jumpPoseTimer -= dt;
if (roamWaitTimer > 0f) roamWaitTimer -= dt;
humSoundAccum += dt;
if (humSoundAccum >= 4.0f)
{
humSoundAccum = 0f;
World.PlaySoundAt(new AssetLocation("game:sounds/effect/tempstorm-drain"), this, null, 0.75f, 12f, 0.22f);
}
bool hasFollow = HasRuneAnyFrame("Follow");
bool hasPatrol = HasRuneAnyFrame("Patrol");
bool hasGuard = HasRuneAnyFrame("Guard");
bool hasCarry = HasRuneAnyFrame("Carry");
bool hasRoam = HasRuneAnyFrame("Roam");
if (actionPoseTimer > 0f)
{
actionPoseTimer -= dt;
CurrentFrame = 3;
}
else if (jumpPoseTimer > 0f || !OnGround)
{
CurrentFrame = 5;
if (moveTarget != null)
{
NavigateWithJumpAndAvoid(moveTarget.X, moveTarget.Z, dt);
}
}
else if (moveTarget != null)
{
bool reachedOwnerZone = false;
if (hasFollow && !IsWaiting && ForcedTargetId == 0 && !string.IsNullOrEmpty(OwnerUid))
{
IPlayer owner = World.PlayerByUid(OwnerUid);
if (owner?.Entity != null && Pos.DistanceTo(owner.Entity.Pos) <= 3.0)
{
reachedOwnerZone = true;
}
}
double dx = moveTarget.X - Pos.X;
double dz = moveTarget.Z - Pos.Z;
if (!reachedOwnerZone && (dx * dx + dz * dz > 0.35))
{
NavigateWithJumpAndAvoid(moveTarget.X, moveTarget.Z, dt);
stepAnimAccum += dt;
if (stepAnimAccum >= 0.35f)
{
stepAnimAccum = 0f;
walkToggle = !walkToggle;
int stepIdx = 1 + World.Rand.Next(4);
float pitch = (HeightBlocks >= 2 ? 0.68f : 0.85f) + (float)World.Rand.NextDouble() * 0.18f;
World.PlaySoundAt(new AssetLocation("game:sounds/walk/stone" + stepIdx), this, null, pitch, 16f, HeightBlocks >= 2 ? 0.65f : 0.45f);
}
CurrentFrame = walkToggle ? 1 : 2;
}
else
{
HaltMovement();
CurrentFrame = 0;
if (hasRoam) roamWaitTimer = 2.0f + (float)World.Rand.NextDouble() * 2.5f;
}
}
else
{
Controls.Forward = false;
Controls.Jump = false;
Controls.WalkVector.Set(0, 0, 0);
CurrentFrame = 0;
}
tickAccum += dt;
if (tickAccum < 0.5f) return;
tickAccum = 0f;
string runes = GetActiveRunes();
bool moving = moveTarget != null;
int drain = moving ? 2 : 1;
if (moving && runes.Contains("Swift")) drain += 1;
if (runes.Contains("Light")) drain += 1;
if (World.Rand.NextDouble() < 0.5)
{
OilMl = Math.Max(0, OilMl - drain);
}
if (OilMl == 0)
{
DeactivateAndDropAggro();
return;
}
ApplyRunes(runes);
UpdateBehaviorAI(runes, hasFollow, hasPatrol, hasGuard, hasCarry, hasRoam);
}
private void ApplyRunes(string runes)
{
if (runes.Contains("Siphon") && OilMl < CapacityMl)
{
TrySiphonNearby();
}
if (runes.Contains("Anchor"))
{
foreach (IPlayer plr in World.AllOnlinePlayers)
{
if (plr.Entity != null && plr.Entity.Pos.DistanceTo(Pos) <= 6.0)
{
double stab = plr.Entity.WatchedAttributes.GetDouble("temporalStability", 1.0);
if (stab < 1.0)
plr.Entity.WatchedAttributes.SetDouble("temporalStability", Math.Min(1.0, stab + 0.005));
}
}
}
if (HasRuneAnyFrame("Mist"))
{
ApplyGolemMist();
}
if (runes.Contains("Seal") && OilMl >= 4)
{
var riftSys = Api.ModLoader.GetModSystem("Vintagestory.GameContent.ModSystemRifts") as ModSystemRifts;
if (riftSys?.riftsById != null)
{
foreach (Rift rift in riftSys.riftsById.Values)
{
if (rift?.Position != null && rift.Size > 0.05f && rift.Position.DistanceTo(Pos.XYZ) <= 7.0)
{
rift.Size = Math.Max(0f, rift.Size - 0.15f);
rift.DieAtTotalHours = Math.Min(rift.DieAtTotalHours, World.Calendar.TotalHours + 0.02);
OilMl = Math.Max(0, OilMl - 4);
SpawnBoltBeam(Pos.XYZ.Add(0, HeightBlocks >= 2 ? 1.25 : 0.55, 0), rift.Position.AddCopy(0, 0.8, 0));
break;
}
}
}
}
if (runes.Contains("Heat") || runes.Contains("Frost"))
{
foreach (IPlayer plr in World.AllOnlinePlayers)
{
if (plr.Entity != null && plr.Entity.Pos.DistanceTo(Pos) <= 5.0)
{
var tempBh = plr.Entity.GetBehavior("bodytemperature") as EntityBehaviorBodyTemperature;
if (tempBh != null)
{
if (runes.Contains("Heat") && tempBh.CurBodyTemperature < 37.0f)
tempBh.CurBodyTemperature = Math.Min(37.0f, tempBh.CurBodyTemperature + 0.2f);
if (runes.Contains("Frost") && tempBh.CurBodyTemperature > 36.8f)
tempBh.CurBodyTemperature = Math.Max(36.8f, tempBh.CurBodyTemperature - 0.2f);
}
}
}
}
bool canBolt = HasRuneAnyFrame("Bolt");
if (canBolt && OilMl >= 25 && meleeCooldown <= 0f)
{
float boltRange = IsWaiting ? 5f : (HeightBlocks >= 2 ? 9f : 7f);
BlockPos targetBlock = FindNearestTargetRuneBlock((int)boltRange);
if (targetBlock != null)
{
actionPoseTimer = 0.6f;
meleeCooldown = 1.4f;
CurrentFrame = 3;
OilMl = Math.Max(0, OilMl - 25);
Vec3d start = Pos.XYZ.Add(0, HeightBlocks >= 2 ? 1.25 : 0.55, 0);
Vec3d hitPt = new Vec3d(targetBlock.X + 0.5, targetBlock.Y + 0.55, targetBlock.Z + 0.5);
Pos.Yaw = (float)Math.Atan2(hitPt.X - Pos.X, hitPt.Z - Pos.Z);
SpawnBoltBeam(start, hitPt);
float boltDmg = HeightBlocks >= 2 ? 1.5f : 0.9f;
Entity[] nearTarget = World.GetEntitiesAround(hitPt, 1.8f, 2.5f, e => e != null && e.Alive && e is not EntityItem && e is not EntityPlayer && e is not EntityChiselGolem);
foreach (Entity t in nearTarget)
{
t.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Entity, SourceEntity = this, Type = EnumDamageType.Electricity }, boltDmg);
if (OilMl > 0) ProvokeEnemy(t);
}
if (OilMl <= 0) DeactivateAndDropAggro();
World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-break"), this, null);
}
else
{
Entity target = ForcedTargetId != 0
? World.GetEntityById(ForcedTargetId)
: World.GetNearestEntity(Pos.XYZ, boltRange, 5f, e => IsHostileTarget(e));
if (target != null && target.Alive && target.Pos.DistanceTo(Pos) <= boltRange + 0.5f)
{
actionPoseTimer = 0.6f;
meleeCooldown = 1.4f;
CurrentFrame = 3;
OilMl = Math.Max(0, OilMl - 25);
Pos.Yaw = (float)Math.Atan2(target.Pos.X - Pos.X, target.Pos.Z - Pos.Z);
Vec3d start = Pos.XYZ.Add(0, HeightBlocks >= 2 ? 1.25 : 0.55, 0);
Vec3d hit = target.Pos.XYZ.Add(0, target.CollisionBox.Y2 * 0.5, 0);
SpawnBoltBeam(start, hit);
float boltDmg = HeightBlocks >= 2 ? 1.5f : 0.9f;
target.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Entity, SourceEntity = this, Type = EnumDamageType.Electricity }, boltDmg);
if (OilMl > 0) ProvokeEnemy(target);
else DeactivateAndDropAggro();
World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-break"), this, null);
}
}
}
}
private void ApplyGolemMist()
{
BlockPos c = Pos.AsBlockPos;
bool foundWater = false;
for (int dx = -4; dx <= 4 && !foundWater; dx++)
for (int dy = -2; dy <= 2 && !foundWater; dy++)
for (int dz = -4; dz <= 4 && !foundWater; dz++)
{
BlockPos p = c.AddCopy(dx, dy, dz);
Block b = World.BlockAccessor.GetBlock(p);
if (b != null && b.LiquidCode == "water") { foundWater = true; break; }
if (b is BlockLiquidContainerBase cont)
{
ItemStack cnt = cont.GetContent(p);
if (cnt != null && cnt.StackSize > 0 && (cnt.Collectible?.Code?.Path?.Contains("water") ?? false))
{
foundWater = true; break;
}
}
}
if (foundWater)
{
mistWaterCharge = HeightBlocks >= 2 ? 120 : 60;
WatchedAttributes.SetInt("mistWater", mistWaterCharge);
}
if (mistWaterCharge <= 0) return;
int watered = 0;
for (int dx = -3; dx <= 3; dx++)
for (int dy = -2; dy <= 1; dy++)
for (int dz = -3; dz <= 3; dz++)
{
BlockPos fp = c.AddCopy(dx, dy, dz);
if (World.BlockAccessor.GetBlockEntity(fp) is BlockEntityFarmland farm && farm.MoistureLevel < 0.98f)
{
farm.WaterFarmland(0.35f, false);
watered++;
World.SpawnParticles(2, ColorUtil.ToRgba(130, 195, 245, 255),
new Vec3d(fp.X + 0.15, fp.Y + 1.02, fp.Z + 0.15),
new Vec3d(fp.X + 0.85, fp.Y + 1.25, fp.Z + 0.85),
new Vec3f(-0.02f, 0.015f, -0.02f),
new Vec3f(0.02f, 0.035f, 0.02f),
1.3f, 0f, 0.45f, EnumParticleModel.Quad);
}
}
if (watered > 0)
{
mistWaterCharge = Math.Max(0, mistWaterCharge - 1);
WatchedAttributes.SetInt("mistWater", mistWaterCharge);
if (World.Rand.NextDouble() < 0.35)
{
World.PlaySoundAt(new AssetLocation("game:sounds/effect/water-fill"), this, null, 1.3f, 10f, 0.25f);
}
}
}
private void SpawnBoltBeam(Vec3d start, Vec3d hit)
{
Vec3d d = hit.SubCopy(start);
Vec3d m1 = start.AddCopy(d.X * 0.33 + (World.Rand.NextDouble() - 0.5) * 0.5, d.Y * 0.33 + 0.25, d.Z * 0.33 + (World.Rand.NextDouble() - 0.5) * 0.5);
Vec3d m2 = start.AddCopy(d.X * 0.66 + (World.Rand.NextDouble() - 0.5) * 0.5, d.Y * 0.66 - 0.15, d.Z * 0.66 + (World.Rand.NextDouble() - 0.5) * 0.5);
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
World.SpawnParticles(shards);
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
World.SpawnParticles(outer);
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
World.SpawnParticles(core);
}
}
private void SpawnTransferArc(Vec3d from, Vec3d to)
{
for (int i = 0; i <= 12; i++)
{
double t = i / 12.0;
Vec3d pt = new Vec3d(
from.X + (to.X - from.X) * t,
from.Y + (to.Y - from.Y) * t + Math.Sin(t * Math.PI) * 0.45,
from.Z + (to.Z - from.Z) * t
);
var p = new SimpleParticleProperties(
1, 2,
ColorUtil.ToRgba(235, 60, 245, 225),
pt, pt,
new Vec3f(-0.03f, 0.02f, -0.03f),
new Vec3f(0.03f, 0.06f, 0.03f),
0.45f, 0f, 0.14f, 0.22f,
EnumParticleModel.Quad
);
p.VertexFlags = 255;
p.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -210f);
World.SpawnParticles(p);
}
}
private void SyncCarriedVisuals()
{
if (carriedStack == null || carriedStack.StackSize <= 0)
{
CarriedInfo = "";
CarriedCode = "";
CarriedClass = "";
return;
}
CarriedInfo = carriedStack.GetName() + " x" + carriedStack.StackSize;
CarriedCode = carriedStack.Collectible?.Code?.ToString() ?? "";
CarriedClass = carriedStack.Class == EnumItemClass.Block ? "block" : "item";
}
private void TrySiphonNearby()
{
BlockPos c = Pos.AsBlockPos;
for (int dx = -2; dx <= 2; dx++)
for (int dy = -1; dy <= 2; dy++)
for (int dz = -2; dz <= 2; dz++)
{
if (OilMl >= CapacityMl) return;
BlockPos p = c.AddCopy(dx, dy, dz);
BlockEntityContainer cont = World.BlockAccessor.GetBlockEntity(p) as BlockEntityContainer;
if (cont?.Inventory == null) continue;
foreach (ItemSlot s in cont.Inventory)
{
if (!s.Empty && s.Itemstack.Collectible?.Code?.ToString() == "runicgems:temporaloil")
{
int take = Math.Min(s.StackSize, (int)Math.Ceiling((CapacityMl - OilMl) / 10.0));
if (take > 0)
{
s.TakeOut(take);
s.MarkDirty();
OilMl = Math.Min(CapacityMl, OilMl + take * 10);
World.PlaySoundAt(new AssetLocation("game:sounds/effect/water-fill"), this, null, 1.1f, 12f, 0.35f);
return;
}
}
}
}
}
private void UpdateBehaviorAI(string runes, bool hasFollow, bool hasPatrol, bool hasGuard, bool hasCarry, bool hasRoam)
{
moveSpeed = runes.Contains("Swift") ? 0.017f : 0.011f;
IPlayer owner = !string.IsNullOrEmpty(OwnerUid) ? World.PlayerByUid(OwnerUid) : null;
if (hasGuard)
{
float aggroRadius = IsWaiting ? 5.5f : 10.0f;
Entity target = null;
if (ForcedTargetId != 0)
{
target = World.GetEntityById(ForcedTargetId);
if (target == null || !target.Alive || target.Pos.DistanceTo(Pos) > (IsWaiting ? 8.0 : 24.0))
{
ForcedTargetId = 0;
target = null;
}
}
if (target == null)
{
target = World.GetNearestEntity(Pos.XYZ, aggroRadius, 5f, e => IsHostileTarget(e));
if (target == null && hasFollow && !IsWaiting && owner?.Entity != null)
{
target = World.GetNearestEntity(owner.Entity.Pos.XYZ, 10f, 5f, e => IsHostileTarget(e));
}
}
if (target != null && OilMl >= 12)
{
double reach = HeightBlocks >= 2 ? 1.85 : 1.35;
double distToEnemy = Pos.DistanceTo(target.Pos);
if (distToEnemy <= reach)
{
HaltMovement();
Pos.Yaw = (float)Math.Atan2(target.Pos.X - Pos.X, target.Pos.Z - Pos.Z);
if (meleeCooldown <= 0f)
{
meleeCooldown = 1.2f;
actionPoseTimer = 0.45f;
CurrentFrame = 3;
OilMl = Math.Max(0, OilMl - 12);
float hitDmg = HeightBlocks >= 2 ? 0.8f : 0.4f;
target.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Entity, SourceEntity = this, Type = EnumDamageType.BluntAttack }, hitDmg);
if (OilMl > 0) ProvokeEnemy(target);
else DeactivateAndDropAggro();
Vec3d push = target.Pos.XYZ.Sub(Pos.XYZ).Normalize();
double kb = HeightBlocks >= 2 ? 0.35 : 0.22;
target.Pos.Motion.Add(push.X * kb, 0.14, push.Z * kb);
World.PlaySoundAt(new AssetLocation("game:sounds/effect/anvilhit"), this, null, HeightBlocks >= 2 ? 0.7f : 0.85f, 16f, 0.7f);
}
}
else if (hasFollow || hasPatrol || hasRoam)
{
moveTarget = target.Pos.XYZ.Clone();
if (distToEnemy <= 4.5) ProvokeEnemy(target);
}
return;
}
}
if (hasPatrol)
{
RunPatrolStep(hasCarry);
return;
}
if (hasRoam)
{
if (hasCarry) TryCarryNearCurrentPos();
BlockEntityChiselRuneBowl bowl = FindNearestActiveBowl(18);
if (bowl != null)
{
double r = Math.Max(2.0, bowl.ActiveEffectRadius - 1.0);
Vec3d bCenter = new Vec3d(bowl.Pos.X + 0.5, bowl.Pos.Y, bowl.Pos.Z + 0.5);
if (Pos.XYZ.HorizontalSquareDistanceTo(bCenter) > (r + 1.5) * (r + 1.5))
{
moveTarget = bCenter.Clone();
}
else if (moveTarget == null && roamWaitTimer <= 0f)
{
double ang = World.Rand.NextDouble() * Math.PI * 2.0;
double dist = 1.2 + World.Rand.NextDouble() * (r - 1.2);
moveTarget = new Vec3d(bCenter.X + Math.Cos(ang) * dist, Pos.Y, bCenter.Z + Math.Sin(ang) * dist);
}
}
else
{
HaltMovement();
}
return;
}
if (hasFollow)
{
if (hasCarry)
{
TryCarryNearCurrentPos();
}
if (IsWaiting || owner?.Entity == null)
{
HaltMovement();
return;
}
double dist = Pos.DistanceTo(owner.Entity.Pos);
if (dist > 4.2 && dist < 32.0)
{
moveTarget = owner.Entity.Pos.XYZ.Clone();
}
else if (dist <= 3.0)
{
HaltMovement();
}
return;
}
if (hasCarry)
{
TryCarryNearCurrentPos();
}
HaltMovement();
}
private void TryCarryNearCurrentPos()
{
if (actionPoseTimer > 0f) return;
BlockPos center = Pos.AsBlockPos;
BlockPos pickupPos = null;
BlockPos dropPos = null;
World.BlockAccessor.WalkBlocks(center.AddCopy(-3, -2, -3), center.AddCopy(3, 2, 3), (block, x, y, z) =>
{
if (block.Code?.Domain != "runicgems") return;
if (block.Code.Path == "runemark-pickup") pickupPos = new BlockPos(x, y, z);
else if (block.Code.Path == "runemark-drop") dropPos = new BlockPos(x, y, z);
});
if (pickupPos != null && carriedStack == null && TryTransferContainer(pickupPos, true))
{
actionPoseTimer = 0.7f;
CurrentFrame = 3;
World.PlaySoundAt(new AssetLocation("game:sounds/effect/latch"), this, null, 0.95f, 14f, 0.6f);
}
else if (dropPos != null && carriedStack != null && TryTransferContainer(dropPos, false))
{
actionPoseTimer = 0.7f;
CurrentFrame = 3;
World.PlaySoundAt(new AssetLocation("game:sounds/effect/latch"), this, null, 1.15f, 14f, 0.6f);
}
}
private void RunPatrolStep(bool canCarry)
{
if (actionPoseTimer > 0f) return;
BlockPos center = Pos.AsBlockPos;
BlockPos foundPoint = null;
BlockPos pickupPos = null;
BlockPos dropPos = null;
World.BlockAccessor.WalkBlocks(center.AddCopy(-16, -4, -16), center.AddCopy(16, 4, 16), (block, x, y, z) =>
{
if (block.Code?.Domain != "runicgems") return;
string p = block.Code.Path;
if (p == "runemark-point" + patrolIndex) foundPoint = new BlockPos(x, y, z);
else if (p == "runemark-pickup") pickupPos = new BlockPos(x, y, z);
else if (p == "runemark-drop") dropPos = new BlockPos(x, y, z);
});
if (foundPoint == null) { patrolIndex = (patrolIndex % 3) + 1; return; }
Vec3d dest = foundPoint.ToVec3d().Add(0.5, 0, 0.5);
if (Pos.XYZ.DistanceTo(dest) > 1.5)
{
moveTarget = dest;
return;
}
HaltMovement();
if (canCarry)
{
if (pickupPos != null && pickupPos.DistanceTo(foundPoint) <= 3 && carriedStack == null)
{
if (TryTransferContainer(pickupPos, true))
{
actionPoseTimer = 0.7f;
CurrentFrame = 3;
World.PlaySoundAt(new AssetLocation("game:sounds/effect/latch"), this, null, 0.95f, 14f, 0.6f);
return;
}
}
if (dropPos != null && dropPos.DistanceTo(foundPoint) <= 3 && carriedStack != null)
{
if (TryTransferContainer(dropPos, false))
{
actionPoseTimer = 0.7f;
CurrentFrame = 3;
World.PlaySoundAt(new AssetLocation("game:sounds/effect/latch"), this, null, 1.15f, 14f, 0.6f);
return;
}
}
}
patrolIndex = (patrolIndex % 3) + 1;
}
private bool TryTransferContainer(BlockPos markPos, bool isPickup)
{
int maxTake = HeightBlocks >= 2 ? 32 : 16;
for (int dx = -2; dx <= 2; dx++)
{
for (int dy = -1; dy <= 1; dy++)
{
for (int dz = -2; dz <= 2; dz++)
{
BlockPos p = markPos.AddCopy(dx, dy, dz);
BlockEntityContainer cont = World.BlockAccessor.GetBlockEntity(p) as BlockEntityContainer;
if (cont?.Inventory == null) continue;
if (isPickup)
{
foreach (ItemSlot slot in cont.Inventory)
{
if (!slot.Empty)
{
carriedStack = slot.TakeOut(Math.Min(maxTake, slot.StackSize));
slot.MarkDirty();
cont.MarkDirty(true);
if (carriedStack != null)
{
Pos.Yaw = (float)Math.Atan2((p.X + 0.5) - Pos.X, (p.Z + 0.5) - Pos.Z);
SpawnTransferArc(new Vec3d(p.X + 0.5, p.Y + 0.6, p.Z + 0.5), Pos.XYZ.Add(0, HeightBlocks >= 2 ? 1.1 : 0.65, 0));
SyncCarriedVisuals();
return true;
}
}
}
}
else if (carriedStack != null)
{
var dummySlot = new DummySlot(carriedStack);
bool movedAny = false;
foreach (ItemSlot slot in cont.Inventory)
{
if (dummySlot.TryPutInto(World, slot, carriedStack.StackSize) > 0)
{
slot.MarkDirty();
movedAny = true;
if (dummySlot.Empty)
{
carriedStack = null;
break;
}
}
}
if (movedAny)
{
cont.MarkDirty(true);
Pos.Yaw = (float)Math.Atan2((p.X + 0.5) - Pos.X, (p.Z + 0.5) - Pos.Z);
SpawnTransferArc(Pos.XYZ.Add(0, HeightBlocks >= 2 ? 1.1 : 0.65, 0), new Vec3d(p.X + 0.5, p.Y + 0.6, p.Z + 0.5));
SyncCarriedVisuals();
return true;
}
}
}
}
}
return false;
}
        public ItemStack PackToItemStack()
{
Item golemItem = World.GetItem(new AssetLocation("runicgems", "golemhead"));
if (golemItem == null) return null;
ItemStack stack = new ItemStack(golemItem);
stack.Attributes.SetBool("isPackedGolem", true);
stack.Attributes.SetInt("heightBlocks", HeightBlocks);
stack.Attributes.SetInt("capacityMl", CapacityMl);
stack.Attributes.SetInt("oilMl", OilMl);
stack.Attributes.SetString("ownerUid", OwnerUid ?? "");
byte[] f0 = WatchedAttributes.GetBytes("f_cuboids_0");
string c0 = WatchedAttributes.GetString("f_codes_0", "");
if (f0 != null) stack.Attributes.SetBytes("cuboids", f0);
if (!string.IsNullOrEmpty(c0)) stack.Attributes.SetString("blockCodes", c0);
stack.Attributes.SetInt("minX", 0);
stack.Attributes.SetInt("maxX", 15);
stack.Attributes.SetInt("minY", 0);
stack.Attributes.SetInt("maxY", 15);
stack.Attributes.SetInt("minZ", 0);
stack.Attributes.SetInt("maxZ", 15);
for (int i = 0; i < 6; i++)
{
byte[] fc = WatchedAttributes.GetBytes("f_cuboids_" + i);
string fcode = WatchedAttributes.GetString("f_codes_" + i, "");
string frune = WatchedAttributes.GetString("f_runes_" + i, "");
if (fc != null) stack.Attributes.SetBytes("f_cuboids_" + i, fc);
stack.Attributes.SetString("f_codes_" + i, fcode);
stack.Attributes.SetString("f_runes_" + i, frune);
byte[] tc = WatchedAttributes.GetBytes("f_top_cuboids_" + i);
string tcode = WatchedAttributes.GetString("f_top_codes_" + i, "");
if (tc != null) stack.Attributes.SetBytes("f_top_cuboids_" + i, tc);
if (!string.IsNullOrEmpty(tcode)) stack.Attributes.SetString("f_top_codes_" + i, tcode);
}
if (carriedStack != null && carriedStack.StackSize > 0)
{
stack.Attributes.SetItemstack("carriedStack", carriedStack.Clone());
}
return stack;
}
        public void RebuildLegacyGolemData()
        {
            if (World == null || World.Side != EnumAppSide.Server) return;
            bool changed = false;

            for (int i = 0; i < 6; i++)
            {
                string fc = WatchedAttributes.GetString("f_codes_" + i, "");
                if (!string.IsNullOrEmpty(fc) && fc.Contains("runeglass"))
                {
                    fc = fc.Replace("runicgems:runeglass-normal", "runicgems:inlaid-runiccrystal")
                           .Replace("runicgems:runeglass-active", "runicgems:inlaid-runiccrystal");
                    WatchedAttributes.SetString("f_codes_" + i, fc);
                    changed = true;
                }

                string tc = WatchedAttributes.GetString("f_top_codes_" + i, "");
                if (!string.IsNullOrEmpty(tc) && tc.Contains("runeglass"))
                {
                    tc = tc.Replace("runicgems:runeglass-normal", "runicgems:inlaid-runiccrystal")
                           .Replace("runicgems:runeglass-active", "runicgems:inlaid-runiccrystal");
                    WatchedAttributes.SetString("f_top_codes_" + i, tc);
                    changed = true;
                }
            }

            byte[] blob = WatchedAttributes.GetBytes("animBlob");
            if (blob != null && blob.Length > 0)
            {
                GolemAnimSet set = GolemAnimCodec.Decompress(blob);
                if (set != null)
                {
                    bool blobChanged = false;
                    if (!string.IsNullOrEmpty(set.BottomCodes) && set.BottomCodes.Contains("runeglass"))
                    {
                        set.BottomCodes = set.BottomCodes.Replace("runicgems:runeglass-normal", "runicgems:inlaid-runiccrystal")
                                                         .Replace("runicgems:runeglass-active", "runicgems:inlaid-runiccrystal");
                        blobChanged = true;
                    }
                    if (!string.IsNullOrEmpty(set.TopCodes) && set.TopCodes.Contains("runeglass"))
                    {
                        set.TopCodes = set.TopCodes.Replace("runicgems:runeglass-normal", "runicgems:inlaid-runiccrystal")
                                                   .Replace("runicgems:runeglass-active", "runicgems:inlaid-runiccrystal");
                        blobChanged = true;
                    }
                    if (blobChanged)
                    {
                        WatchedAttributes.SetBytes("animBlob", GolemAnimCodec.Compress(set));
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                WatchedAttributes.MarkAllDirty();
            }
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            RebuildLegacyGolemData();
        }

        public void UnpackFromItemStack(ItemStack stack, string fallbackOwnerUid)
{
if (stack?.Attributes == null) return;
HeightBlocks = stack.Attributes.GetInt("heightBlocks", 1);
CapacityMl = stack.Attributes.GetInt("capacityMl", 100);
OilMl = stack.Attributes.GetInt("oilMl", 0);
string savedOwner = stack.Attributes.GetString("ownerUid", "");
OwnerUid = !string.IsNullOrEmpty(savedOwner) ? savedOwner : (fallbackOwnerUid ?? "");
IsWaiting = false;
CurrentFrame = OilMl > 0 ? 0 : 4;
for (int i = 0; i < 6; i++)
{
byte[] fc = stack.Attributes.GetBytes("f_cuboids_" + i) ?? (i == 0 ? stack.Attributes.GetBytes("cuboids") : null);
string fcode = stack.Attributes.GetString("f_codes_" + i, i == 0 ? stack.Attributes.GetString("blockCodes", "") : "");
string frune = stack.Attributes.GetString("f_runes_" + i, "");
if (fc != null) WatchedAttributes.SetBytes("f_cuboids_" + i, fc);
WatchedAttributes.SetString("f_codes_" + i, fcode);
WatchedAttributes.SetString("f_runes_" + i, frune);
byte[] tc = stack.Attributes.GetBytes("f_top_cuboids_" + i);
string tcode = stack.Attributes.GetString("f_top_codes_" + i, "");
if (tc != null) WatchedAttributes.SetBytes("f_top_cuboids_" + i, tc);
if (!string.IsNullOrEmpty(tcode)) WatchedAttributes.SetString("f_top_codes_" + i, tcode);
}
ItemStack savedCargo = stack.Attributes.GetItemstack("carriedStack");
if (savedCargo != null && savedCargo.ResolveBlockOrItem(World))
{
carriedStack = savedCargo;
SyncCarriedVisuals();
}
RebuildLegacyGolemData();
ApplyHeightBounds();
WatchedAttributes.MarkAllDirty();
}
public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
{
if (mode != EnumInteractMode.Interact || World.Side != EnumAppSide.Server)
{
base.OnInteract(byEntity, itemslot, hitPosition, mode);
return;
}
IPlayer player = (byEntity as EntityPlayer)?.Player;
ItemStack held = itemslot?.Itemstack;
if (byEntity.Controls.Sneak && held == null)
{
ItemStack packed = PackToItemStack();
if (packed != null)
{
HaltMovement();
if (itemslot != null && itemslot.Empty)
{
itemslot.Itemstack = packed;
itemslot.MarkDirty();
}
else if (player == null || !player.InventoryManager.TryGiveItemstack(packed, true))
{
World.SpawnItemEntity(packed, Pos.XYZ.Add(0, 0.5, 0));
}
World.PlaySoundAt(new AssetLocation("game:sounds/effect/latch"), this, null, 1.1f, 12f, 0.7f);
Die(EnumDespawnReason.PickedUp);
return;
}
}
if (held != null && TryRefillOil(byEntity, itemslot)) return; if (!(this is not EntityChiselGolem) && GetType().Name == "EntityChiselGolem" && HasRuneAnyFrame("Storage") && GolemStorageHelper.TryInteractStorage(this, player as IServerPlayer, itemslot, 1)) return;
if (held == null && HasRuneAnyFrame("Follow"))
{
IsWaiting = !IsWaiting;
ForcedTargetId = 0;
if (IsWaiting) HaltMovement();
World.PlaySoundAt(new AssetLocation("game:sounds/effect/latch"), this, null, IsWaiting ? 0.8f : 1.15f, 12f, 0.5f);
string msg = IsWaiting ? "Голем: Охрана поста (Ждёт)" : "Голем: Следует за вами";
(player as IServerPlayer)?.SendMessage(GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
return;
}
base.OnInteract(byEntity, itemslot, hitPosition, mode);
}
private bool TryRefillOil(EntityAgent byEntity, ItemSlot slot)
{
if (OilMl >= CapacityMl || slot?.Itemstack == null) return false;
var liqCont = slot.Itemstack.Collectible as BlockLiquidContainerBase;
if (liqCont == null) return false;
ItemStack content = liqCont.GetContent(slot.Itemstack);
if (content == null || content.Collectible?.Code?.ToString() != "runicgems:temporaloil") return false;
int needMl = CapacityMl - OilMl;
int needItems = (int)Math.Ceiling(needMl / 10.0);
int takeItems = Math.Min(content.StackSize, needItems);
if (takeItems <= 0) return false;
if (slot.Itemstack.StackSize > 1)
{
ItemStack singleBucket = slot.TakeOut(1);
liqCont.TryTakeContent(singleBucket, takeItems);
slot.MarkDirty();
var player = (byEntity as EntityPlayer)?.Player;
if (player == null || !player.InventoryManager.TryGiveItemstack(singleBucket, true))
World.SpawnItemEntity(singleBucket, byEntity.Pos.XYZ);
}
else
{
liqCont.TryTakeContent(slot.Itemstack, takeItems);
slot.MarkDirty();
}
bool wasOff = OilMl <= 0;
OilMl = Math.Min(CapacityMl, OilMl + takeItems * 10);
if (OilMl > 0 && CurrentFrame == 4) CurrentFrame = 0;
World.PlaySoundAt(new AssetLocation("game:sounds/effect/water-fill"), this, null);
if (wasOff && OilMl > 0)
{
World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-active"), this, null);
}
return true;
}
public override byte[] LightHsv
{
get
{
if (IsPowered && GetActiveRunes().Contains("Light"))
return new byte[] { 32, 6, 14 };
return base.LightHsv;
}
}
public override string GetInfoText()
{
var sb = new StringBuilder();
if (HeightBlocks >= 2) sb.AppendLine("Класс: Большой голем (2 блока)");
sb.AppendLine("Темпоральное масло: " + OilMl + " / " + CapacityMl + " мл");
string runes = GetActiveRunes();
int idleDrain = 1 + (runes.Contains("Light") ? 2 : 0);
int moveDrain = 3 + (runes.Contains("Swift") ? 2 : 0) + (runes.Contains("Light") ? 2 : 0);
if (!IsPowered)
sb.AppendLine("Состояние: Деактивирован (нужна заправка)");
else
sb.AppendLine("Расход: покой " + idleDrain + " мл/сек | шаг " + moveDrain + " мл/сек");
string baseRunes = WatchedAttributes.GetString("f_runes_0", "");
string actRunes = WatchedAttributes.GetString("f_runes_3", "");
if (!string.IsNullOrEmpty(baseRunes)) sb.AppendLine("Руны: " + baseRunes);
if (!string.IsNullOrEmpty(actRunes)) sb.AppendLine("Руны действия (кадр 4): " + actRunes);
if (HasRuneAnyFrame("Mist"))
{
int w = WatchedAttributes.GetInt("mistWater", 0);
sb.AppendLine("Заряд тумана (влаги): " + (w > 0 ? w + " ед." : "0 (подойдите к воде/бочке)"));
}
if (HasRuneAnyFrame("Follow") && IsWaiting)
{
sb.AppendLine("Режим: Охрана поста (Ждёт)");
}
if (!string.IsNullOrEmpty(CarriedInfo))
{
sb.AppendLine("Несёт: " + CarriedInfo);
}
sb.Append("Shift + ПКМ (пустой рукой): Взять в руки");
return sb.ToString();
}
}
}










