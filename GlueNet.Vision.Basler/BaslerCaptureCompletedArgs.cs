using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Basler.Pylon;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using GlueNet.Vision.Core;

namespace GlueNet.Vision.Basler
{
    public class BaslerCaptureCompletedArgs : ICaptureCompletedArgs
    {
        public Bitmap Bitmap { get; set; }
        public object Raw { get; }
        public BaslerCaptureCompletedArgs(Bitmap bitmap)
        {
            Bitmap = bitmap;
        }

        public Mat GetMat()
        {
            var bitmap = (Bitmap)Bitmap.Clone();
            var image = new Image<Bgr, byte>(bitmap);
            var mat = image.Mat;
            var width = Bitmap.Width;
            var height = Bitmap.Height;
            var isGray = false;
            int channels = isGray ? 1 : 3;
            int stride = isGray ? width : width * 3;
            //return new Mat(height, width, DepthType.Cv8U, channels, ptrImageData, stride);
            return mat;

            //var rect = new Rectangle(0, 0, Bitmap.Width, Bitmap.Height);

            //var bmpData = Bitmap.LockBits(rect, ImageLockMode.ReadWrite, Bitmap.PixelFormat);

            //IntPtr data = bmpData.Scan0;

            //int step = bmpData.Stride;

            //Mat mat = new Mat(Bitmap.Height, Bitmap.Width, DepthType.Cv8U, 3, data, step);

            //Bitmap.UnlockBits(bmpData);

            //return mat;
        }
    }
}
