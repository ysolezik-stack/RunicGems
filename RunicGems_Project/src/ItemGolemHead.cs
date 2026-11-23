using System;
using System.Collections;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
namespace RunicGems
{
public class ItemGolemHead : Item
{
private readonly Hashtable meshCache = new Hashtable();
public override string GetHeldItemName(ItemStack itemStack)
{
if (itemStack?.Attributes != null && (itemStack.Attributes.GetBool("isPackedGolem", false) || itemStack.Attributes.HasAttribute("f_cuboids_0")))
{
int h = itemStack.Attributes.GetInt("heightBlocks", 1);
return h >= 2 ? "Свёрнутый большой рунический голем" : "Свёрнутый рунический голем";
}
return base.GetHeldItemName(itemStack);
}
public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
{
byte[] raw = itemstack?.Attributes?.GetBytes("cuboids") ?? itemstack?.Attributes?.GetBytes("f_cuboids_0");
string codes = itemstack?.Attributes?.GetString("blockCodes", "");
if (string.IsNullOrEmpty(codes)) codes = itemstack?.Attributes?.GetString("f_codes_0", "");
if (raw != null && raw.Length > 0 && !string.IsNullOrEmpty(codes))
{
byte[] topRaw = itemstack.Attributes.GetBytes("f_top_cuboids_0");
int key = raw.Length ^ codes.GetHashCode() ^ itemstack.Attributes.GetInt("oilMl", 0) ^ (topRaw != null ? topRaw.Length * 31 : 0);
MultiTextureMeshRef meshRef = meshCache[key] as MultiTextureMeshRef;
if (meshRef == null)
{
var tmp = new TreeAttribute();
tmp.SetBytes("cuboids", raw);
tmp.SetString("blockCodes", codes);
if (topRaw != null && topRaw.Length > 0)
{
tmp.SetBytes("topCuboids", topRaw);
tmp.SetString("topBlockCodes", itemstack.Attributes.GetString("f_top_codes_0", ""));
}
tmp.SetInt("minX", itemstack.Attributes.GetInt("minX", 0));
tmp.SetInt("maxX", itemstack.Attributes.GetInt("maxX", 15));
tmp.SetInt("minY", itemstack.Attributes.GetInt("minY", 0));
tmp.SetInt("maxY", itemstack.Attributes.GetInt("maxY", 15));
tmp.SetInt("minZ", itemstack.Attributes.GetInt("minZ", 0));
tmp.SetInt("maxZ", itemstack.Attributes.GetInt("maxZ", 15));
MeshData mesh = BuildHeadMesh(capi, tmp);
if (mesh != null)
{
meshRef = capi.Render.UploadMultiTextureMesh(mesh);
meshCache[key] = meshRef;
}
}
if (meshRef != null)
{
renderinfo.ModelRef = meshRef;
}
}
base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
}
public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
{ if (slot?.Itemstack?.Attributes?.HasAttribute("mech_parts_blob") == true || (blockSel == null && entitySel?.Entity?.WatchedAttributes?.HasAttribute("mech_parts_blob") == true)) { handling = EnumHandHandling.PreventDefault; if ((byEntity as EntityPlayer)?.Player is Vintagestory.API.Server.IServerPlayer spMsg) spMsg.SendMessage(0, "[RunicMechs] Многоблочный мех редактируется через Пересобрать корпус в окне конечностей!", EnumChatType.Notification); return; }
if (!firstEvent) return;
ITreeAttribute attr = slot?.Itemstack?.Attributes;
bool isPacked = attr != null && (attr.GetBool("isPackedGolem", false) || attr.HasAttribute("f_cuboids_0"));
if (blockSel != null && isPacked)
{
handling = EnumHandHandling.PreventDefault;
if (byEntity.World.Side != EnumAppSide.Server) return;
EntityProperties type = byEntity.World.GetEntityType(new AssetLocation("runicgems", "chiselgolem"));
if (type == null) return;
EntityChiselGolem golem = byEntity.World.ClassRegistry.CreateEntity(type) as EntityChiselGolem;
if (golem == null) return;
BlockPos placePos = blockSel.Position.AddCopy(blockSel.Face);
golem.Pos.SetPos(placePos.X + 0.5, placePos.Y, placePos.Z + 0.5);
golem.Pos.Yaw = byEntity.Pos.Yaw + GameMath.PI;
string ownerUid = (byEntity as EntityPlayer)?.PlayerUID ?? "";
golem.UnpackFromItemStack(slot.Itemstack, ownerUid);
byEntity.World.SpawnEntity(golem);
slot.TakeOut(1);
slot.MarkDirty();
byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/latch"), golem, null, 0.9f, 12f, 0.7f);
return;
}
base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
}
public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
{
base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
ITreeAttribute attr = inSlot?.Itemstack?.Attributes;
if (attr == null) return;
if (attr.GetBool("isPackedGolem", false) || attr.HasAttribute("f_cuboids_0"))
{
int h = attr.GetInt("heightBlocks", 1);
if (h >= 2) dsc.AppendLine("Тип: Большой голем (2 блока в высоту)");
int oil = attr.GetInt("oilMl", 0);
int cap = attr.GetInt("capacityMl", 100);
dsc.AppendLine("Темпоральное масло: " + oil + " / " + cap + " мл");
string runes = attr.GetString("f_runes_0", "");
if (!string.IsNullOrEmpty(runes)) dsc.AppendLine("Руны: " + runes);
string tab = attr.GetString("tabletMode", "");
if (!string.IsNullOrEmpty(tab)) dsc.AppendLine("Пластина: " + tab);
dsc.AppendLine("ПКМ по земле: Развернуть голема");
}
}
public override void OnUnloaded(ICoreAPI api)
{
base.OnUnloaded(api);
foreach (DictionaryEntry entry in meshCache)
{
(entry.Value as MultiTextureMeshRef)?.Dispose();
}
meshCache.Clear();
}
private static void AppendCuboidLayer(ICoreClientAPI capi, byte[] raw, string codesStr, float yShift, ref MeshData combined)
{
if (raw == null || raw.Length == 0 || string.IsNullOrEmpty(codesStr)) return;
int count = raw.Length / 4;
var cuboidsArr = new uint[count];
Buffer.BlockCopy(raw, 0, cuboidsArr, 0, raw.Length);
string[] codes = codesStr.Split('|');
var matMeshes = new MeshData[codes.Length];
for (int i = 0; i < codes.Length; i++)
{
Block b = capi.World.GetBlock(new AssetLocation(codes[i]));
if (b != null && b.Id != 0)
{
    string lowCode = codes[i].ToLowerInvariant();
    if (lowCode.Contains("inlaid"))
    {
        Block baseCube = capi.World.GetBlock(new AssetLocation(lowCode.EndsWith("-active") ? "runicgems:runeglass-active" : "runicgems:runeglass-normal"))
                         ?? capi.World.GetBlock(new AssetLocation("game:stone-granite"));
        capi.Tesselator.TesselateBlock(baseCube, out MeshData bm);

        TextureAtlasPosition tp = null;
        if (b.Textures != null)
        {
            foreach (var kv in b.Textures)
            {
                if (kv.Value?.Baked != null && kv.Value.Baked.TextureSubId > 0)
                {
                    tp = capi.BlockTextureAtlas.Positions[kv.Value.Baked.TextureSubId];
                    break;
                }
            }
        }
        if (tp == null) tp = capi.BlockTextureAtlas.GetPosition(b, "all") ?? capi.BlockTextureAtlas.GetPosition(b, "up");
        if (bm != null && tp != null) bm.SetTexPos(tp);
        matMeshes[i] = bm;
    }
    else
    {
        capi.Tesselator.TesselateBlock(b, out MeshData bm);
        matMeshes[i] = bm;
    }
}
}
for (int i = 0; i < cuboidsArr.Length; i++)
{
uint c = cuboidsArr[i];
int mat = (int)((c >> 24) & 0xFF);
if (mat < 0 || mat >= matMeshes.Length || matMeshes[mat] == null) continue;
float x1 = (c & 0xF) / 16f;
float y1 = (((c >> 4) & 0xF) / 16f) + yShift;
float z1 = ((c >> 8) & 0xF) / 16f;
float x2 = (((c >> 12) & 0xF) + 1) / 16f;
float y2 = ((((c >> 16) & 0xF) + 1) / 16f) + yShift;
float z2 = (((c >> 20) & 0xF) + 1) / 16f;
MeshData part = matMeshes[mat].Clone();
part.Scale(new Vec3f(0, 0, 0), x2 - x1, y2 - y1, z2 - z1);
part.Translate(x1, y1, z1);
if (combined == null) combined = part;
else combined.AddMeshData(part);
}
}
public static MeshData BuildHeadMesh(ICoreClientAPI capi, ITreeAttribute attr)
{
if (attr == null || !attr.HasAttribute("cuboids")) return null;
MeshData combined = null;
AppendCuboidLayer(capi, attr.GetBytes("cuboids"), attr.GetString("blockCodes", ""), 0f, ref combined);
if (attr.HasAttribute("topCuboids"))
{
AppendCuboidLayer(capi, attr.GetBytes("topCuboids"), attr.GetString("topBlockCodes", ""), 1.0f, ref combined);
}
if (combined != null)
{
float cx = ((attr.GetInt("minX", 0) + attr.GetInt("maxX", 15) + 1) * 0.5f) / 16f;
float cy = attr.GetInt("minY", 0) / 16f;
float cz = ((attr.GetInt("minZ", 0) + attr.GetInt("maxZ", 15) + 1) * 0.5f) / 16f;
combined.Translate(0.5f - cx, -cy, 0.5f - cz);
}
if (combined != null) GuiDialogGolemAnimator.ApplyChiselWorldUV(combined);
return combined;
}
}
}










