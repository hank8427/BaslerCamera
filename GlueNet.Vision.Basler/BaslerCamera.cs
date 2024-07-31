using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Basler.Pylon;
using GlueNet.Vision.Core;
using Camera = Basler.Pylon.Camera;
using Color = System.Drawing.Color;
using ICamera = GlueNet.Vision.Core.ICamera;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace GlueNet.Vision.Basler
{
    public class BaslerCamera : ICamera, INotifyPropertyChanged
    {
        private TriggerModes myTriggerMode;

        private int myGrabFrame;

        private DateTime myStartTime;

        private DateTime myAccumTime;

        private Camera myCamera;

        private float myFps;

        private PixelDataConverter myConverter = new PixelDataConverter();

        private bool myGrabOver = false;

        public CameraInfo CameraInfo { get; private set; }
        public TriggerModes TriggerMode
        {
            get { return myTriggerMode; }
            set
            {
                myTriggerMode = value;

                if (!IsPlaying)
                {
                    OnTriggerModeChanged();
                }

            }
        }

        private void OnTriggerModeChanged()
        {
            switch (TriggerMode)
            {
                case TriggerModes.Continues:
                    myCamera.Parameters[PLCamera.TriggerMode].SetValue(PLCamera.TriggerMode.Off);
                    myCamera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                    break;
                case TriggerModes.HardTrigger:
                    myCamera.Parameters[PLCamera.TriggerMode].SetValue(PLCamera.TriggerMode.On);
                    myCamera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.SingleFrame);
                    myCamera.Parameters[PLCamera.TriggerSource].SetValue(PLCamera.TriggerSource.Line1);
                    myCamera.Parameters[PLCamera.TriggerSelector].SetValue(PLCamera.TriggerSelector.FrameStart);
                    myCamera.Parameters[PLCamera.TriggerActivation].SetValue(PLCamera.TriggerActivation.RisingEdge);
                    break;
                case TriggerModes.SoftTrigger:
                    myCamera.Parameters[PLCamera.TriggerMode].SetValue(PLCamera.TriggerMode.On);
                    myCamera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                    myCamera.Parameters[PLCamera.TriggerSource].SetValue(PLCamera.TriggerSource.Software);
                    myCamera.Parameters[PLCamera.TriggerSelector].SetValue(PLCamera.TriggerSelector.FrameStart);
                    myCamera.Parameters[PLCamera.TriggerActivation].SetValue(PLCamera.TriggerActivation.RisingEdge);
                    break;
            }
        }

        public bool IsPlaying { get; set; }
        public FrameInfo FrameInfo { get; }

        public event EventHandler<ICaptureCompletedArgs> CaptureCompleted;

        public event PropertyChangedEventHandler PropertyChanged;

        public void InitCamera(CameraInfo cameraInfo)
        {
            CameraInfo = cameraInfo;
            myCamera = new Camera(cameraInfo.Raw as ICameraInfo);

            myCamera.CameraOpened += Configuration.AcquireContinuous;
            myCamera.StreamGrabber.GrabStarted += OnGrabStarted;
            myCamera.StreamGrabber.ImageGrabbed += OnImageGrabbed;
            myCamera.StreamGrabber.GrabStopped += OnGrabStopped;

            myCamera.Open();

            if (File.Exists($"{Environment.CurrentDirectory}\\CameraParameters.pfs"))
            {
                myCamera.Parameters.Load($"{Environment.CurrentDirectory}\\CameraParameters.pfs", ParameterPath.CameraDevice);
            }

            //myCamera.Parameters[PLCamera.GevHeartbeatTimeout].SetValue(10000, IntegerValueCorrection.Nearest);

            //myCamera.Parameters[PLCamera.Width].SetValue(1280);
            //myCamera.Parameters[PLCamera.Height].SetValue(960);

            myCamera.Parameters[PLCamera.TriggerSource].SetValue(PLCamera.TriggerSource.Software);
            //myCamera.Parameters[PLCamera.ReverseX].SetValue(true);
            //myCamera.Parameters[PLCamera.ReverseY].SetValue(true);
            Console.WriteLine($"Mode : {myCamera.Parameters[PLCamera.TriggerSource].GetValue()}");
            Console.WriteLine($"Exp Time : {myCamera.Parameters[PLCamera.ExposureTime].GetValue()}");
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
                // For SoftTrigger Test
                //myStartTime = DateTime.Now;

                IGrabResult grabResult = e.GrabResult;

                PixelFormat pixelFormat;

                if (grabResult.IsValid)
                {
                    if (IsMonoData(grabResult))
                    {
                        pixelFormat = PixelFormat.Format8bppIndexed;
                        myConverter.OutputPixelFormat = PixelType.Mono8;
                    }
                    else
                    {
                        pixelFormat = PixelFormat.Format24bppRgb;
                        myConverter.OutputPixelFormat = PixelType.BayerBG8;
                    }

                    Bitmap bitmap = new Bitmap(grabResult.Width, grabResult.Height, pixelFormat);

                    BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);

                    IntPtr ptrBmp = bmpData.Scan0;


                    myConverter.Convert(ptrBmp, bmpData.Stride * bitmap.Height, grabResult);
                    

                    bitmap.UnlockBits(bmpData);

                    if (myGrabOver)
                    {
                        //Application.Current.Dispatcher.Invoke(() =>
                        //{
                        //    CaptureCompleted?.Invoke(this, new BaslerCaptureCompletedArgs(bitmap));
                        //});
                        
                        Task.Run(() =>
                        {
                            CaptureCompleted?.Invoke(this, new BaslerCaptureCompletedArgs(bitmap));
                        });

                        myGrabFrame++;
                        myAccumTime = DateTime.Now;
                        var elapsedTime = (myAccumTime - myStartTime).TotalSeconds;
                        myFps = (float)(myGrabFrame / elapsedTime);
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
                ContinuousShot();
                IsPlaying = true;
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
                myCamera?.StreamGrabber.Stop();
                IsPlaying = false;
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
                //myCamera.StreamGrabber.Start(1, GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);

                myGrabFrame = 0;

                myCamera.ExecuteSoftwareTrigger();
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

                double min = myCamera.Parameters[PLCamera.Gain].GetMinimum();
                double max = myCamera.Parameters[PLCamera.Gain].GetMaximum();
                double? incr = myCamera.Parameters[PLCamera.Gain].GetIncrement();
                if (gain < min)
                {
                    gain = (float)min;
                }
                else if (gain > max)
                {
                    gain = (float)max;
                }
                //else
                //{
                //    gain = (float)(min + (((gain - min)/incr) * incr));
                //}

                myCamera.Parameters[PLCamera.Gain].SetValue((long)gain);
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
                var gain = myCamera.Parameters[PLCamera.Gain].GetValue();
                return (float)gain;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 0;
            }
        }

        public float GetFps()
        {
            try
            {
                //var fps = myCamera.Parameters[PLCamera.AcquisitionFrameRate].GetValue();
                //var fps = myCamera.Parameters[PLCamera.ResultingFrameRate].GetValue();

                return myFps;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 0;
            }
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
                myGrabFrame = 0;
                myStartTime = DateTime.Now;
                //Configuration.AcquireContinuous(myCamera, null);

                myCamera.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.ToString());
            }
        }

        public void SetBuffer(IntPtr[] address)
        {
            throw new NotImplementedException();
        }
    }
}
