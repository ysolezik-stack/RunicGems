using System;
using System.Collections;
using RunicGems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RunicMechs
{
    public class ItemMechActivator : Item
    {
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel != null && byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityChisel)
            {
                // Полностью блокируем ломание чизл-блока (даже мгновенный удар в креативе!)
                handling = EnumHandHandling.PreventDefault;
                if (byEntity.World.Side == EnumAppSide.Server)
                {
                    IServerPlayer sp = (byEntity as EntityPlayer)?.Player as IServerPlayer;
                    MechRitualBuilder.TryPerformMechRitual(byEntity.World, blockSel.Position, sp, blockSel.Face);
                }
                return;
            }
            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!firstEvent) return;

            EntityChiselGolem target = entitySel?.Entity as EntityChiselGolem;
            if (target == null && blockSel?.HitPosition != null)
            {
                target = byEntity.World.GetNearestEntity(blockSel.HitPosition, 1.8f, 2.5f, e => e is EntityChiselGolem && e.Alive) as EntityChiselGolem;
            }

            if (target != null && target.Alive)
            {
                handling = EnumHandHandling.PreventDefault;

                if (byEntity.World.Side == EnumAppSide.Client)
                {
                    ICoreClientAPI capi = api as ICoreClientAPI;
                    
                    ArrayList sockets = RunicMechsModSystem.GetHostJoints(target);
                    if (capi != null && sockets.Count > 0)
                    {
                        var inv = new MechAssemblyInventory(target, sockets, capi);
                        capi.World.Player.InventoryManager.OpenInventory(inv);
                        new GuiDialogMechAssembly(target, inv, sockets.Count, capi).TryOpen();
                    }
                    else
                    {
                        capi?.ShowChatMessage("[RunicMechs] На этом мехе не найдено рун суставов (из рунной крошки)!");
                    }
                    return;
                }

                if (byEntity.World.Side == EnumAppSide.Server)
                {
                    IServerPlayer sp = (byEntity as EntityPlayer)?.Player as IServerPlayer;
                    RunicMechsModSystem sys = api.ModLoader.GetModSystem<RunicMechsModSystem>();
                    if (sp != null && sys != null)
                    {
                        sys.OpenServerInventoryOnly(sp, target);
                    }
                }
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}
