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
using System.Windows.Media.Media3D;

namespace GlueNet.Vision.Basler.WpfApp
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public BitmapSource BitmapSource { get; set; }
        public ICamera Camera { get; set; }
        public float Gain { get; set; }
        public float Fps { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public MainWindow()
        {
            var factory = new BaslerCameraFactory();
            var cameraInfos = factory.EnumerateCameras().ToList();

            if (cameraInfos.Count != 0)
            {
                Camera = factory.CreateCamera(cameraInfos.FirstOrDefault());
                Camera.CaptureCompleted += Camera_CaptureCompleted;
                Camera.TriggerMode = TriggerModes.Continues;
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
                        BitmapSource = ConvertToBitmapImage(bitmap);
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

        private void GetParmOnClick(object sender, RoutedEventArgs e)
        {
            Gain = Camera.GetGain();

            if (Camera is BaslerCamera camera)
            {
                Fps = camera.GetFps();
            }
        }

        private void SetParmOnClick(object sender, RoutedEventArgs e)
        {
            Camera.SetGain(Gain);
        }

        private void StartPlayOnClick(object sender, RoutedEventArgs e)
        {
            Camera.StartPlay();
        }

        private void StopPlayOnClick(object sender, RoutedEventArgs e)
        {
            Camera.StopPlay();
        }

        private void CaptureOnClick(object sender, RoutedEventArgs e)
        {
            Camera.SoftTrigger();
        }

        private void CloseCameraOnClick(object sender, RoutedEventArgs e)
        {
            Camera.Dispose();
        }

        private void OpenCameraOnClick(object sender, RoutedEventArgs e)
        {
            var factory = new BaslerCameraFactory();
            var cameraInfos = factory.EnumerateCameras().ToList();

            if (cameraInfos.Count != 0)
            {
                Camera = factory.CreateCamera(cameraInfos.FirstOrDefault());
                Camera.CaptureCompleted += Camera_CaptureCompleted;
            }
        }


        private void ChangeTriggerMode_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Camera.IsPlaying)
            {
                MessageBox.Show("Camera is playing, please stop it first.");
                e.Handled = true;
            }
        }
    }
}
