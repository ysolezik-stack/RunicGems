using System;
using System.Collections;
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
public class ItemRuneActivator : Item
{
public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
{
if (!firstEvent) return;
IWorldAccessor world = byEntity.World;
bool isSneak = (byEntity.Controls != null && byEntity.Controls.Sneak) || byEntity.ServerControls.Sneak;
// 1. Shift + ПКМ по живому голему — редактирование его анимаций
if (entitySel != null && entitySel.Entity is EntityChiselGolem targetGolem)
{
if (!isSneak) return;
handling = EnumHandHandling.PreventDefault;
if (world.Side == EnumAppSide.Client)
{
ICoreClientAPI capi = world.Api as ICoreClientAPI;
if (capi == null) return;
if (targetGolem.WatchedAttributes.GetBool("isCorrupted", false))
{
capi.ShowChatMessage("Осквернённый голем не поддаётся настройке!");
return;
}
GolemAnimSet liveSet = GolemAnimCodec.LoadFromAttributes(targetGolem.WatchedAttributes);
if (liveSet == null)
{
capi.ShowChatMessage("Не удалось загрузить модель голема.");
return;
}
long entId = targetGolem.EntityId;
var dlg = new GuiDialogGolemAnimator(capi, liveSet, null, entId, () =>
{
GolemHeadModSystem.SendSaveToServer(null, entId, liveSet, false);
});
dlg.TryOpen();
}
return;
}
if (blockSel == null) return;
BlockEntityChisel clickedBe = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
if (clickedBe == null || clickedBe is BlockEntityChiselRuneBowl) return;
handling = EnumHandHandling.PreventDefault;
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
var tree = new TreeAttribute();
bottomBe.ToTreeAttributes(tree);
byte[] savedBlob = tree.GetBytes("runicgems_animBlob");
if (world.Side == EnumAppSide.Client)
{
ICoreClientAPI capi = world.Api as ICoreClientAPI;
if (capi == null) return;
GolemAnimSet animSet = (savedBlob != null && savedBlob.Length != 0)
? GolemAnimCodec.Decompress(savedBlob)
: null;
if (animSet == null || animSet.BaseSlot == null || animSet.BaseSlot.IsEmpty)
{
animSet = GolemAnimCodec.CreateFromChiselBlocks(capi, bottomBe, topBe);
}
BlockPos savePos = basePos.Copy();
// ПРИСЯД + ПКМ -> Отправляем команду мгновенного оживления голема на сервер!
if (isSneak)
{
GolemHeadModSystem.SendSaveToServer(savePos, 0, animSet, true);
return;
}
// ОБЫЧНЫЙ ПКМ -> Открываем 3D-редактор анимаций
var dlg = new GuiDialogGolemAnimator(capi, animSet, savePos, 0, () =>
{
GolemHeadModSystem.SendSaveToServer(savePos, 0, animSet, false);
});
dlg.TryOpen();
}
}
}
}
