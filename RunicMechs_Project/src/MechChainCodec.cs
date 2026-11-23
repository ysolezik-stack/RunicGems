using System;
using System.Collections;
using System.IO;

namespace RunicMechs
{
    public static class MechChainCodec
    {
        private const byte MagicV3 = 0xFE;

        public static byte[] Encode(ArrayList chains)
        {
            if (chains == null || chains.Count == 0) return null;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(MagicV3);
                bw.Write((byte)chains.Count);
                for (int i = 0; i < chains.Count; i++)
                {
                    MechLegChain ch = chains[i] as MechLegChain;
                    WriteJoint(bw, ch.RootSocket, true);
                    bw.Write((byte)ch.Segments.Count);
                    for (int s = 0; s < ch.Segments.Count; s++)
                    {
                        MechSegmentNode seg = ch.Segments[s] as MechSegmentNode;
                        bw.Write(seg.Scale);
                        bw.Write(seg.Length);
                        WriteJoint(bw, seg.TopJoint, true);
                        WriteJoint(bw, seg.BottomJoint, true);
                        bw.Write(seg.GolemBlob != null ? seg.GolemBlob.Length : 0);
                        if (seg.GolemBlob != null && seg.GolemBlob.Length > 0) bw.Write(seg.GolemBlob);
                    }
                }
                return ms.ToArray();
            }
        }

        public static ArrayList Decode(byte[] data)
        {
            var list = new ArrayList();
            if (data == null || data.Length == 0) return list;
            try
            {
                using (var ms = new MemoryStream(data))
                using (var br = new BinaryReader(ms))
                {
                    byte first = br.ReadByte();
                    bool hasIsArm = (first == MagicV3);
                    int count = hasIsArm ? br.ReadByte() : first;

                    for (int i = 0; i < count; i++)
                    {
                        JointPoint root = ReadJoint(br, hasIsArm);
                        var ch = new MechLegChain { RootSocket = root, IsArm = root.IsArm };
                        int segCount = br.ReadByte();
                        for (int s = 0; s < segCount; s++)
                        {
                            var seg = new MechSegmentNode
                            {
                                Scale = br.ReadSingle(),
                                Length = br.ReadSingle(),
                                TopJoint = ReadJoint(br, hasIsArm),
                                BottomJoint = ReadJoint(br, hasIsArm)
                            };
                            int bLen = br.ReadInt32();
                            if (bLen > 0) seg.GolemBlob = br.ReadBytes(bLen);
                            ch.Segments.Add(seg);
                        }
                        list.Add(ch);
                    }
                }
            }
            catch
            {
                return new ArrayList();
            }
            return list;
        }

        private static void WriteJoint(BinaryWriter bw, JointPoint j, bool withArm)
        {
            if (j == null) j = new JointPoint();
            bw.Write(j.X); bw.Write(j.Y); bw.Write(j.Z);
            bw.Write(j.Nx); bw.Write(j.Ny); bw.Write(j.Nz);
            if (withArm) bw.Write(j.IsArm);
        }

        private static JointPoint ReadJoint(BinaryReader br, bool withArm)
        {
            return new JointPoint
            {
                X = br.ReadSingle(), Y = br.ReadSingle(), Z = br.ReadSingle(),
                Nx = br.ReadSingle(), Ny = br.ReadSingle(), Nz = br.ReadSingle(),
                IsArm = withArm && br.ReadBoolean()
            };
        }
    }
}
