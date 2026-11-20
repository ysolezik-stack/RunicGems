using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
namespace RunicGems
{
public class ItemRuneChalk : Item
{
private SkillItem[] toolModes;
public override void OnLoaded(ICoreAPI api)
{
base.OnLoaded(api);
if (api is ICoreClientAPI capi)
{
toolModes = new SkillItem[]
{
new SkillItem { Code = new AssetLocation("point1"), Name = Lang.Get("runicgems:mark-point1") },
new SkillItem { Code = new AssetLocation("point2"), Name = Lang.Get("runicgems:mark-point2") },
new SkillItem { Code = new AssetLocation("point3"), Name = Lang.Get("runicgems:mark-point3") },
new SkillItem { Code = new AssetLocation("pickup"), Name = Lang.Get("runicgems:mark-pickup") },
new SkillItem { Code = new AssetLocation("drop"), Name = Lang.Get("runicgems:mark-drop") }
};
}
}
public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel) => toolModes;
public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
{
return slot?.Itemstack?.Attributes?.GetInt("toolMode", 0) ?? 0;
}
public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
{
slot?.Itemstack?.Attributes?.SetInt("toolMode", toolMode);
slot?.MarkDirty();
}
public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
{
if (!firstEvent || blockSel == null) return;
handling = EnumHandHandling.PreventDefault;
IWorldAccessor world = byEntity.World;
if (world.Side != EnumAppSide.Server) return;
BlockPos placePos = blockSel.Position.AddCopy(blockSel.Face);
Block existing = world.BlockAccessor.GetBlock(placePos);
if (existing != null && existing.Id != 0 && !existing.IsReplacableBy(world.GetBlock(new AssetLocation("runicgems", "runemark-point1")))) return;
int mode = slot.Itemstack.Attributes.GetInt("toolMode", 0);
string[] variants = { "point1", "point2", "point3", "pickup", "drop" };
string code = variants[GameMath.Clamp(mode, 0, variants.Length - 1)];
Block markBlock = world.GetBlock(new AssetLocation("runicgems", "runemark-" + code));
if (markBlock == null) return;
world.BlockAccessor.SetBlock(markBlock.Id, placePos);
world.PlaySoundAt(new AssetLocation("game:sounds/player/chalkdraw"), placePos.X + 0.5, placePos.Y + 0.1, placePos.Z + 0.5, null);
DamageItem(world, byEntity, slot, 1);
}
public override void OnUnloaded(ICoreAPI api)
{
base.OnUnloaded(api);
if (toolModes != null)
{
for (int i = 0; i < toolModes.Length; i++) toolModes[i]?.Dispose();
}
}
}
}