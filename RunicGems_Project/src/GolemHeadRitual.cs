using Vintagestory.API.Client;
using Vintagestory.API.Common;
namespace RunicGems
{
public class GolemHeadModSystem : ModSystem
{
public override void Start(ICoreAPI api)
{
api.RegisterItemClass("ItemGolemHead", typeof(ItemGolemHead));
api.RegisterItemClass("ItemRuneActivator", typeof(ItemRuneActivator));
api.RegisterEntity("EntityChiselGolem", typeof(EntityChiselGolem));
}
public override void StartClientSide(ICoreClientAPI api)
{
api.RegisterEntityRendererClass("ChiselGolem", typeof(EntityChiselGolemRenderer));
}
}
}