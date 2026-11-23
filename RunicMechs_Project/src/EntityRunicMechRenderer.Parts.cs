using System;
using System.IO;
using RunicGems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public partial class EntityRunicMechRenderer
    {
        private void RenderMountedStorageAndCargo(IShaderProgram prog, IRenderAPI rpi, Vec3d cam, EntityRunicMech mech, float lift, MechSwayState sway, float yawAngle, bool isShadowPass)
        {
            MechRuneBridge.ScanStorageAnchorsFromPartsBlob(mech.WatchedAttributes);
            for (int msIdx = 0; msIdx != 2; msIdx++)
            {
                string stCode = mech.WatchedAttributes.GetString("mounted_storage_code_" + msIdx, "");
                if (string.IsNullOrEmpty(stCode)) continue;
                MultiTextureMeshRef msRef = GetCachedBlockMesh(stCode);
                if (msRef == null) continue;

                bool hasRunePt = mech.WatchedAttributes.HasAttribute("mech_st_x_" + msIdx);
                float rx = mech.WatchedAttributes.GetFloat("mech_st_x_" + msIdx, 0f);
                float ry = mech.WatchedAttributes.GetFloat("mech_st_y_" + msIdx, 0.35f);
                float rz = mech.WatchedAttributes.GetFloat("mech_st_z_" + msIdx, 0f);
                float nx = mech.WatchedAttributes.GetFloat("mech_st_nx_" + msIdx, 0f);
                float ny = mech.WatchedAttributes.GetFloat("mech_st_ny_" + msIdx, 0f);
                float nz = mech.WatchedAttributes.GetFloat("mech_st_nz_" + msIdx, 0f);

                float px = hasRunePt ? (rx + nx * 0.36f) : ((msIdx == 0 ? -0.45f : 0.45f));
                float py = hasRunePt ? (ny > 0.5f ? (ry + 0.01f) : (ry + ny * 0.36f - 0.36f)) : 0.55f;
                float pz = hasRunePt ? (rz + nz * 0.36f - 1.0f) : -0.25f;

                mat.Identity();
                mat.Translate((float)(mech.Pos.X - cam.X), (float)(mech.Pos.Y + lift + sway.BobY - cam.Y), (float)(mech.Pos.Z - cam.Z));
                mat.RotateY(yawAngle);
                mat.RotateX(sway.PitchX);
                mat.RotateZ(sway.RollZ);
                mat.Translate(px, py, pz);
                mat.Scale(0.72f, 0.72f, 0.72f);
                mat.Translate(-0.5f, 0f, -0.5f);
                if (prog is IStandardShaderProgram sp) sp.ModelMatrix = mat.Values; else if (prog != null) { mvp.Set(rpi.CurrentShadowProjectionMatrix).Mul(mat.Values); prog.UniformMatrix("mvpMatrix", mvp.Values); }
                rpi.RenderMultiTextureMesh(msRef, isShadowPass ? "tex2d" : "tex");
            }

            MultiTextureMeshRef cRef = GetCachedBlockMesh(mech.WatchedAttributes.GetString("mech_cargo_code", ""));
            if (cRef != null)
            {
                bool isDbl = mech.WatchedAttributes.GetBool("mech_cargo_double", false);
                double fwdX = Math.Sin(mech.Pos.Yaw), fwdZ = Math.Cos(mech.Pos.Yaw);
                double rightX = Math.Cos(mech.Pos.Yaw), rightZ = -Math.Sin(mech.Pos.Yaw);
                double cargoFwd = mech.WatchedAttributes.GetInt("mech_dimX", 1) * 0.5 + 0.65;
                double cargoY = mech.Pos.Y + lift + sway.BobY + mech.WatchedAttributes.GetInt("mech_dimY", 1) * 0.55 - 0.42;

                int halves = isDbl ? 2 : 1;
                for (int hIdx = 0; hIdx != halves; hIdx++)
                {
                    double sideOff = isDbl ? (hIdx == 0 ? -0.45 : 0.45) : 0.0;
                    mat.Identity();
                    mat.Translate((float)(mech.Pos.X + fwdX * cargoFwd + rightX * sideOff - cam.X), (float)(cargoY - cam.Y), (float)(mech.Pos.Z + fwdZ * cargoFwd + rightZ * sideOff - cam.Z));
                    mat.RotateY(mech.Pos.Yaw);
                    mat.Translate(-0.5f, 0f, -0.5f);
                    if (prog is IStandardShaderProgram sp) sp.ModelMatrix = mat.Values; else if (prog != null) { mvp.Set(rpi.CurrentShadowProjectionMatrix).Mul(mat.Values); prog.UniformMatrix("mvpMatrix", mvp.Values); }
                    rpi.RenderMultiTextureMesh(cRef, isShadowPass ? "tex2d" : "tex");
                }
            }
        }

        private void UpdateSeatedPilotTransform(EntityRunicMech mech, float lift, MechSwayState sway, float yawAngle)
        {
            long pid = mech.WatchedAttributes.GetLong("mech_pilot_id", 0);
            if (pid == 0 && lastSeatedPilotId != 0)
            {
                if (capi.World.GetEntityById(lastSeatedPilotId) is EntityPlayer prevPilot)
                {
                    prevPilot.Controls.FloorSitting = false;
                    prevPilot.StopAnimation("sitflooridle");
                    prevPilot.SetCollisionBox(0.6f, 1.85f);
                    prevPilot.SetSelectionBox(0.6f, 1.85f); prevPilot.SelectionBox.Set(-0.3f, 0f, -0.3f, 0.3f, 1.85f, 0.3f);
                }
                float bodyH = Math.Max(0.85f, mech.WatchedAttributes.GetInt("mech_dimY", 1)); mech.SetSelectionBox(0.95f, bodyH); mech.SelectionBox.Set(-0.48f, lift, -0.48f, 0.48f, lift + bodyH, 0.48f); lastSeatedPilotId = 0;
            }

            if (pid != 0 && capi.World.GetEntityById(pid) is EntityPlayer pilot)
            {
                lastSeatedPilotId = pid;
                float slx = mech.WatchedAttributes.GetFloat("mech_seat_lx", 0f);
                float sly = mech.WatchedAttributes.GetFloat("mech_seat_ly", -1f);
                float slz = mech.WatchedAttributes.GetFloat("mech_seat_lz", 0f);
                int dimY = Math.Max(1, mech.WatchedAttributes.GetInt("mech_dimY", 1));
                float localY = sly >= 0f ? sly : (float)dimY;

                mat.Identity();
                mat.Translate((float)mech.Pos.X, (float)(mech.Pos.Y + lift + sway.BobY), (float)mech.Pos.Z);
                mat.RotateY(yawAngle);
                mat.RotateX(sway.PitchX);
                mat.RotateZ(sway.RollZ);
                mat.Translate(slx, localY, slz);

                float[] v = Mat4f.MulWithVec4(mat.Values, new float[] { 0f, 0f, 0f, 1f });
                double wx = (float)v.GetValue(0), wy = (float)v.GetValue(1), wz = (float)v.GetValue(2);
                pilot.Pos.SetPos(wx, wy, wz);
                pilot.Controls.FloorSitting = true;
                pilot.OnGround = true;
                pilot.CollidedHorizontally = false;
                pilot.CollidedVertically = false;
                pilot.SetSelectionBox(0f, 0f);
                pilot.SelectionBox.Y1 = -100f;
                pilot.SelectionBox.Y2 = -100f;
                mech.SetSelectionBox(0f, 0f);
                mech.SelectionBox.Y1 = -100f;
                mech.SelectionBox.Y2 = -100f;

                if (pilot.AnimManager != null && !pilot.AnimManager.IsAnimationActive("sitflooridle")) pilot.StartAnimation("sitflooridle");
                pilot.Pos.Motion.Set(0, 0, 0);
                pilot.BodyYaw = mech.Pos.Yaw;
                pilot.WalkYaw = mech.Pos.Yaw;
                pilot.Pos.Roll = sway.RollZ;
            }
        }

        private void BuildMultiBlockParts(byte[] blob, bool isPowered)
        {
            using (var ms = new MemoryStream(blob))
            using (var br = new BinaryReader(ms))
            {
                int sizeX = br.ReadByte();
                int sizeY = br.ReadByte();
                int sizeZ = br.ReadByte();
                int count = br.ReadUInt16();

                float halfX = sizeX * 0.5f;
                float halfZ = sizeZ * 0.5f;

                for (int i = 0; i != count; i++)
                {
                    int bx = br.ReadSByte();
                    int by = br.ReadSByte();
                    int bz = br.ReadSByte();
                    string codes = br.ReadString();
                    int len = br.ReadInt32();
                    byte[] rawCubs = len > 0 ? br.ReadBytes(len) : Array.Empty<byte>();
                    if (rawCubs.Length == 0) continue;

                    if (isPowered) codes = codes.Replace("runicgems:inlaid-runiccrystal", "runicgems:inlaid-runiccrystal-active");
                    else codes = codes.Replace("runicgems:inlaid-runiccrystal-active", "runicgems:inlaid-runiccrystal");

                    var attr = new TreeAttribute();
                    attr.SetBytes("cuboids", rawCubs);
                    attr.SetString("blockCodes", codes);
                    attr.SetInt("minX", 0); attr.SetInt("maxX", 15);
                    attr.SetInt("minY", 0); attr.SetInt("maxY", 15);
                    attr.SetInt("minZ", 0); attr.SetInt("maxZ", 15);

                    MeshData partMesh = ItemGolemHead.BuildHeadMesh(capi, attr);
                    if (partMesh == null) continue;

                    MultiTextureMeshRef meshRef = capi.Render.UploadMultiTextureMesh(partMesh);
                    partMeshes.Add(new MechPartMesh { Mesh = meshRef, OffX = bx - halfX, OffY = by, OffZ = bz - halfZ });
                }
            }
        }

        private void ClearMeshes()
        {
            for (int i = 0; i != partMeshes.Count; i++)
            {
                if (partMeshes[i] is MechPartMesh mp) mp.Mesh?.Dispose();
            }
            partMeshes.Clear();
        }

        public override void Dispose()
        {
            ClearMeshes();
            foreach (object val in blockMeshCache.Values)
            {
                if (val is MultiTextureMeshRef mRef) mRef?.Dispose();
            }
            blockMeshCache.Clear();
            
        }
    }
}
