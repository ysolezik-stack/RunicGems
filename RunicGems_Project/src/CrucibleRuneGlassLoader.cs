using Vintagestory.API.Common;

namespace RunicGems
{
    public class CrucibleRuneGlassLoader : ModSystem
    {
        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);
            Item crystal = api.World.GetItem(new AssetLocation("runicgems", "runiccrystal"));
            Block runeGlass = api.World.GetBlock(new AssetLocation("runicgems", "runeglass-normal"));
            if (crystal != null && runeGlass != null)
            {
                crystal.CombustibleProps = new CombustibleProperties
                {
                    MeltingPoint = 900,
                    MeltingDuration = 30f,
                    SmeltedRatio = 2,
                    SmeltingType = EnumSmeltType.Smelt,
                    SmeltedStack = new JsonItemStack
                    {
                        Type = EnumItemClass.Block,
                        Code = runeGlass.Code,
                        StackSize = 1,
                        ResolvedItemstack = new ItemStack(runeGlass, 1)
                    }
                };
            }
        }
    }
}
