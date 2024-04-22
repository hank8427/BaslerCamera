using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Basler.Pylon;
using Emgu.CV.Reg;
using GlueNet.Vision.Core;
using Color = System.Drawing.Color;
using ICamera = GlueNet.Vision.Core.ICamera;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace GlueNet.Vision.Basler
{
    public class BaslerCamera : ICamera, INotifyPropertyChanged
    {
        private Camera myCamera;

        private bool myGrabOver = false;
        public CameraInfo CameraInfo { get; }
        public TriggerModes TriggerMode { get; set; }
        public bool IsPlaying { get; set; }
        public FrameInfo FrameInfo { get; }

        public event EventHandler<ICaptureCompletedArgs> CaptureCompleted;

        public event PropertyChangedEventHandler PropertyChanged;

        public void InitCamera(CameraInfo cameraInfo)
        {
            myCamera = new Camera(cameraInfo.Raw as ICameraInfo);

            myCamera.CameraOpened += Configuration.AcquireContinuous;
            myCamera.StreamGrabber.GrabStarted += OnGrabStarted;
            myCamera.StreamGrabber.ImageGrabbed += OnImageGrabbed;
            myCamera.StreamGrabber.GrabStopped += OnGrabStopped;

            myCamera.Open();

            myCamera.Parameters[PLCamera.GevHeartbeatTimeout].SetValue(1000);

            Console.WriteLine($"Mode : {myCamera.Parameters[PLCamera.AcquisitionMode].GetValue()}");
        }

        private void OnGrabStopped(object sender, GrabStopEventArgs e)
        {
            myGrabOver = false;
        }

        private void OnGrabStarted(object sender, EventArgs e)
        {
            myGrabOver = true;
        }

        private void OnImageGrabbed(object sender, ImageGrabbedEventArgs e)
        {
            IGrabResult grabResult = e.GrabResult;

            Bitmap bitmap;

            if (IsMonoData(grabResult))
            {
                bitmap = ToBitmap(grabResult, PixelFormat.Format8bppIndexed);
            }
            else
            {
                bitmap = ToBitmap(grabResult, PixelFormat.Format24bppRgb);
            }


            if (myGrabOver)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CaptureCompleted?.Invoke(this, new BaslerCaptureCompletedArgs(bitmap));
                });
            }
        }

        public void Dispose()
        {
            try
            {
                if (myCamera == null)
                {
                    return;
                }

                myCamera.Close();
                myCamera.Dispose();
                myCamera = null;

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void StartPlay()
        {
            try
            {
                //myCamera.StreamGrabber.Start();
                myCamera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                myCamera.StreamGrabber.Start(GrabStrategy.LatestImages, GrabLoop.ProvidedByUser);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void StopPlay()
        {
            try
            {
                myCamera.StreamGrabber.Stop();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void SoftTrigger()
        {
            try
            {
                myCamera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.SingleFrame);
                myCamera.StreamGrabber.Start(1, GrabStrategy.LatestImages, GrabLoop.ProvidedByUser);
                //myCamera.ExecuteSoftwareTrigger();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void SetGain(float gain)
        {
            try
            {
                myCamera.Parameters[PLCamera.GainAuto].TrySetValue(PLCamera.GainAuto.Off);

                long min = myCamera.Parameters[PLCamera.GainRaw].GetMinimum();
                long max = myCamera.Parameters[PLCamera.GainRaw].GetMaximum();
                long incr = myCamera.Parameters[PLCamera.GainRaw].GetIncrement();
                if (gain < min)
                {
                    gain = min;
                }
                else if (gain > max)
                {
                    gain = max;
                }
                else
                {
                    gain = min + (((gain - min)/incr) * incr);
                }

                myCamera.Parameters[PLCamera.GainRaw].SetValue((long)gain);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public float GetGain()
        {
            try
            {
                var gain = myCamera.Parameters[PLCamera.GainRaw].GetValue();
                return (float)gain;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 0;
            }
        }

        public void SetBuffer(IntPtr address)
        {
            throw new NotImplementedException();
        }

        public bool IsMonoData(IGrabResult iGrabResult)
        {
            switch (iGrabResult.PixelTypeValue)
            {
                case PixelType.Mono1packed:
                case PixelType.Mono2packed:
                case PixelType.Mono4packed:
                case PixelType.Mono8:
                case PixelType.Mono8signed:
                case PixelType.Mono10:
                case PixelType.Mono10p:
                case PixelType.Mono10packed:
                case PixelType.Mono12:
                case PixelType.Mono12p:
                case PixelType.Mono12packed:
                case PixelType.Mono16:
                    return true;
                default:
                    return false;
            }
        }

        private Bitmap ToBitmap(IGrabResult grabResult, PixelFormat pixelFormat)
        {
            Bitmap bitmap = null;

            using (grabResult)
            {
                if (grabResult.GrabSucceeded)
                {
                    var buffer = grabResult.PixelData as byte[];
                    bitmap = new Bitmap(grabResult.Width, grabResult.Height, pixelFormat);
                    var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
                    
                    int stride = bitmapData.Stride;
                    int offset = stride - grabResult.Width;
                    IntPtr ptr = bitmapData.Scan0;
                    int scanBytes = stride * bitmap.Height;

                    int posScan = 0, posReal = 0;
                    byte[] pixelValues = new byte[scanBytes];

                    for (int x = 0; x < grabResult.Height; x++)
                    {
                        for (int y = 0; y < grabResult.Width; y++)
                        {
                            pixelValues[posScan++] = buffer[posReal++];
                        }
                        posScan += offset;
                    }

                    System.Runtime.InteropServices.Marshal.Copy(pixelValues, 0, ptr, scanBytes);
                    bitmap.UnlockBits(bitmapData);

                    ColorPalette tempPalette;
                    using (Bitmap tempBitmap = new Bitmap(1, 1, pixelFormat))
                    {
                        tempPalette = tempBitmap.Palette;
                    }

                    for (int i = 0; i < 256; i++)
                    {
                        tempPalette.Entries[i] = Color.FromArgb(i, i, i);
                    }

                    bitmap.Palette = tempPalette;
                }
            }

            return bitmap;
        }

        public void SetTriggerMode()
        {
            myCamera.CameraOpened -= Configuration.AcquireContinuous;
            myCamera.CameraOpened -= Configuration.SoftwareTrigger;
            myCamera.CameraOpened += Configuration.SoftwareTrigger;

            TriggerMode = TriggerModes.SoftTrigger;
        }

        public void SetContinuousMode()
        {
            myCamera.CameraOpened -= Configuration.AcquireContinuous;
            myCamera.CameraOpened -= Configuration.SoftwareTrigger;
            myCamera.CameraOpened += Configuration.SoftwareTrigger;

            TriggerMode = TriggerModes.Continues;
        }
    }
}
