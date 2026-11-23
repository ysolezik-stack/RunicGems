using System;
using Vintagestory.API.MathTools;

namespace RunicMechs
{
    public static class FabrikSolver
    {
        public static Vec3d[] SolveChain(Vec3d root, Vec3d target, float[] lengths, Vec3d kneePole, Vec3d anklePole = null)
        {
            int n = lengths.Length + 1;
            Vec3d[] pts = new Vec3d[n];
            pts.SetValue(root.Clone(), 0);

            float totalLen = 0f;
            for (int i = 0; i < lengths.Length; i++) totalLen += (float)lengths.GetValue(i);

            Vec3d toTarget = target.SubCopy(root);
            double dist = toTarget.Length();
            if (dist < 0.0001) toTarget = new Vec3d(0, -1, 0);
            else toTarget.Normalize();

            if (dist >= totalLen * 0.995)
            {
                for (int i = 0; i < lengths.Length; i++)
                {
                    Vec3d prev = (Vec3d)pts.GetValue(i);
                    float len = (float)lengths.GetValue(i);
                    pts.SetValue(prev.AddCopy(toTarget.X * len, toTarget.Y * len, toTarget.Z * len), i + 1);
                }
                return pts;
            }

            // Начальная Z-образная поза суставов перед итерациями FABRIK
            for (int i = 1; i < n; i++)
            {
                float t = (float)i / (n - 1);
                Vec3d p = new Vec3d(
                    root.X + (target.X - root.X) * t,
                    root.Y + (target.Y - root.Y) * t,
                    root.Z + (target.Z - root.Z) * t
                );

                if (i < n - 1)
                {
                    // Нечётный сустав (колено) -> kneePole (назад), чётный сустав (голень/пятка) -> anklePole (вперёд и в сторону)
                    Vec3d bend = (i % 2 == 1 || anklePole == null) ? kneePole : anklePole;
                    if (bend != null)
                    {
                        p.X += bend.X * 0.55;
                        p.Y += bend.Y * 0.35;
                        p.Z += bend.Z * 0.55;
                    }
                }
                pts.SetValue(p, i);
            }

            for (int iter = 0; iter < 12; iter++)
            {
                pts.SetValue(target.Clone(), n - 1);
                for (int i = n - 2; i >= 0; i--)
                {
                    Vec3d pCur = (Vec3d)pts.GetValue(i);
                    Vec3d pNext = (Vec3d)pts.GetValue(i + 1);
                    Vec3d dir = pCur.SubCopy(pNext);
                    if (dir.Length() < 0.0001) dir = new Vec3d(0, 1, 0);
                    else dir.Normalize();
                    float len = (float)lengths.GetValue(i);
                    pts.SetValue(pNext.AddCopy(dir.X * len, dir.Y * len, dir.Z * len), i);
                }

                pts.SetValue(root.Clone(), 0);
                for (int i = 0; i < n - 1; i++)
                {
                    Vec3d pCur = (Vec3d)pts.GetValue(i);
                    Vec3d pNext = (Vec3d)pts.GetValue(i + 1);
                    Vec3d dir = pNext.SubCopy(pCur);
                    if (dir.Length() < 0.0001) dir = new Vec3d(0, -1, 0);
                    else dir.Normalize();
                    float len = (float)lengths.GetValue(i);
                    pts.SetValue(pCur.AddCopy(dir.X * len, dir.Y * len, dir.Z * len), i + 1);
                }
            }

            return pts;
        }
    }
}
