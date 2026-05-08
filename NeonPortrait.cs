using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class NeonForm : Form
{
    public NeonForm()
    {
        Text = "Neon Portrait - Purple Glow";
        WindowState = FormWindowState.Maximized;
        BackColor = Color.Black;

        var pb = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        Controls.Add(pb);

        string imagePath = "photo_2026-05-08_19-19-40_1778257327886.jpg";

        if (!System.IO.File.Exists(imagePath))
        {
            MessageBox.Show("ضع صورة بجانب البرنامج باسم:\n" + imagePath, "الصورة غير موجودة");
            return;
        }

        Bitmap src = new Bitmap(imagePath);
        pb.Image = CreateNeon(src);
    }

    static Bitmap CreateNeon(Bitmap src)
    {
        int w = src.Width, h = src.Height;

        float[] gray = GetGrayscale(src);
        float[] edges = SobelEdges(gray, w, h);

        float max = 0;
        foreach (var v in edges) if (v > max) max = v;
        if (max > 0) for (int i = 0; i < edges.Length; i++) edges[i] /= max;

        float[] rCh = new float[w * h];
        float[] gCh = new float[w * h];
        float[] bCh = new float[w * h];

        for (int i = 0; i < edges.Length; i++)
        {
            float e = (float)Math.Pow(edges[i], 0.7);
            rCh[i] = 0.55f * e;
            gCh[i] = 0.0f  * e;
            bCh[i] = 1.0f  * e;
        }

        float[] glowR = (float[])rCh.Clone();
        float[] glowG = (float[])gCh.Clone();
        float[] glowB = (float[])bCh.Clone();

        int[] radii    = { 2, 4, 8, 14, 22 };
        float[] strengths = { 0.9f, 0.7f, 0.5f, 0.35f, 0.2f };

        for (int pass = 0; pass < radii.Length; pass++)
        {
            float[] blurR = BoxBlur(glowR, w, h, radii[pass]);
            float[] blurG = BoxBlur(glowG, w, h, radii[pass]);
            float[] blurB = BoxBlur(glowB, w, h, radii[pass]);

            float s = strengths[pass];
            for (int i = 0; i < rCh.Length; i++)
            {
                rCh[i] = Math.Min(1f, rCh[i] + blurR[i] * s);
                gCh[i] = Math.Min(1f, gCh[i] + blurG[i] * s);
                bCh[i] = Math.Min(1f, bCh[i] + blurB[i] * s);
            }
        }

        for (int i = 0; i < edges.Length; i++)
        {
            float e = edges[i];
            rCh[i] = Math.Min(1f, rCh[i] + 0.9f * e);
            gCh[i] = Math.Min(1f, gCh[i] + 0.15f * e);
            bCh[i] = Math.Min(1f, bCh[i] + 1.0f  * e);
        }

        Bitmap result = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        BitmapData bd = result.LockBits(new Rectangle(0, 0, w, h),
                                        ImageLockMode.WriteOnly,
                                        PixelFormat.Format32bppRgb);
        byte[] pixels = new byte[bd.Stride * h];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i  = y * w + x;
            int pi = y * bd.Stride + x * 4;
            pixels[pi + 0] = (byte)(Math.Min(1f, bCh[i]) * 255);
            pixels[pi + 1] = (byte)(Math.Min(1f, gCh[i]) * 255);
            pixels[pi + 2] = (byte)(Math.Min(1f, rCh[i]) * 255);
            pixels[pi + 3] = 255;
        }

        Marshal.Copy(pixels, 0, bd.Scan0, pixels.Length);
        result.UnlockBits(bd);
        return result;
    }

    static float[] GetGrayscale(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        Bitmap conv = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(conv))
            g.DrawImage(bmp, 0, 0, w, h);

        BitmapData bd = conv.LockBits(new Rectangle(0, 0, w, h),
                                      ImageLockMode.ReadOnly,
                                      PixelFormat.Format32bppArgb);
        byte[] data = new byte[bd.Stride * h];
        Marshal.Copy(bd.Scan0, data, 0, data.Length);
        conv.UnlockBits(bd);

        float[] gray = new float[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int pi = y * bd.Stride + x * 4;
            float r = data[pi + 2] / 255f;
            float g = data[pi + 1] / 255f;
            float b = data[pi + 0] / 255f;
            gray[y * w + x] = 0.299f * r + 0.587f * g + 0.114f * b;
        }
        return gray;
    }

    static float[] SobelEdges(float[] gray, int w, int h)
    {
        float[] edges = new float[w * h];
        for (int y = 1; y < h - 1; y++)
        for (int x = 1; x < w - 1; x++)
        {
            float gx = -    gray[(y-1)*w+(x-1)] +     gray[(y-1)*w+(x+1)]
                       - 2f*gray[ y   *w+(x-1)] + 2f*gray[ y   *w+(x+1)]
                       -    gray[(y+1)*w+(x-1)] +     gray[(y+1)*w+(x+1)];

            float gy = -    gray[(y-1)*w+(x-1)] - 2f*gray[(y-1)*w+ x   ] -     gray[(y-1)*w+(x+1)]
                       +    gray[(y+1)*w+(x-1)] + 2f*gray[(y+1)*w+ x   ] +     gray[(y+1)*w+(x+1)];

            edges[y * w + x] = (float)Math.Sqrt(gx*gx + gy*gy);
        }
        return edges;
    }

    static float[] BoxBlur(float[] src, int w, int h, int radius)
    {
        float[] tmp = new float[w * h];
        float[] dst = new float[w * h];
        float inv = 1f / (2 * radius + 1);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float sum = 0;
            for (int dx = -radius; dx <= radius; dx++)
                sum += src[y * w + Math.Max(0, Math.Min(w - 1, x + dx))];
            tmp[y * w + x] = sum * inv;
        }

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float sum = 0;
            for (int dy = -radius; dy <= radius; dy++)
                sum += tmp[Math.Max(0, Math.Min(h - 1, y + dy)) * w + x];
            dst[y * w + x] = sum * inv;
        }

        return dst;
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new NeonForm());
    }
}
