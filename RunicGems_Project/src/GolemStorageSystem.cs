using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace RunicGems
{
    public class GolemMountedStorageInv : InventoryGeneric
    {
        private readonly EntityChiselGolem owner;
        private readonly string attrKey;
        private bool loading;

        public GolemMountedStorageInv(EntityChiselGolem owner, int slotCount, int slotIdx, ICoreAPI api)
            : base(slotCount, "golemstorage-" + owner.EntityId + "-" + slotIdx, api)
        {
            this.owner = owner;
            this.attrKey = "mounted_storage_inv_" + slotIdx;
            LoadFromEntity();
        }

        public void LoadFromEntity()
        {
            loading = true;
            try
            {
                for (int i = 0; i != Count; i++) this[i].Itemstack = null;
                byte[] data = owner.WatchedAttributes.GetBytes(attrKey);
                if (data == null || data.Length == 0) return;
                var tree = new TreeAttribute();
                using (var ms = new MemoryStream(data))
                using (var br = new BinaryReader(ms)) tree.FromBytes(br);
                FromTreeAttributes(tree);
            }
            catch { }
            finally { loading = false; }
        }

        public override void OnItemSlotModified(ItemSlot slot)
        {
            base.OnItemSlotModified(slot);
            if (loading || Api.Side != EnumAppSide.Server) return;
            var tree = new TreeAttribute();
            ToTreeAttributes(tree);
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                tree.ToBytes(bw);
                owner.WatchedAttributes.SetBytes(attrKey, ms.ToArray());
                owner.WatchedAttributes.MarkPathDirty(attrKey);
            }
        }
    }

    public class GuiDialogGolemStorage : GuiDialogGeneric
    {
        private readonly InventoryBase inv;

        public GuiDialogGolemStorage(string title, InventoryBase inv, ICoreClientAPI capi) : base(title, capi)
        {
            this.inv = inv;
            Compose();
        }

        private void Compose()
        {
            int cols = 4;
            int rows = Math.Max(1, (int)Math.Ceiling(inv.Count / (double)cols));
            ElementBounds slotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 26, cols, rows);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(slotBounds);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-380, 0);

            SingleComposer = capi.Gui.CreateCompo("golemstorage-dlg-" + inv.InventoryID, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, () => TryClose())
                .BeginChildElements(bgBounds)
                    .AddItemSlotGrid(inv, (object pkt) => capi.Network.SendPacketClient(pkt), cols, slotBounds, "grid")
                .EndChildElements()
                .Compose();
        }

        public override bool PrefersUngrabbedMouse => false;

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            capi.Network.SendPacketClient(inv.Close(capi.World.Player));
        }
    }

    public static partial class GolemStorageHelper
    {
        public static bool IsAttachableStorage(ItemStack stack)
        {
            string path = stack?.Collectible?.Code?.Path?.ToLowerInvariant() ?? "";
            if (string.IsNullOrEmpty(path)) return false;
            if (path.Contains("crate") || path.Contains("double") || path.Contains("barrel") || path.Contains("crock") || path.Contains("jug")) return false;
            return path.Contains("chest") || path.Contains("trunk") || path.Contains("storagevessel") || path.Contains("reedchest") || path.Contains("basket");
        }

        public static GolemMountedStorageInv CreateStorageInventory(EntityChiselGolem owner, ICoreAPI api, int slotIdx = 0)
        {
            if (owner == null) return null;
            string code = owner.WatchedAttributes.GetString("mounted_storage_code_" + slotIdx, "");
            if (string.IsNullOrEmpty(code)) return null;
            return new GolemMountedStorageInv(owner, 12, slotIdx, api);
        }

        public static bool TryInteractStorage(EntityChiselGolem golem, IServerPlayer sp, ItemSlot slot, int maxContainers, int targetSlot = -1)
        {
            if (golem == null || sp == null || maxContainers <= 0) return false;
            ScanGolemStorageAnchor(golem);

            ItemStack held = slot?.Itemstack;
            int mountedCount = golem.WatchedAttributes.GetInt("mounted_storage_count", 0);

            if (held != null && IsAttachableStorage(held))
            {
                int freeSlot = -1;
                for (int i = 0; i != maxContainers; i++)
                {
                    if (string.IsNullOrEmpty(golem.WatchedAttributes.GetString("mounted_storage_code_" + i, ""))) { freeSlot = i; break; }
                }
                if (freeSlot == -1)
                {
                    sp.SendMessage(0, $"[Руна Хранения] Все слоты хранения заняты ({maxContainers}/{maxContainers}).", EnumChatType.Notification);
                    return true;
                }

                string code = held.Collectible.Code.ToString();
                string subType = held.Attributes?.GetString("type", "") ?? "";
                golem.WatchedAttributes.SetString("mounted_storage_code_" + freeSlot, code);
                golem.WatchedAttributes.SetString("mounted_storage_type_" + freeSlot, subType);
                golem.WatchedAttributes.SetInt("mounted_storage_count", mountedCount + 1);
                golem.WatchedAttributes.MarkAllDirty();

                slot.TakeOut(1);
                slot.MarkDirty();
                golem.World.PlaySoundAt(new AssetLocation("game:sounds/block/planks"), golem, null, 1.0f, 14f, 0.8f);
                sp.SendMessage(0, $"[Руна Хранения] Контейнер закреплён в слот {freeSlot + 1}/{maxContainers}.", EnumChatType.Notification);
                return true;
            }

            if ((held == null || slot == null || slot.Empty) && mountedCount > 0)
            {
                int activeSlot = targetSlot >= 0 ? targetSlot : 0;
                if (string.IsNullOrEmpty(golem.WatchedAttributes.GetString("mounted_storage_code_" + activeSlot, "")))
                {
                    for (int i = 0; i != maxContainers; i++)
                    {
                        if (!string.IsNullOrEmpty(golem.WatchedAttributes.GetString("mounted_storage_code_" + i, ""))) { activeSlot = i; break; }
                    }
                }

                var inv = CreateStorageInventory(golem, golem.Api, activeSlot);
                if (inv == null) return false;

                if (sp.Entity?.Controls?.Sneak == true)
                {
                    bool isEmpty = true;
                    for (int i = 0; i != inv.Count; i++) if (!inv[i].Empty) { isEmpty = false; break; }
                    if (!isEmpty)
                    {
                        sp.SendMessage(0, "[Руна Хранения] Сначала освободите контейнер от предметов!", EnumChatType.Notification);
                        return true;
                    }

                    string code = golem.WatchedAttributes.GetString("mounted_storage_code_" + activeSlot, "");
                    string subType = golem.WatchedAttributes.GetString("mounted_storage_type_" + activeSlot, "");
                    Block blk = golem.World.GetBlock(new AssetLocation(code));
                    if (blk != null)
                    {
                        ItemStack ret = new ItemStack(blk);
                        if (!string.IsNullOrEmpty(subType)) ret.Attributes.SetString("type", subType);
                        if (!sp.InventoryManager.TryGiveItemstack(ret)) golem.World.SpawnItemEntity(ret, golem.Pos.XYZ.Add(0, 0.8, 0));
                    }

                    golem.WatchedAttributes.RemoveAttribute("mounted_storage_code_" + activeSlot);
                    golem.WatchedAttributes.RemoveAttribute("mounted_storage_type_" + activeSlot);
                    golem.WatchedAttributes.RemoveAttribute("mounted_storage_inv_" + activeSlot);
                    golem.WatchedAttributes.SetInt("mounted_storage_count", Math.Max(0, mountedCount - 1));
                    golem.WatchedAttributes.MarkAllDirty();
                    golem.World.PlaySoundAt(new AssetLocation("game:sounds/block/planks"), golem, null, 1.0f, 14f, 0.8f);
                    sp.SendMessage(0, "[Руна Хранения] Контейнер снят.", EnumChatType.Notification);
                    return true;
                }

                sp.InventoryManager.OpenInventory(inv);
                golem.World.PlaySoundAt(new AssetLocation("game:sounds/block/chestopen"), golem, null, 1.0f, 12f, 0.7f);
                return true;
            }

            return false;
        }
    }
}
