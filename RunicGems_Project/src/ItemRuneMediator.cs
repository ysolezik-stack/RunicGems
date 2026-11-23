using System.Text;
using Vintagestory.API.Common;
namespace RunicGems
{
public class ItemRuneMediator : Item
{
public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
{
base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
dsc.AppendLine("Рунический медиатор для настройки и передачи энергии Потока.");
}
}
}
