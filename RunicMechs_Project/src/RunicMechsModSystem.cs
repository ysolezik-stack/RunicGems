using System;
using System.Collections;
using RunicGems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RunicMechs
{
    public class RunicMechsModSystem : ModSystem
    {
        public static bool IsMechLeftMouseHeld;
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;
        private MechLimbIkRenderer ikRenderer;

        public override double ExecuteOrder() => 0.25;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterEntity("EntityRunicMech", typeof(EntityRunicMech));
            api.RegisterItemClass("ItemMechActivator", typeof(ItemMechActivator));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            api.Event.BreakBlock += (IServerPlayer byPlayer, BlockSelection blockSel, ref float dropQuantityMultiplier, ref EnumHandling handling) =>
            {
                if (byPlayer?.Entity != null && byPlayer.Entity.WatchedAttributes.GetLong("mech_riding_id", 0) != 0)
                {
                    handling = EnumHandling.PreventDefault;
                }
            };

            api.ChatCommands.Create("mechpad")
                .WithDescription("Создать тестовый стенд RunicMechs")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(args =>
                {
                    IServerPlayer sp = args.Caller.Player as IServerPlayer;
                    if (sp == null) return TextCommandResult.Error("Только для игрока");
                    MechTestKitBuilder.BuildBlackBronzePlatform3x3(api.World, sp);
                    return TextCommandResult.Success("[RunicMechs] Тестовый стенд и детали созданы!");
                });
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.RegisterEntityRendererClass("RunicMechRenderer", typeof(EntityRunicMechRenderer));
            ikRenderer = new MechLimbIkRenderer(api);
            api.Event.RegisterRenderer(ikRenderer, EnumRenderStage.Opaque, "runicmechs_ik");

            api.Event.MouseDown += (MouseEvent e) =>
            {
                if (e.Button == EnumMouseButton.Left)
                {
                    IsMechLeftMouseHeld = true;
                }
            };

            api.Event.MouseUp += (MouseEvent e) =>
            {
                if (e.Button == EnumMouseButton.Left)
                {
                    IsMechLeftMouseHeld = false;
                }
            };

            api.Input.InWorldAction += (EnumEntityAction action, bool on, ref EnumHandling handled) =>
            {
                EntityPlayer plr = api.World?.Player?.Entity;
                if (plr != null && plr.WatchedAttributes.GetLong("mech_riding_id", 0) != 0)
                {
                    if (action == EnumEntityAction.LeftMouseDown || action == EnumEntityAction.InWorldLeftMouseDown)
                    {
                        plr.Controls.LeftMouseDown = false;
                        handled = EnumHandling.PreventDefault;
                    }
                }
            };
        }

        public static ArrayList GetHostJoints(EntityChiselGolem host)
        {
            if (host == null) return new ArrayList();
            byte[] blob = host.WatchedAttributes.GetBytes("mech_parts_blob");
            if (blob != null && blob.Length > 0) return MultiJointScanner.FindAllJointsFromPartsBlob(blob);
            byte[] anim = host.WatchedAttributes.GetBytes("animBlob");
            if (anim != null && anim.Length > 0) return MultiJointScanner.FindAllJoints(GolemAnimCodec.Decompress(anim));
            return new ArrayList();
        }

        public void OpenServerInventoryOnly(IServerPlayer sp, EntityChiselGolem target)
        {
            if (sp == null || target == null) return;
            ArrayList sockets = GetHostJoints(target);
            if (sockets.Count == 0) return;
            var inv = new MechAssemblyInventory(target, sockets, sapi);
            sp.InventoryManager.OpenInventory(inv);
        }

        public void OpenAssemblyForPlayer(IServerPlayer sp, EntityRunicMech mech) => OpenServerInventoryOnly(sp, mech);

        public override void Dispose()
        {
            ikRenderer?.Dispose();
            base.Dispose();
        }
    }
}
