# pcan-manager
![Build history](https://buildstats.info/nuget/PCANDevice)

PeakCAN USB device helper class for C#

## How to use this nuget package?

```
using System.Collections.Generic;
using PCANDevice;
namespace ConsoleApp
{
    class Program
    {
        public static int callback(object[] args)
        {
            TPCANMsg msg = (TPCANMsg)args[0];

            return 0;
        }


        static void Main(string[] args)
        {
            List<ushort> interfaces = PCANDevice.PCANManager.GetAllAvailable();
            PCANDevice.PCANManager pcan = new PCANDevice.PCANManager();

            var oo = pcan.Connect(interfaces[0], PCANDevice.TPCANBaudrate.PCAN_BAUD_500K);
            pcan.AddReceiveCallback(callback);
            pcan.SendFrame(1, 8, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
            pcan.SendFrameExt(0x7ff1, 8, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
            pcan.ActivateAutoReceive();

            ...
            ...
            ...


            pcan.Disconnect();
        }
    }
}
```
