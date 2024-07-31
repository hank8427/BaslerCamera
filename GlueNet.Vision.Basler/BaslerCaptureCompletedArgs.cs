using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Basler.Pylon;
using GlueNet.Vision.Core;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace GlueNet.Vision.Basler
{
    public class BaslerCaptureCompletedArgs : ICaptureCompletedArgs
    {
        public Bitmap Bitmap { get; set; }
        public object Raw { get; }
        public BaslerCaptureCompletedArgs(Bitmap bitmap)
        {
            SetGrayscalePalette(bitmap);
            Bitmap = bitmap;
        }

        public Mat GetMat()
        {
            var mat = Bitmap.ToMat();

            return mat;
        }

        private static void SetGrayscalePalette(Bitmap bitmap)
        {
            if (bitmap.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                ColorPalette palette = bitmap.Palette;
                for (int i = 0; i < 256; i++)
                {
                    palette.Entries[i] = Color.FromArgb(i, i, i);
                }
                bitmap.Palette = palette;
            }
        }
    }
}
