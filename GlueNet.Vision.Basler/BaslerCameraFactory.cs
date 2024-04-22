using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;
using GlueNet.Vision.Core;
using Basler.Pylon;
using ICamera = GlueNet.Vision.Core.ICamera;

namespace GlueNet.Vision.Basler
{
    public class BaslerCameraFactory : ICameraFactory
    {
        public IEnumerable<CameraInfo> EnumerateCameras()
        {
            var allCameras = CameraFinder.Enumerate();

            var cameraInfoList = new List<CameraInfo>();
            
            foreach (var cameraInfo in allCameras)
            {
                cameraInfoList.Add(new CameraInfo(typeof(BaslerCameraFactory).ToString(),
                                                            cameraInfo[CameraInfoKey.ModelName],
                                                            cameraInfo[CameraInfoKey.SerialNumber],
                                                            cameraInfo));
            }


            return cameraInfoList;
        }

        public ICamera CreateCamera(CameraInfo cameraInfo)
        {
            var camera = new BaslerCamera();
            camera.InitCamera(cameraInfo);
            return camera;
        }

        public ICamera CreateCamera(string serial)
        {
            throw new NotImplementedException();
        }
    }
}
