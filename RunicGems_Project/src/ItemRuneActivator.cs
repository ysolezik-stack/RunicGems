using System;
using System.Collections;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
namespace RunicGems
{
public class ItemRuneActivator : Item
{
public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
{
if (!firstEvent) return;
IWorldAccessor world = byEntity.World;
if (entitySel != null && entitySel.Entity is EntityChiselGolem targetGolem)
{
if (!byEntity.Controls.Sneak) return;
handling = EnumHandHandling.PreventDefault;
if (world.Side != EnumAppSide.Server) return;
IServerPlayer sPlr = (byEntity as EntityPlayer)?.Player as IServerPlayer;
UnpackGolemToChiselFrames(world, sPlr, slot, targetGolem);
return;
}
if (blockSel == null) return;
BlockEntityChisel clickedBe = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
if (clickedBe == null || clickedBe is BlockEntityChiselRuneBowl) return;
handling = EnumHandHandling.PreventDefault;
if (world.Side != EnumAppSide.Server) return;
IServerPlayer player = (byEntity as EntityPlayer)?.Player as IServerPlayer;
BlockPos basePos = blockSel.Position.Copy();
BlockEntityChisel bottomBe = clickedBe;
BlockEntityChisel topBe = world.BlockAccessor.GetBlockEntity(basePos.UpCopy()) as BlockEntityChisel;
if (topBe == null || topBe is BlockEntityChiselRuneBowl)
{
BlockPos belowPos = basePos.DownCopy();
if (world.BlockAccessor.GetBlockEntity(belowPos) is BlockEntityChisel belowBe && !(belowBe is BlockEntityChiselRuneBowl))
{
basePos = belowPos;
bottomBe = belowBe;
topBe = clickedBe;
}
else
{
topBe = null;
}
}
bool isTall = topBe != null;
BlockFacing frontFace = blockSel.Face != null && blockSel.Face.IsHorizontal
? blockSel.Face
: BlockFacing.HorizontalFromYaw(byEntity.Pos.Yaw).Opposite;
BlockFacing backDir = frontFace.Opposite;
BlockFacing existingDir = HasFiveFramesBehind(world, basePos, backDir, isTall) ? backDir : FindExistingRowDirection(world, basePos, isTall);
if (existingDir == null)
{
ChiselFrameData check = ChiselGolemGeometry.AnalyzeDoubleFrame(world.Api, bottomBe, topBe, true);
if (!check.IsValid)
{
player?.SendIngameError("runicgems:golem-err", check.ErrorKey);
return;
}
for (int i = 1; i <= 5; i++)
{
BlockPos p = basePos.AddCopy(backDir, i);
Block b = world.BlockAccessor.GetBlock(p);
if (b != null && b.Id != 0 && !b.IsReplacableBy(bottomBe.Block))
{
player?.SendIngameError("runicgems:golem-space", "Нужно 5 свободных блоков позади статуэтки для кадров анимации (включая кадр прыжка)!");
return;
}
if (isTall)
{
BlockPos pTop = p.UpCopy();
Block bTop = world.BlockAccessor.GetBlock(pTop);
if (bTop != null && bTop.Id != 0 && !bTop.IsReplacableBy(topBe.Block))
{
player?.SendIngameError("runicgems:golem-space-top", "Нужно 5 свободных блоков сверху позади для 2-блочного голема!");
return;
}
}
}
for (int i = 1; i <= 5; i++)
{
BlockPos p = basePos.AddCopy(backDir, i);
CloneChiselBlock(world, bottomBe, p);
if (isTall)
{
CloneChiselBlock(world, topBe, p.UpCopy());
}
}
DamageItem(world, byEntity, slot, 1);
world.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-active"), basePos.X + 0.5, basePos.Y + 0.5, basePos.Z + 0.5, null);
string kind = isTall ? "высокого (2 блока)" : "малого (1 блок)";
player?.SendMessage(GlobalConstants.GeneralChatGroup, "Созданы 5 кадров позади (покой, шаг 1, шаг 2, действие, выкл, прыжок) для " + kind + " голема! Отредактируйте позы и снова нажмите активатором.", EnumChatType.Notification);
return;
}
var framesList = new ArrayList();
for (int i = 0; i < 6; i++)
{
BlockPos p = basePos.AddCopy(existingDir, i);
BlockEntityChisel frameBottom = world.BlockAccessor.GetBlockEntity(p) as BlockEntityChisel;
BlockEntityChisel frameTop = isTall ? (world.BlockAccessor.GetBlockEntity(p.UpCopy()) as BlockEntityChisel) : null;
if (frameBottom == null || (isTall && frameTop == null))
{
player?.SendIngameError("runicgems:golem-row", "Не хватает блока кадра №" + (i + 1) + " в ряду!");
return;
}
ChiselFrameData fData = ChiselGolemGeometry.AnalyzeDoubleFrame(world.Api, frameBottom, frameTop, i == 0);
if (!fData.IsValid)
{
player?.SendIngameError("runicgems:golem-err", fData.ErrorKey);
return;
}
framesList.Add(fData);
}
EntityProperties props = world.GetEntityType(new AssetLocation("runicgems", "chiselgolem"));
if (props == null)
{
player?.SendIngameError("runicgems:no-chiselgolem", "Сущность chiselgolem не найдена!");
return;
}
EntityChiselGolem golem = world.ClassRegistry.CreateEntity(props) as EntityChiselGolem;
if (golem == null) return;
ChiselFrameData baseFrame = framesList.ToArray().GetValue(0) as ChiselFrameData;
BlockFacing lookFace = existingDir.Opposite;
float yaw = (float)Math.Atan2(lookFace.Normalf.X, lookFace.Normalf.Z);
golem.Pos.SetPos(basePos.X + 0.5, basePos.Y, basePos.Z + 0.5);
golem.Pos.Yaw = yaw;
golem.LoadFrames(framesList, player?.PlayerUID ?? "");
ITreeAttribute itemAttr = slot.Itemstack.Attributes;
int savedOil = itemAttr.GetInt("savedRechiselOil", 0);
if (savedOil > 0)
{
double distSq = Math.Pow(itemAttr.GetInt("savedRechiselX") - basePos.X, 2) + Math.Pow(itemAttr.GetInt("savedRechiselZ") - basePos.Z, 2);
if (distSq <= 144)
{
golem.OilMl = Math.Min(golem.CapacityMl, savedOil);
}
itemAttr.RemoveAttribute("savedRechiselOil");
slot.MarkDirty();
}
for (int i = 0; i < 6; i++)
{
BlockPos p = basePos.AddCopy(existingDir, i);
world.BlockAccessor.SetBlock(0, p);
if (isTall) world.BlockAccessor.SetBlock(0, p.UpCopy());
}
DamageItem(world, byEntity, slot, isTall ? 3 : 2);
world.SpawnEntity(golem);
world.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-break"), golem, null);
string title = isTall ? "Большой чизл-голем (2 блока) собран!" : "Чизл-голем собран!";
string oilMsg = golem.OilMl > 0 ? (" Восстановлено масла: " + golem.OilMl + " мл.") : " Заправьте его темпоральным маслом.";
player?.SendMessage(GlobalConstants.GeneralChatGroup, title + " Объём бака: " + baseFrame.CapacityMl + " мл." + oilMsg, EnumChatType.Notification);
}
private void UnpackGolemToChiselFrames(IWorldAccessor world, IServerPlayer player, ItemSlot slot, EntityChiselGolem golem)
{
Block chiselBlock = world.GetBlock(new AssetLocation("game:chiseledblock"));
if (chiselBlock == null) return;
bool isTall = golem.HeightBlocks >= 2;
BlockPos basePos = golem.Pos.AsBlockPos;
BlockFacing lookFace = BlockFacing.HorizontalFromYaw(golem.Pos.Yaw);
BlockFacing backDir = lookFace.Opposite;
for (int i = 0; i < 6; i++)
{
BlockPos p = basePos.AddCopy(backDir, i);
Block b = world.BlockAccessor.GetBlock(p);
if (b != null && b.Id != 0 && !b.IsReplacableBy(chiselBlock))
{
player?.SendIngameError("runicgems:rechisel-space", "Освободите 6 блоков позади голема для развёртывания кадров!");
return;
}
if (isTall)
{
BlockPos pTop = p.UpCopy();
Block bTop = world.BlockAccessor.GetBlock(pTop);
if (bTop != null && bTop.Id != 0 && !bTop.IsReplacableBy(chiselBlock))
{
player?.SendIngameError("runicgems:rechisel-space-top", "Освободите верхний ряд из 6 блоков позади голема!");
return;
}
}
}
for (int i = 0; i < 6; i++)
{
BlockPos p = basePos.AddCopy(backDir, i);
RestoreChiselBlockFromBytes(world, chiselBlock, p,
golem.WatchedAttributes.GetBytes("f_cuboids_" + i),
golem.WatchedAttributes.GetString("f_codes_" + i, ""));
if (isTall)
{
RestoreChiselBlockFromBytes(world, chiselBlock, p.UpCopy(),
golem.WatchedAttributes.GetBytes("f_top_cuboids_" + i),
golem.WatchedAttributes.GetString("f_top_codes_" + i, ""));
}
}
if (golem.OilMl > 0)
{
ITreeAttribute itemAttr = slot.Itemstack.Attributes;
itemAttr.SetInt("savedRechiselOil", golem.OilMl);
itemAttr.SetInt("savedRechiselX", basePos.X);
itemAttr.SetInt("savedRechiselZ", basePos.Z);
slot.MarkDirty();
}
int preservedOil = golem.OilMl;
golem.Die(EnumDespawnReason.Removed, null);
world.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-active"), basePos.X + 0.5, basePos.Y + 0.5, basePos.Z + 0.5, null);
player?.SendMessage(GlobalConstants.GeneralChatGroup,
"Голем развёрнут в 6 кадров для перечизлинга! Сохранён запас масла: " + preservedOil + " мл. Отредактируйте блоки и нажмите ПКМ активатором.",
EnumChatType.Notification);
}
private void RestoreChiselBlockFromBytes(IWorldAccessor world, Block chiselBlock, BlockPos pos, byte[] cuboidsBytes, string codesStr)
{
world.BlockAccessor.SetBlock(chiselBlock.Id, pos);
BlockEntityChisel be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityChisel;
if (be == null || cuboidsBytes == null || string.IsNullOrEmpty(codesStr)) return;
string[] codes = codesStr.Split('|');
int[] ids = (int[])Array.CreateInstance(typeof(int), codes.Length);
for (int m = 0; m < codes.Length; m++)
{
string code = (string)codes.GetValue(m);
Block matBlock = !string.IsNullOrEmpty(code) && code != "game:air" ? world.GetBlock(new AssetLocation(code)) : null;
ids.SetValue(matBlock != null ? matBlock.Id : chiselBlock.Id, m);
}
be.BlockIds = ids;
be.VoxelCuboids.Clear();
int count = cuboidsBytes.Length / 4;
for (int k = 0; k < count; k++)
{
uint val = BitConverter.ToUInt32(cuboidsBytes, k * 4);
be.VoxelCuboids.Add(val);
}
be.MarkDirty(true);
world.BlockAccessor.MarkBlockDirty(pos);
}
private void CloneChiselBlock(IWorldAccessor world, BlockEntityChisel srcBe, BlockPos targetPos)
{
world.BlockAccessor.SetBlock(srcBe.Block.Id, targetPos);
BlockEntityChisel copyBe = world.BlockAccessor.GetBlockEntity(targetPos) as BlockEntityChisel;
if (copyBe != null)
{
copyBe.BlockIds = (int[])srcBe.BlockIds.Clone();
copyBe.VoxelCuboids.Clear();
for (int k = 0; k < srcBe.VoxelCuboids.Count; k++)
copyBe.VoxelCuboids.Add(srcBe.VoxelCuboids[k]);
copyBe.MarkDirty(true);
}
}
private bool HasFiveFramesBehind(IWorldAccessor world, BlockPos start, BlockFacing dir, bool isTall)
{
for (int step = 1; step <= 5; step++)
{
BlockPos p = start.AddCopy(dir, step);
if (!(world.BlockAccessor.GetBlockEntity(p) is BlockEntityChisel)) return false;
if (isTall && !(world.BlockAccessor.GetBlockEntity(p.UpCopy()) is BlockEntityChisel)) return false;
}
return true;
}
private BlockFacing FindExistingRowDirection(IWorldAccessor world, BlockPos start, bool isTall)
{
foreach (BlockFacing face in BlockFacing.ALLFACES)
{
if (!face.IsHorizontal) continue;
if (HasFiveFramesBehind(world, start, face, isTall)) return face;
}
return null;
}
}
}
