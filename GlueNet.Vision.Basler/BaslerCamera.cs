using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Basler.Pylon;
using Emgu.CV.Reg;
using GlueNet.Vision.Core;
using Camera = Basler.Pylon.Camera;
using Color = System.Drawing.Color;
using ICamera = GlueNet.Vision.Core.ICamera;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace GlueNet.Vision.Basler
{
    public class BaslerCamera : ICamera, INotifyPropertyChanged
    {
        private Camera myCamera;

        private PixelDataConverter myConverter = new PixelDataConverter();

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

            myCamera.Parameters.Load("CameraParameters.pfs", ParameterPath.CameraDevice);

            myCamera.Parameters[PLCamera.GevHeartbeatTimeout].SetValue(10000, IntegerValueCorrection.Nearest);

            //myCamera.Parameters[PLCamera.ExposureTimeAbs].SetValue(10000);
            
            //myCamera.Parameters[PLCamera.TriggerMode].SetValue("On");

            myCamera.Parameters[PLCamera.TriggerSource].SetValue(PLCamera.TriggerSource.Software);

            Console.WriteLine($"Mode : {myCamera.Parameters[PLCamera.TriggerSource].GetValue()}");
            Console.WriteLine($"Exp Time : {myCamera.Parameters[PLCamera.ExposureTimeRaw].GetValue()}");
        }

        private void OnGrabStopped(object sender, GrabStopEventArgs e)
        {
            myGrabOver = false;
        }

        private void OnGrabStarted(object sender, EventArgs e)
        {
            myGrabOver = true;
        }

        private void OnImageGrabbed(Object sender, ImageGrabbedEventArgs e)
        {
            try
            {
                IGrabResult grabResult = e.GrabResult;

                PixelFormat pixelFormat;

                if (grabResult.IsValid)
                {

                    Bitmap bitmap = new Bitmap(grabResult.Width, grabResult.Height, PixelFormat.Format24bppRgb);

                    BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);

                    myConverter.OutputPixelFormat = PixelType.BGR8packed;
                    IntPtr ptrBmp = bmpData.Scan0;
                    myConverter.Convert(ptrBmp, bmpData.Stride * bitmap.Height, grabResult);
                    bitmap.UnlockBits(bmpData);

                    if (myGrabOver)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            CaptureCompleted?.Invoke(this, new BaslerCaptureCompletedArgs(bitmap));
                        });
                    }
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.ToString());
            }
            finally
            {
                // Dispose the grab result if needed for returning it to the grab loop.
                e.DisposeGrabResultIfClone();
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
                //TriggerMode = TriggerModes.Continues;
                //myCamera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.SingleFrame);
                //myCamera.StreamGrabber.Start(GrabStrategy.LatestImages, GrabLoop.ProvidedByStreamGrabber);

                ContinuousShot();
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
                //TriggerMode = TriggerModes.SoftTrigger;
                ////myCamera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.SingleFrame);
                //myCamera.StreamGrabber.Start(1, GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);

                //if (myCamera.WaitForFrameTriggerReady(5000, TimeoutHandling.ThrowException))
                //{
                //    myCamera.ExecuteSoftwareTrigger();
                //}

                OneShot();
            }
            catch (Exception e)
            {
                myCamera.StreamGrabber.Stop();
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
            TriggerMode = TriggerModes.SoftTrigger;
        }

        public void SetContinuousMode()
        {
            TriggerMode = TriggerModes.Continues;
        }

        private void OneShot()
        {
            try
            {
                Configuration.AcquireSingleFrame(myCamera, null);
                myCamera.StreamGrabber.Start(1, GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.ToString());
            }
        }


        private void ContinuousShot()
        {
            try
            {
                Configuration.AcquireContinuous(myCamera, null);
                myCamera.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.ToString());
            }
        }
    }
}
