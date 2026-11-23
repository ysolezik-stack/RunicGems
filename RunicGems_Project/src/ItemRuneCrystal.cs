using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
namespace RunicGems
{
public class ItemRuneCrystal : Item
{
public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
{
if (!firstEvent || blockSel == null)
{
base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
return;
}
IWorldAccessor world = byEntity.World;
BlockEntityChisel ch = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
if (ch == null || ch.VoxelCuboids == null || ch.BlockIds == null)
{
base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
return;
}
int crystalBlockId = world.GetBlock(new AssetLocation("runicgems", "inlaid-runiccrystal"))?.BlockId ?? -1;
if (crystalBlockId <= 0) return;
bool hasCrystal = false;
for (int i = 0; i != ch.BlockIds.Length; i++)
{
if ((int)ch.BlockIds.GetValue(i) == crystalBlockId)
{
hasCrystal = true;
break;
}
}
if (!hasCrystal) return;
int totalVoxels = 0;
foreach (uint c in ch.VoxelCuboids)
{
int dx = ((int)((c >> 12) & 0xF) - (int)(c & 0xF)) + 1;
int dy = ((int)((c >> 16) & 0xF) - (int)((c >> 4) & 0xF)) + 1;
int dz = ((int)((c >> 20) & 0xF) - (int)((c >> 8) & 0xF)) + 1;
totalVoxels += dx * dy * dz;
}
// Если из инкрустированного кристалла вырезана заготовка инструмента (от 8 до 160 вокселей)
if (totalVoxels >= 8 && totalVoxels <= 160)
{
handling = EnumHandHandling.PreventDefault;
if (world.Side != EnumAppSide.Server) return;
Item activator = world.GetItem(new AssetLocation("runicgems", "runeactivator"));
if (activator == null) return;
ItemStack outStack = new ItemStack(activator, 1);
outStack.Attributes.SetInt("durability", 64);
world.BlockAccessor.SetBlock(0, blockSel.Position);
slot.TakeOut(1);
slot.MarkDirty();
if (!byEntity.TryGiveItemStack(outStack))
{
world.SpawnItemEntity(outStack, blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));
}
world.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-break"), blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5, null, true, 16f);
((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(0, "Вы вырезали прозрачный рунный активатор!", EnumChatType.Notification);
return;
}
base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
}
}
}

