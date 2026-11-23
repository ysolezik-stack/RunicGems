using System;
using System.Collections;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
namespace RunicGems
{
public class RunicGemsModSystem : ModSystem
{
public static IServerNetworkChannel ServerNet;
private ICoreServerAPI sapi;
private LightningLineRenderer clientRenderer;
private readonly Hashtable crushTimers = new Hashtable();
private readonly Hashtable drinkTimers = new Hashtable();
private readonly Hashtable bowlCooldowns = new Hashtable();
public override void Start(ICoreAPI api)
{
base.Start(api);
            api.RegisterItemClass("ItemRuneCrystal", typeof(ItemRuneCrystal));
api.RegisterBlockClass("BlockChiselRuneBowl", typeof(BlockChiselRuneBowl));
api.RegisterBlockEntityClass("ChiselRuneBowl", typeof(BlockEntityChiselRuneBowl));
api.Network.RegisterChannel("runicgems").RegisterMessageType(typeof(byte[]));
}
public override void StartClientSide(ICoreClientAPI api)
{
base.StartClientSide(api); api.Event.RegisterGameTickListener(dt => { foreach (var inv in api.World.Player.InventoryManager.OpenedInventories) { if (inv.InventoryID.StartsWith("golemstorage-")) { string tag = "dlg_open_" + inv.InventoryID; bool already = false; foreach (var g in api.Gui.OpenedGuis) { if (g is GuiDialogGolemStorage && g.IsOpened()) { already = true; break; } } if (!already) { new GuiDialogGolemStorage("Навесное хранилище", inv as InventoryBase, api).TryOpen(); } } } }, 100);
clientRenderer = new LightningLineRenderer(api);
api.Network.GetChannel("runicgems").SetMessageHandler((byte[] data) =>
{
LightningBoltPacket pkt = LightningBoltPacket.FromBytes(data);
clientRenderer?.AddBolt(new Vec3d(pkt.X1, pkt.Y1, pkt.Z1), new Vec3d(pkt.X2, pkt.Y2, pkt.Z2));
});
}
public override void StartServerSide(ICoreServerAPI api)
{
base.StartServerSide(api);
sapi = api;
ServerNet = api.Network.GetChannel("runicgems");
sapi.Event.RegisterGameTickListener(OnServerTick, 100);
}
private float GetTimer(Hashtable map, string uid)
{
object val = map[uid];
return val is float f ? f : 0f;
}
private void OnServerTick(float dt)
{
foreach (IServerPlayer plr in sapi.World.AllOnlinePlayers)
{
if (plr?.Entity == null || plr.ConnectionState != EnumClientState.Playing) continue;
string uid = plr.PlayerUID;
bool rDown = plr.Entity.Controls.RightMouseDown;
bool sneak = plr.Entity.Controls.Sneak;
ItemSlot slot = plr.InventoryManager?.ActiveHotbarSlot;
string code = slot?.Itemstack?.Collectible?.Code?.ToString() ?? "";
float cd = GetTimer(bowlCooldowns, uid);
if (cd > 0f) bowlCooldowns[uid] = cd - dt;
if (plr.CurrentBlockSelection?.Position != null && slot?.Itemstack?.Collectible?.Tool == EnumTool.Chisel)
{
    BlockPos cPos = plr.CurrentBlockSelection.Position;
    if (sapi.World.BlockAccessor.GetBlockEntity(cPos) is BlockEntityChisel cBe && !(cBe is BlockEntityChiselRuneBowl))
    {
        if (CrystalToolKnapping.TryFinishChiseledActivator(sapi, plr, cPos, cBe)) continue;
    }
}
if (rDown && sneak && code == "runicgems:temporalpowder" && plr.CurrentBlockSelection?.Position != null && cd <= 0f)
{
BlockPos bPos = plr.CurrentBlockSelection.Position;
if (sapi.World.BlockAccessor.GetBlockEntity(bPos) is BlockEntityChisel ch && !(ch is BlockEntityChiselRuneBowl))
{
bowlCooldowns[uid] = 1.0f;
if (ChiselBowlGeometry.TryConvertToRuneBowl(sapi.World, bPos, ch, plr))
{
slot.TakeOut(1);
slot.MarkDirty();
sapi.World.PlaySoundAt(new AssetLocation("game:effect/translocate-break"), bPos.X + 0.5, bPos.Y + 0.5, bPos.Z + 0.5, null, true, 16f);
}
continue;
}
}
if (rDown && code == "game:gear-rusty")
{
float t = GetTimer(crushTimers, uid) + dt;
if (t >= 1.2f) { crushTimers[uid] = 0f; CrushGear(plr, slot); }
else
{
crushTimers[uid] = t;
Vec3d handPos = plr.Entity.Pos.XYZ.Add(0, 1.2, 0);
sapi.World.SpawnParticles(2, ColorUtil.ToRgba(220, 45, 225, 195), handPos, handPos, new Vec3f(-0.3f, 0.1f, -0.3f), new Vec3f(0.3f, 0.4f, 0.3f), 0.3f, 0.2f, 0.12f, EnumParticleModel.Quad);
}
}
else crushTimers[uid] = 0f;
if (rDown && slot?.Itemstack != null && slot.Itemstack.Collectible is BlockLiquidContainerBase cont)
{
bool lookingAtContainer = false;
if (plr.CurrentBlockSelection?.Position != null)
{
Block lookBlock = sapi.World.BlockAccessor.GetBlock(plr.CurrentBlockSelection.Position);
if (lookBlock is BlockLiquidContainerBase || lookBlock is BlockChiselRuneBowl) lookingAtContainer = true;
}
ItemStack c = cont.GetContent(slot.Itemstack);
if (!lookingAtContainer && c?.Collectible?.Code?.ToString() == "runicgems:temporaloil" && c.StackSize > 0)
{
float d = GetTimer(drinkTimers, uid) + dt;
if (d >= 1.0f) { drinkTimers[uid] = 0f; DrinkOil(plr, slot, cont, c); }
else drinkTimers[uid] = d;
continue;
}
}
drinkTimers[uid] = 0f;
}
}
private void CrushGear(IServerPlayer plr, ItemSlot slot)
{
slot.TakeOut(1); slot.MarkDirty();
Item pItem = sapi.World.GetItem(new AssetLocation("runicgems:temporalpowder"));
if (pItem != null && !plr.InventoryManager.TryGiveItemstack(new ItemStack(pItem, 2), true))
sapi.World.SpawnItemEntity(new ItemStack(pItem, 2), plr.Entity.Pos.XYZ.Add(0, 1.0, 0));
plr.Entity.WatchedAttributes.SetDouble("temporalStability", Math.Max(0.0, plr.Entity.WatchedAttributes.GetDouble("temporalStability", 1.0) - 0.25));
sapi.World.PlaySoundAt(new AssetLocation("game:effect/stonecrush"), plr.Entity, null, true, 16f);
}
private void DrinkOil(IServerPlayer plr, ItemSlot slot, BlockLiquidContainerBase cont, ItemStack c)
{
int sip = Math.Min(25, c.StackSize);
if (sip <= 0) return;
cont.TryTakeContent(slot.Itemstack, sip); slot.MarkDirty();
plr.Entity.WatchedAttributes.SetDouble("temporalStability", Math.Min(1.0, plr.Entity.WatchedAttributes.GetDouble("temporalStability", 1.0) + (sip / 100f) * 0.05));
sapi.World.PlaySoundAt(new AssetLocation("game:player/drink"), plr.Entity, null, true, 16f);
}
}
}





