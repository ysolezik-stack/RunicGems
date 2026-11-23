using System;
using RunicGems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RunicMechs
{
    public class GuiDialogMechAssembly : GuiDialog
    {
        private readonly MechAssemblyInventory inv;
        private readonly EntityChiselGolem host;
        private readonly int socketCount;

        public override string ToggleKeyCombinationCode => null;

        public GuiDialogMechAssembly(EntityChiselGolem host, MechAssemblyInventory inv, int socketCount, ICoreClientAPI capi) : base(capi)
        {
            this.host = host;
            this.inv = inv;
            this.socketCount = Math.Max(1, Math.Min(8, socketCount));
            Compose();
        }

        private void Compose()
        {
            int rows = socketCount;
            int cols = 4;
            double slotStep = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGridBase.unscaledSlotPadding;
            double labelW = 95;
            double gridW = cols * slotStep;
            double innerW = Math.Max(360, labelW + gridW + 8);
            double bodyH = 44 + rows * slotStep + 56;

            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, innerW, bodyH).WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-180, 0);

            string mode = host.WatchedAttributes.GetString("mech_gait", "auto");
            string modeLabel = mode == "spider"
                ? "Походка: ПАУК (Spider)"
                : (mode == "biped" ? "Походка: ДВУНОГИЙ (Biped)" : "Походка: АВТО");

            var composer = capi.Gui.CreateCompo("mech-assembly-" + host.EntityId, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Сборка конечностей меха", OnTitleBarClose)
                .BeginChildElements(bgBounds)
                .AddSmallButton(modeLabel, OnToggleGait, ElementBounds.Fixed(0, GuiStyle.TitleBarHeight + 6, innerW, 28));

            int legNum = 0;
            int armNum = 0;
            for (int r = 0; r != rows; r++)
            {
                var sList = RunicMechsModSystem.GetHostJoints(host); bool isArm = (r < sList.Count && (sList[r]?.ToString()?.IndexOf("arm", StringComparison.OrdinalIgnoreCase) >= 0));
                string label = isArm ? ("Рука " + (++armNum) + ":") : ("Нога " + (++legNum) + ":");

                double y = GuiStyle.TitleBarHeight + 42 + r * slotStep;
                composer.AddStaticText(label, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y + 14, labelW - 6, 24));

                int[] slotIds = new int[cols];
                for (int c = 0; c != cols; c++) slotIds.SetValue(r * 4 + c, c);

                ElementBounds rowBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, labelW, y, cols, 1);
                composer.AddItemSlotGrid(inv, SendInvPacket, cols, slotIds, rowBounds, "limbGrid_" + r);
            }

            float reach = host.WatchedAttributes.GetFloat("mech_arm_reach", 0f);
            string reachText = reach > 0.1f
                ? $"Дистанция захвата рук: {reach:F2} бл. (ЛКМ: взять/поставить, зажать ЛКМ: удар)"
                : "Ноги: Бедро->Голень->Стопа | Руки (3 сегм.): Плечо->Локоть->Кисть";

            double footY = GuiStyle.TitleBarHeight + 42 + rows * slotStep + 2;
            composer.AddStaticText(reachText, CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, footY, innerW, 20));
            composer.AddSmallButton("Пересобрать корпус (разобрать в блоки)", OnRebuildClicked, ElementBounds.Fixed(0, footY + 22, innerW, 26));

            SingleComposer = composer.EndChildElements().Compose();
        }

        private bool OnRebuildClicked()
        {
            if (host == null) return true;
            capi?.Network?.SendEntityPacket(host.EntityId, 9918, new byte[] { 1 });
            TryClose();
            return true;
        }

        private bool OnToggleGait()
        {
            if (host == null) return true;
            string cur = host.WatchedAttributes.GetString("mech_gait", "auto");
            string next = cur == "auto" ? "spider" : (cur == "spider" ? "biped" : "auto");
            host.WatchedAttributes.SetString("mech_gait", next);
            capi?.Network?.SendEntityPacket(host.EntityId, 9915, System.Text.Encoding.UTF8.GetBytes(next));
            capi?.Event?.EnqueueMainThreadTask(() =>
            {
                if (IsOpened()) TryClose(); TryOpen();
            }, "mech_gait_ui");
            return true;
        }

        private void SendInvPacket(object p) { capi.Network.SendPacketClient(p); }
        private void OnTitleBarClose() { TryClose(); }

        public override void OnGuiClosed()
        {
            foreach (var g in capi.Gui.OpenedGuis) { if (g is RunicGems.GuiDialogGolemStorage && g.IsOpened()) g.TryClose(); }
            base.OnGuiClosed();
            capi.Network.SendPacketClient(inv.Close(capi.World.Player));
        }
    }
}
