using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
namespace RunicGems
{
public class GolemHeadModSystem : ModSystem
{
public static IClientNetworkChannel ClientGolemNet;
public static IServerNetworkChannel ServerGolemNet;
private ICoreClientAPI capi;
private ICoreServerAPI sapi;
public override void Start(ICoreAPI api)
{
api.RegisterItemClass("ItemGolemHead", typeof(ItemGolemHead));
api.RegisterItemClass("ItemRuneActivator", typeof(ItemRuneActivator));
api.RegisterEntity("EntityChiselGolem", typeof(EntityChiselGolem));
api.Network.RegisterChannel("runicgems_golem").RegisterMessageType(typeof(byte[]));
}
public override void StartClientSide(ICoreClientAPI api)
{
capi = api;
api.RegisterEntityRendererClass("ChiselGolem", typeof(EntityChiselGolemRenderer));
ClientGolemNet = api.Network.GetChannel("runicgems_golem");
BindInterfaceHandler(typeof(IClientNetworkChannel), ClientGolemNet, this, "OnClientBytes", typeof(byte[]));
}
public override void StartServerSide(ICoreServerAPI api)
{
sapi = api;
ServerGolemNet = api.Network.GetChannel("runicgems_golem");
BindInterfaceHandler(typeof(IServerNetworkChannel), ServerGolemNet, this, "OnServerBytes", typeof(byte[]));
}
private static void BindInterfaceHandler(Type ifaceType, object channel, object target, string methodName, Type msgType)
{
if (channel == null) return;
MethodInfo[] methods = ifaceType.GetMethods();
for (int i = 0; i != methods.Length; i++)
{
MethodInfo m = (MethodInfo)methods.GetValue(i);
if (m.Name == "SetMessageHandler" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
{
MethodInfo closed = m.MakeGenericMethod(new Type[] { msgType });
ParameterInfo pi = (ParameterInfo)closed.GetParameters().GetValue(0);
Delegate del = Delegate.CreateDelegate(pi.ParameterType, target, methodName);
closed.Invoke(channel, new object[] { del });
return;
}
}
}
public static void SendOpenEditorToClient(IServerPlayer player, BlockPos blockPos, long entityId, byte[] animBlob)
{
if (ServerGolemNet == null || player == null) return;
using (var ms = new MemoryStream())
using (var bw = new BinaryWriter(ms, Encoding.UTF8))
{
bw.Write((byte)1);
bw.Write(blockPos != null ? 1 : 0);
if (blockPos != null)
{
bw.Write(blockPos.X);
bw.Write(blockPos.Y);
bw.Write(blockPos.Z);
}
bw.Write(entityId);
bw.Write(animBlob != null ? animBlob.Length : 0);
if (animBlob != null && animBlob.Length != 0) bw.Write(animBlob);
bw.Flush();
ServerGolemNet.SendPacket(ms.ToArray(), player);
}
}
public static void SendSaveToServer(BlockPos blockPos, long entityId, GolemAnimSet set, bool spawnNow)
{
if (ClientGolemNet == null || set == null) return;
byte[] blob = GolemAnimCodec.Compress(set);
using (var ms = new MemoryStream())
using (var bw = new BinaryWriter(ms, Encoding.UTF8))
{
bw.Write(spawnNow ? (byte)3 : (byte)2);
bw.Write(blockPos != null ? 1 : 0);
if (blockPos != null)
{
bw.Write(blockPos.X);
bw.Write(blockPos.Y);
bw.Write(blockPos.Z);
}
bw.Write(entityId);
bw.Write(blob.Length);
bw.Write(blob);
bw.Flush();
ClientGolemNet.SendPacket(ms.ToArray());
}
}
public void OnClientBytes(byte[] data)
{
try
{
if (data == null || data.Length == 0 || capi == null) return;
using (var ms = new MemoryStream(data))
using (var br = new BinaryReader(ms, Encoding.UTF8))
{
byte msgType = br.ReadByte();
if (msgType != 1) return;
bool hasPos = br.ReadInt32() == 1;
BlockPos bPos = null;
if (hasPos)
{
bPos = new BlockPos(br.ReadInt32(), br.ReadInt32(), br.ReadInt32(), 0);
}
long entId = br.ReadInt64();
int blobLen = br.ReadInt32();
byte[] blob = blobLen != 0 ? br.ReadBytes(blobLen) : null;
GolemAnimSet animSet = GolemAnimCodec.Decompress(blob);
if (animSet == null)
{
capi.ShowChatMessage("[RunicGems] Ошибка: animSet == null");
return;
}
GuiDialogGolemAnimator dlg = new GuiDialogGolemAnimator(capi, animSet, bPos, entId, () =>
{
SendSaveToServer(bPos, entId, animSet, false);
});
dlg.TryOpen();
}
}
catch (Exception ex)
{
capi?.ShowChatMessage("[RunicGems GUI Error] " + ex.Message);
}
}
public void OnServerBytes(IServerPlayer sPlr, byte[] data)
{
try
{
if (sPlr == null || sPlr.Entity == null || data == null || data.Length == 0 || sapi == null) return;
using (var ms = new MemoryStream(data))
using (var br = new BinaryReader(ms, Encoding.UTF8))
{
byte msgType = br.ReadByte();
if (msgType != 2 && msgType != 3) return;
bool hasPos = br.ReadInt32() == 1;
BlockPos bPos = null;
if (hasPos)
{
bPos = new BlockPos(br.ReadInt32(), br.ReadInt32(), br.ReadInt32(), 0);
}
long entId = br.ReadInt64();
int blobLen = br.ReadInt32();
if (blobLen == 0 || Math.Max(blobLen, 131072) != 131072) return;
byte[] blob = br.ReadBytes(blobLen);
GolemAnimSet incoming = GolemAnimCodec.Decompress(blob);
if (incoming == null || incoming.BaseSlot == null || incoming.BaseSlot.IsEmpty) return;
if (entId != 0)
{
HandleEditLiveGolem(sPlr, entId, incoming, blob);
}
else if (bPos != null)
{
HandleSaveStatueBlock(sPlr, bPos, incoming, blob, msgType == 3);
}
}
}
catch (Exception ex)
{
sPlr?.SendMessage(GlobalConstants.GeneralChatGroup, "[RunicGems Server Error] " + ex.Message, EnumChatType.Notification);
}
}
private void HandleEditLiveGolem(IServerPlayer sPlr, long entId, GolemAnimSet incoming, byte[] blob)
{
EntityChiselGolem golem = sapi.World.GetEntityById(entId) as EntityChiselGolem;
if (golem == null || !golem.Alive) return;
if (Math.Max(sPlr.Entity.Pos.DistanceTo(golem.Pos), 8.0) != 8.0) return;
string owner = golem.OwnerUid;
if (!string.IsNullOrEmpty(owner) && owner != sPlr.PlayerUID) return;
if (golem.WatchedAttributes.GetBool("isCorrupted", false)) return;
GolemAnimSet oldSet = GolemAnimCodec.LoadFromAttributes(golem.WatchedAttributes);
if (oldSet == null || oldSet.BaseSlot == null) return;
int botMatCnt = (oldSet.BottomCodes ?? "").Split('|').Length;
int topMatCnt = (oldSet.TopCodes ?? "").Split('|').Length;
int[] botBudget = GolemAnimCodec.CountVoxelsPerMaterial(oldSet.BaseSlot.BottomCuboids, botMatCnt);
int[] topBudget = GolemAnimCodec.CountVoxelsPerMaterial(oldSet.BaseSlot.TopCuboids, topMatCnt);
if (!GolemAnimCodec.ValidateAgainstSource(incoming, oldSet.BottomCodes, oldSet.TopCodes, botBudget, topBudget))
{
sPlr.SendIngameError("runicgems:invalid-voxels", "Обнаружено превышение допустимого объёма материалов!");
return;
}
GolemAnimCodec.SaveToAttributes(golem.WatchedAttributes, incoming);
ApplyComputedStatsToGolem(golem, incoming, sapi);
golem.WatchedAttributes.MarkAllDirty();
sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-active"), golem, null);
}
private void HandleSaveStatueBlock(IServerPlayer sPlr, BlockPos basePos, GolemAnimSet incoming, byte[] blob, bool spawnNow)
{
Vec3d targetCenter = new Vec3d(basePos.X + 0.5, basePos.Y + 0.5, basePos.Z + 0.5);
if (Math.Max(sPlr.Entity.Pos.DistanceTo(targetCenter), 8.0) != 8.0) return;
if (!sapi.World.Claims.TryAccess(sPlr, basePos, EnumBlockAccessFlags.BuildOrBreak)) return;
BlockEntityChisel bottomBe = sapi.World.BlockAccessor.GetBlockEntity(basePos) as BlockEntityChisel;
if (bottomBe == null || bottomBe is BlockEntityChiselRuneBowl) return;
BlockEntityChisel topBe = sapi.World.BlockAccessor.GetBlockEntity(basePos.UpCopy()) as BlockEntityChisel;
if (topBe is BlockEntityChiselRuneBowl) topBe = null;
GolemAnimSet sourceCheck = GolemAnimCodec.CreateFromChiselBlocks(sapi, bottomBe, topBe);
int botMatCnt = bottomBe.BlockIds != null ? bottomBe.BlockIds.Length : 1;
int topMatCnt = (topBe != null && topBe.BlockIds != null) ? topBe.BlockIds.Length : 1;
int[] botBudget = GolemAnimCodec.CountVoxelsPerMaterial(sourceCheck.BaseSlot.BottomCuboids, botMatCnt);
int[] topBudget = GolemAnimCodec.CountVoxelsPerMaterial(sourceCheck.BaseSlot.TopCuboids, topMatCnt);
if (!GolemAnimCodec.ValidateAgainstSource(incoming, sourceCheck.BottomCodes, sourceCheck.TopCodes, botBudget, topBudget))
{
sPlr.SendIngameError("runicgems:invalid-voxels", "Обнаружено превышение допустимого объёма материалов!");
return;
}
if (!spawnNow)
{
var tree = new TreeAttribute();
bottomBe.ToTreeAttributes(tree);
tree.SetBytes("runicgems_animBlob", blob);
tree.SetFloat("runicgems_scale", incoming.Scale);
tree.SetString("runicgems_golemName", incoming.GolemName ?? "");
bottomBe.FromTreeAttributes(tree, sapi.World);
bottomBe.MarkDirty(true);
sapi.World.BlockAccessor.MarkBlockDirty(basePos);
sPlr.SendMessage(GlobalConstants.GeneralChatGroup, "Анимации и параметры голема сохранены! Нажмите Присядь (Shift) + ПКМ активатором для оживления.", EnumChatType.Notification);
return;
}
bool isTall = topBe != null;
ChiselFrameData check = ChiselGolemGeometry.AnalyzeDoubleFrame(sapi, bottomBe, topBe, false);
if (!check.IsValid)
{
sPlr.SendIngameError("runicgems:golem-err", check.ErrorKey);
return;
}
EntityProperties props = sapi.World.GetEntityType(new AssetLocation("runicgems", "chiselgolem"));
if (props == null)
{
sPlr.SendIngameError("runicgems:no-chiselgolem", "Сущность chiselgolem не найдена!");
return;
}
EntityChiselGolem golem = sapi.World.ClassRegistry.CreateEntity(props) as EntityChiselGolem;
if (golem == null) return;
BlockFacing lookFace = BlockFacing.HorizontalFromYaw(sPlr.Entity.Pos.Yaw).Opposite;
float yaw = (float)Math.Atan2(lookFace.Normalf.X, lookFace.Normalf.Z);
golem.Pos.SetPos(basePos.X + 0.5, basePos.Y, basePos.Z + 0.5);
golem.Pos.Yaw = yaw;
golem.Pos.SetFrom(golem.Pos);
sapi.World.BlockAccessor.SetBlock(0, basePos);
if (isTall) sapi.World.BlockAccessor.SetBlock(0, basePos.UpCopy());
sapi.World.SpawnEntity(golem);
var legacyList = new ArrayList();
legacyList.Add(check);
golem.LoadFrames(legacyList, sPlr.PlayerUID ?? "");
GolemAnimCodec.SaveToAttributes(golem.WatchedAttributes, incoming);
ApplyComputedStatsToGolem(golem, incoming, sapi);
golem.WatchedAttributes.MarkAllDirty();
ItemSlot activeSlot = sPlr.InventoryManager?.ActiveHotbarSlot;
if (activeSlot != null && activeSlot.Itemstack != null && activeSlot.Itemstack.Item is ItemRuneActivator actItem)
{
actItem.DamageItem(sapi.World, sPlr.Entity, activeSlot, isTall ? 3 : 2);
}
sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-break"), golem, null, 1.1f, 16f, 0.8f);
GolemComputedStats stats = GolemAnimCodec.ComputeStats(sapi, incoming);
string namePart = !string.IsNullOrEmpty(incoming.GolemName) ? ("«" + incoming.GolemName + "» ") : "";
string title = isTall ? ("Большой голем " + namePart + "оживлён!") : ("Рунический голем " + namePart + "оживлён!");
sPlr.SendMessage(GlobalConstants.GeneralChatGroup,
title + " ХП: " + stats.MaxHealth + " | Броня: " + (int)Math.Round(stats.DamageResist * 100f) + "% | Батарея: " + stats.BatteryCapacity + " Потока.",
EnumChatType.Notification);
}
public static void ApplyComputedStatsToGolem(EntityChiselGolem golem, GolemAnimSet set, ICoreAPI apiOverride = null)
{
if (golem == null || set == null) return;
ICoreAPI useApi = apiOverride ?? golem.Api;
if (useApi == null) return;
GolemComputedStats stats = GolemAnimCodec.ComputeStats(useApi, set);
golem.CapacityMl = stats.BatteryCapacity;
if (Math.Max(golem.OilMl, golem.CapacityMl) != golem.CapacityMl)
{
golem.OilMl = golem.CapacityMl;
}
golem.WatchedAttributes.SetFloat("golemScale", set.Scale);
golem.WatchedAttributes.SetFloat("maxGolemHp", stats.MaxHealth);
golem.WatchedAttributes.SetFloat("damageResist", stats.DamageResist);
golem.WatchedAttributes.SetString("dominantBlock", stats.DominantBlockCode ?? "");
golem.WatchedAttributes.SetInt("totalVoxels", stats.TotalVoxels);
// Отображение имени голема над головой как у игрока через EntityBehaviorNameTag
string cleanName = (set.GolemName ?? "").Trim();
golem.WatchedAttributes.SetString("golemName", cleanName);
ITreeAttribute nameTagTree = golem.WatchedAttributes.GetOrAddTreeAttribute("nametag");
nameTagTree.SetString("name", cleanName);
nameTagTree.SetBool("showtagonlywhentargeted", false);
nameTagTree.SetInt("renderRange", 32);
golem.WatchedAttributes.MarkPathDirty("nametag");
float sc = GameMath.Clamp(set.Scale, 0.5f, 1.25f);
if (set.HeightBlocks == 2)
{
golem.SetCollisionBox(0.85f * sc, 1.85f * sc);
golem.SetSelectionBox(0.85f * sc, 1.85f * sc);
}
else
{
golem.SetCollisionBox(0.75f * sc, 0.95f * sc);
golem.SetSelectionBox(0.75f * sc, 0.95f * sc);
}
}
}
}


