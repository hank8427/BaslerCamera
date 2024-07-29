using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Basler.Pylon;
using GlueNet.Vision.Core;
using ICamera = GlueNet.Vision.Core.ICamera;

namespace GlueNet.Vision.Basler.WpfApp
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public BitmapSource BitmapSource1 { get; set; }
        public BitmapImage BitmapSource2 { get; set; }
        public ICamera Camera1 { get; set; }
        public ICamera Camera2 { get; set; }
        public bool IsContinuous { get; set; } = false;
        public bool IsSoftTrigger { get; set; } = true;
        public float Gain { get; set; }
        public float Fps { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public MainWindow()
        {
            var factory = new BaslerCameraFactory();
            var cameraInfos = factory.EnumerateCameras().ToList();

            if (cameraInfos.Count != 0)
            {
                Camera1 = factory.CreateCamera(cameraInfos.FirstOrDefault());
                Camera1.CaptureCompleted += Camera_CaptureCompleted;
            }

            InitializeComponent();
        }

        private void Camera_CaptureCompleted(object sender, ICaptureCompletedArgs e)
        {
            if (e is BaslerCaptureCompletedArgs args)
            {
                var bitmap = args.Bitmap;
                if (bitmap != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        BitmapSource1 = ConvertToBitmapImage(bitmap);
                    });
                }
            }
        }

        public BitmapImage ConvertToBitmapImage(Bitmap bitmap)
        {
            MemoryStream ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Bmp);
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            image.StreamSource = ms;
            image.EndInit();

            return image;
        }

        private void ContinuousMode_Checked(object sender, RoutedEventArgs e)
        {
            if ( Camera1 is BaslerCamera camera)
            {
                camera.SetContinuousMode();
            }
        }

        private void TriggerMode_Checked(object sender, RoutedEventArgs e)
        {
            if (Camera1 is BaslerCamera camera)
            {
                camera.SetTriggerMode();
            }
        }

        private void GetParmOnClick(object sender, RoutedEventArgs e)
        {
            Gain = Camera1.GetGain();

            if (Camera1 is BaslerCamera camera)
            {
                Fps = camera.GetFps();
            }
        }

        private void SetParmOnClick(object sender, RoutedEventArgs e)
        {
            Camera1.SetGain(Gain);
        }

        private void StartPlayOnClick(object sender, RoutedEventArgs e)
        {
            IsContinuous = true;
            Camera1.StartPlay();
        }

        private void StopPlayOnClick(object sender, RoutedEventArgs e)
        {
            Camera1.StopPlay();
        }

        private void CaptureOnClick(object sender, RoutedEventArgs e)
        {
            IsSoftTrigger = true;
            Camera1.SoftTrigger();
        }

        private void CloseCameraOnClick(object sender, RoutedEventArgs e)
        {
            Camera1.Dispose();
        }

        private void OpenCameraOnClick(object sender, RoutedEventArgs e)
        {
            var factory = new BaslerCameraFactory();
            var cameraInfos = factory.EnumerateCameras().ToList();

            if (cameraInfos.Count != 0)
            {
                Camera1 = factory.CreateCamera(cameraInfos.FirstOrDefault());
                Camera1.CaptureCompleted += Camera_CaptureCompleted;
            }
        }

        private void HardwareTrigger_OnClick(object sender, RoutedEventArgs e)
        {
            if (Camera1 is BaslerCamera camera)
            {
                camera.HardwareTrigger();
            }
        }
    }
}
