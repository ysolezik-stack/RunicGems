using Vintagestory.API.Common;
namespace RunicGems
{
public class RuneChalkSystem : ModSystem
{
public override void Start(ICoreAPI api)
{
api.RegisterItemClass("ItemRuneChalk", typeof(ItemRuneChalk));
api.RegisterBlockClass("BlockRuneMark", typeof(BlockRuneMark));
}
}
}