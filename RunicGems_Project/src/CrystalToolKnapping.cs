using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
namespace RunicGems
{
public static class CrystalToolKnapping
{
public static bool TryFinishChiseledActivator(ICoreServerAPI sapi, IServerPlayer plr, BlockPos pos, BlockEntityChisel ch)
{
if (ch == null || ch.VoxelCuboids == null || ch.BlockIds == null) return false;
int crystalBlockId = sapi.World.GetBlock(new AssetLocation("runicgems", "inlaid-runiccrystal"))?.BlockId ?? -1;
if (crystalBlockId <= 0) return false;
// Проверяем, что блок состоит только из инкрустированного рунного кристалла
if (ch.BlockIds.Length != 1 || (int)ch.BlockIds.GetValue(0) != crystalBlockId) return false;
int totalVoxels = 0;
int minX = 16, maxX = -1;
int minY = 16, maxY = -1;
int minZ = 16, maxZ = -1;
foreach (uint c in ch.VoxelCuboids)
{
int x1 = (int)(c & 0xF);
int y1 = (int)((c >> 4) & 0xF);
int z1 = (int)((c >> 8) & 0xF);
int x2 = (int)((c >> 12) & 0xF);
int y2 = (int)((c >> 16) & 0xF);
int z2 = (int)((c >> 20) & 0xF);
int dx = (x2 - x1) + 1;
int dy = (y2 - y1) + 1;
int dz = (z2 - z1) + 1;
totalVoxels += dx * dy * dz;
if (x1 < minX) minX = x1;
if (x2 > maxX) maxX = x2;
if (y1 < minY) minY = y1;
if (y2 > maxY) maxY = y2;
if (z1 < minZ) minZ = z1;
if (z2 > maxZ) maxZ = z2;
}
// Форма резца/активатора (как при отколке каменного инструмента):
// 18..96 вокселей, плоская или стержневая форма (высота 1..3 вокселя, длина >= 8, ширина <= 6)
if (totalVoxels < 18 || totalVoxels > 96) return false;
int sizeX = (maxX - minX) + 1;
int sizeY = (maxY - minY) + 1;
int sizeZ = (maxZ - minZ) + 1;
int length = Math.Max(sizeX, sizeZ);
int width = Math.Min(sizeX, sizeZ);
if (sizeY > 3 || length < 8 || width > 6) return false;
Item activator = sapi.World.GetItem(new AssetLocation("runicgems", "runeactivator"));
if (activator == null) return false;
ItemStack outStack = new ItemStack(activator, 1);
outStack.Attributes.SetInt("durability", 64);
sapi.World.BlockAccessor.SetBlock(0, pos);
Vec3d dropPos = new Vec3d(pos.X + 0.5, pos.Y + 0.35, pos.Z + 0.5);
if (plr != null && plr.InventoryManager.TryGiveItemstack(outStack, true))
{
// Выдано прямо в руки/инвентарь игрока
}
else
{
sapi.World.SpawnItemEntity(outStack, dropPos);
}
sapi.World.PlaySoundAt(new AssetLocation("game:sounds/block/glassbreak1"), dropPos.X, dropPos.Y, dropPos.Z, null, true, 16f);
sapi.World.SpawnParticles(
16,
ColorUtil.ToRgba(240, 110, 245, 255),
dropPos.AddCopy(-0.25, -0.1, -0.25),
dropPos.AddCopy(0.25, 0.25, 0.25),
new Vec3f(-0.6f, 0.4f, -0.6f),
new Vec3f(0.6f, 1.2f, 0.6f),
0.55f,
0.4f,
0.15f,
EnumParticleModel.Cube
);
plr?.SendMessage(0, "Из рунного кристалла отколот прозрачный рунный активатор!", EnumChatType.Notification);
return true;
}
}
}
