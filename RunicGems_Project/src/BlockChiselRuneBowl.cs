using System;
using System.Collections;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
namespace RunicGems
{
public class BlockChiselRuneBowl : BlockChisel
{
public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
{
if (pos != null && blockAccessor.GetBlockEntity(pos) is BlockEntityChiselRuneBowl be)
{
byte val = be.GetActiveLightLevel();
if (val > 0) return new byte[] { 28, 5, val };
}
return base.GetLightHsv(blockAccessor, pos, stack);
}
public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
{
if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityChiselRuneBowl be)
if (be.TryInteractLiquid(byPlayer)) return true;
return base.OnBlockInteractStart(world, byPlayer, blockSel);
}
}
public partial class BlockEntityChiselRuneBowl : BlockEntityChisel
{
private int oilAmount = 0;
private int MaxOil = 100;
private int syncBlocks, syncRunes, syncAnchor, syncLight, syncSwift, syncBolt, syncSiphon;
private int syncMist, syncSeal, syncHeat, syncFrost, syncStorage;
private float syncRadius = 6f;
private bool syncDayOnly, syncNightOnly, syncRoomMode;
private string syncSymbols = "";
private string syncSourceInfo = "";
private string syncAreaInfo = "";
private byte lastLightLevel = 0;
private int lastRenderedOilLayer = -1;
private ArrayList activeBeams = new ArrayList();
private ArrayList cachedRoomBlocks = new ArrayList();
private ArrayList targetPositions = new ArrayList();
public bool IsActiveForGolems => oilAmount > 0;
public float ActiveEffectRadius => syncRadius;
public override void Initialize(ICoreAPI api)
{
base.Initialize(api);
if (api.Side == EnumAppSide.Server) RegisterGameTickListener(OnServerTick, 1000);
else RegisterGameTickListener(OnClientTick, 350);
}
public void RestoreChiselData(uint[] cuboids, int[] ids, string name, int capItems)
{
VoxelCuboids = cuboids.ToList();
BlockIds = ids;
BlockName = name;
MaxOil = capItems;
MarkDirty(true);
Api.World.BlockAccessor.MarkBlockDirty(Pos);
}
public byte GetActiveLightLevel() => (syncLight > 0 && oilAmount > 0) ? (byte)Math.Min(22, 14 + syncLight * 2) : (byte)0;
private bool IsTimeConditionMet()
{
if (!syncDayOnly && !syncNightOnly) return true;
float hour = Api?.World?.Calendar?.HourOfDay ?? 12f;
bool isDay = hour >= 6f && hour < 18f;
return (syncDayOnly && isDay) || (syncNightOnly && !isDay);
}
public bool TryInteractLiquid(IPlayer plr)
{
ItemSlot handSlot = plr?.InventoryManager?.ActiveHotbarSlot;
ItemStack handStack = handSlot?.Itemstack;
if (handStack?.Collectible is not BlockLiquidContainerBase cont) return false;
ItemStack handContent = cont.GetContent(handStack);
int step = (plr.Entity?.Controls?.Sneak ?? false) ? MaxOil : 100;
if (handContent != null && handContent.StackSize > 0 && handContent.Collectible?.Code?.ToString() == "runicgems:temporaloil")
{
int space = MaxOil - oilAmount;
if (space <= 0) return true;
int move = Math.Min(space, Math.Min(step, handContent.StackSize));
if (Api.Side == EnumAppSide.Server)
{
cont.TryTakeContent(handStack, move); handSlot.MarkDirty();
oilAmount += move;
UpdateLiquidVoxels();
MarkDirty(true);
Api.World.PlaySoundAt(new AssetLocation("game:effect/water-pour"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, plr);
}
return true;
}
else if ((handContent == null || handContent.StackSize <= 0) && oilAmount > 0)
{
int move = Math.Min((int)(cont.CapacityLitres * 100), Math.Min(step, oilAmount));
if (Api.Side == EnumAppSide.Server)
{
Item oilItem = Api.World.GetItem(new AssetLocation("runicgems:temporaloil"));
int put = cont.TryPutLiquid(handStack, new ItemStack(oilItem, move), move / 100f);
if (put > 0)
{
oilAmount -= put;
handSlot.MarkDirty();
UpdateLiquidVoxels();
MarkDirty(true);
Api.World.PlaySoundAt(new AssetLocation("game:effect/water-pour"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, plr);
}
}
return true;
}
return false;
}
}
}
