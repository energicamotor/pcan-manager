using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCANDevice
{
    public class StatusChanged : EventArgs
    {
        public bool status { get; set; }
    }

    public enum PCANStatus
    {
        OK = 0x00,
        Connected = 0x01,
        Error = 0xff
    };


    public delegate int PCANCallback(object[] args);



    public class PCANManager
    {
        public bool isConnected = false;

        public event EventHandler<StatusChanged> Connected;
        public event EventHandler<StatusChanged> Disconnected;

        private ushort device = 0x00;

        private Thread InstanceCaller;
        private volatile bool autoReceiveStatus = false;
        public List<PCANCallback> receiveCallbacks = new List<PCANCallback>();

        protected virtual void OnConnect(StatusChanged e)
        {
            Connected?.Invoke(this, e);
        }

        protected virtual void OnDisconnect(StatusChanged e)
        {
            Disconnected?.Invoke(this, e);
        }

        private static ushort[] m_HandlesArray = new ushort[]
        {
                PCANBasic.PCAN_ISABUS1,
                PCANBasic.PCAN_ISABUS2,
                PCANBasic.PCAN_ISABUS3,
                PCANBasic.PCAN_ISABUS4,
                PCANBasic.PCAN_ISABUS5,
                PCANBasic.PCAN_ISABUS6,
                PCANBasic.PCAN_ISABUS7,
                PCANBasic.PCAN_ISABUS8,
                PCANBasic.PCAN_DNGBUS1,
                PCANBasic.PCAN_PCIBUS1,
                PCANBasic.PCAN_PCIBUS2,
                PCANBasic.PCAN_PCIBUS3,
                PCANBasic.PCAN_PCIBUS4,
                PCANBasic.PCAN_PCIBUS5,
                PCANBasic.PCAN_PCIBUS6,
                PCANBasic.PCAN_PCIBUS7,
                PCANBasic.PCAN_PCIBUS8,
                PCANBasic.PCAN_PCIBUS9,
                PCANBasic.PCAN_PCIBUS10,
                PCANBasic.PCAN_PCIBUS11,
                PCANBasic.PCAN_PCIBUS12,
                PCANBasic.PCAN_PCIBUS13,
                PCANBasic.PCAN_PCIBUS14,
                PCANBasic.PCAN_PCIBUS15,
                PCANBasic.PCAN_PCIBUS16,
                PCANBasic.PCAN_USBBUS1,
                PCANBasic.PCAN_USBBUS2,
                PCANBasic.PCAN_USBBUS3,
                PCANBasic.PCAN_USBBUS4,
                PCANBasic.PCAN_USBBUS5,
                PCANBasic.PCAN_USBBUS6,
                PCANBasic.PCAN_USBBUS7,
                PCANBasic.PCAN_USBBUS8,
                PCANBasic.PCAN_USBBUS9,
                PCANBasic.PCAN_USBBUS10,
                PCANBasic.PCAN_USBBUS11,
                PCANBasic.PCAN_USBBUS12,
                PCANBasic.PCAN_USBBUS13,
                PCANBasic.PCAN_USBBUS14,
                PCANBasic.PCAN_USBBUS15,
                PCANBasic.PCAN_USBBUS16,
                PCANBasic.PCAN_PCCBUS1,
                PCANBasic.PCAN_PCCBUS2,
                PCANBasic.PCAN_LANBUS1,
                PCANBasic.PCAN_LANBUS2,
                PCANBasic.PCAN_LANBUS3,
                PCANBasic.PCAN_LANBUS4,
                PCANBasic.PCAN_LANBUS5,
                PCANBasic.PCAN_LANBUS6,
                PCANBasic.PCAN_LANBUS7,
                PCANBasic.PCAN_LANBUS8,
                PCANBasic.PCAN_LANBUS9,
                PCANBasic.PCAN_LANBUS10,
                PCANBasic.PCAN_LANBUS11,
                PCANBasic.PCAN_LANBUS12,
                PCANBasic.PCAN_LANBUS13,
                PCANBasic.PCAN_LANBUS14,
                PCANBasic.PCAN_LANBUS15,
                PCANBasic.PCAN_LANBUS16,
        };

        private volatile object thread_lock = new object();
        private volatile object thread_lock_callbacks = new object();

        public static List<ushort> GetAllAvailable()
        {
            try
            {
                List<ushort> availableInterfaces = new List<ushort>();
                UInt32 iBuffer;

                for (int i = 0; i < m_HandlesArray.Length; i++)
                {
                    if (m_HandlesArray[i] > PCANBasic.PCAN_DNGBUS1)
                    {
                        TPCANStatus stsResult = PCANBasic.GetValue(m_HandlesArray[i], TPCANParameter.PCAN_CHANNEL_CONDITION, out iBuffer, sizeof(UInt32));
                        if ((stsResult == TPCANStatus.PCAN_ERROR_OK) && ((iBuffer & PCANBasic.PCAN_CHANNEL_AVAILABLE) == PCANBasic.PCAN_CHANNEL_AVAILABLE))
                        {
                            availableInterfaces.Add(m_HandlesArray[i]);
                        }
                    }
                }
                return availableInterfaces;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public PCANStatus Connect(ushort handle, TPCANBaudrate baud)
        {
            TPCANStatus stsResult = TPCANStatus.PCAN_ERROR_UNKNOWN;

            lock (thread_lock)
            {
                stsResult = PCANBasic.Initialize(handle, baud);
                stsResult = PCANBasic.FilterMessages(handle, 0x000, 0x1FFFFFFF, TPCANMode.PCAN_MODE_EXTENDED);
                device = stsResult == TPCANStatus.PCAN_ERROR_OK ? handle : (ushort)0x00;
            }
            OnConnect(new StatusChanged() { status = true });
            return stsResult == TPCANStatus.PCAN_ERROR_OK ? PCANStatus.OK : PCANStatus.Error;
        }

        public PCANStatus Connect(ushort handle, TPCANBaudrate baud, int filterLow, int filterHigh)
        {
            TPCANStatus stsResult = TPCANStatus.PCAN_ERROR_UNKNOWN;
            try
            {
                lock (thread_lock)
                {
                    stsResult = PCANBasic.Initialize(handle, baud);
                    stsResult = PCANBasic.FilterMessages(handle, (uint)filterLow, (uint)filterHigh, TPCANMode.PCAN_MODE_EXTENDED);
                    device = stsResult == TPCANStatus.PCAN_ERROR_OK ? handle : (ushort)0x00;
                }
                OnConnect(new StatusChanged() { status = true });
                return stsResult == TPCANStatus.PCAN_ERROR_OK ? PCANStatus.OK : PCANStatus.Error;
            }
            catch (Exception)
            {
                return PCANStatus.Error;
            }
        }

        public PCANStatus Disconnect()
        {
            TPCANStatus stsResult = TPCANStatus.PCAN_ERROR_UNKNOWN;
            try
            {
                lock (thread_lock)
                {
                    DeactivateAutoReceive();
                    stsResult = PCANBasic.Uninitialize(device);
                    device = 0x00;
                }
                OnDisconnect(new StatusChanged() { status = false });
                return stsResult == TPCANStatus.PCAN_ERROR_OK ? PCANStatus.OK : PCANStatus.Error;
            }
            catch (Exception)
            {
                device = 0x00;
                return PCANStatus.Error;
            }
        }
        TPCANMsg _CANMsg = new TPCANMsg() { MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD };
        TPCANMsg _CANMsgExt = new TPCANMsg() { MSGTYPE = TPCANMessageType.PCAN_MESSAGE_EXTENDED };
        byte[] tmp = new byte[8];

        public PCANStatus SendFrame(int canID, int DLC, byte[] data)
        {
            TPCANStatus stsResult = TPCANStatus.PCAN_ERROR_UNKNOWN;
            try
            {
                byte[] tmp = new byte[8];
                for (int i = 0; i < data.Length; i++)
                    tmp[i] = data[i];


                _CANMsg.ID = (uint)canID;
                _CANMsg.LEN = (byte)DLC;
                _CANMsg.DATA = tmp;

                lock (thread_lock)
                {
                    stsResult = PCANBasic.Write(device, ref _CANMsg);
                }

                return stsResult == TPCANStatus.PCAN_ERROR_OK ? PCANStatus.OK : PCANStatus.Error;
            }
            catch (Exception)
            {
                return PCANStatus.Error;
            }
        }
        public PCANStatus SendFrameExt(int canID, int DLC, byte[] data)
        {
            TPCANStatus stsResult = TPCANStatus.PCAN_ERROR_UNKNOWN;
            try
            {
                byte[] tmp = new byte[8];
                for (int i = 0; i < data.Length; i++)
                    tmp[i] = data[i];


                _CANMsgExt.ID = (uint)canID;
                _CANMsgExt.LEN = (byte)DLC;
                _CANMsgExt.DATA = tmp;

                lock (thread_lock)
                {
                    stsResult = PCANBasic.Write(device, ref _CANMsgExt);
                }

                return stsResult == TPCANStatus.PCAN_ERROR_OK ? PCANStatus.OK : PCANStatus.Error;
            }
            catch (Exception)
            {
                return PCANStatus.Error;
            }
        }
        public PCANStatus RetrieveFrame(out TPCANMsg CANMsg, out TPCANTimestamp CANTimeStamp)
        {
            TPCANStatus stsResult = TPCANStatus.PCAN_ERROR_UNKNOWN;

            CANMsg = new TPCANMsg();

            try
            {
                lock (thread_lock)
                {
                    stsResult = PCANBasic.Read(device, out CANMsg, out CANTimeStamp);
                }

                return CANMsg.MSGTYPE == TPCANMessageType.PCAN_MESSAGE_STATUS ? PCANStatus.Error : (stsResult != TPCANStatus.PCAN_ERROR_QRCVEMPTY ? PCANStatus.OK : PCANStatus.Error);
            }
            catch (Exception)
            {
                CANMsg = new TPCANMsg();
                CANTimeStamp = new TPCANTimestamp();
                return PCANStatus.Error;
            }
        }

        public int IsConnected()
        {
            return device;
        }

        public void AddReceiveCallback(PCANCallback callback)
        {
            receiveCallbacks.Add(callback);
        }

        public void RemoveReceiveCallback(PCANCallback callback)
        {
            int index_found = -1;

            lock (thread_lock_callbacks)
            {
                for (int i = (receiveCallbacks.Count - 1); i >= 0; i--)
                    if (receiveCallbacks[i] == callback)
                    {
                        index_found = i;
                        break;
                    }

                if (index_found != -1)
                    receiveCallbacks.RemoveAt(index_found);
            }
        }

        public void ActivateAutoReceive()
        {
            InstanceCaller = new Thread(new ThreadStart(AutoReceive));
            InstanceCaller.Priority = ThreadPriority.AboveNormal;
            autoReceiveStatus = true;
            InstanceCaller.Start();
        }

        public void DeactivateAutoReceive()
        {
            autoReceiveStatus = false;
        }

        private void AutoReceive()
        {
            TPCANMsg msg;
            TPCANTimestamp timestamp;
            List<PCANCallback> toRemoveCallbacks = new List<PCANCallback>();
            bool isreceived = false;
            while (autoReceiveStatus)
            {
                while (RetrieveFrame(out msg, out timestamp) == PCANStatus.OK)
                {
                    if (autoReceiveStatus == false)
                    {
                        receiveCallbacks.Clear();
                        return;
                    }
                    isreceived = true;
                    lock (thread_lock_callbacks)
                    {
                        try
                        {
                            for (int i = (receiveCallbacks.Count - 1); i >= 0; i--)
                                if (receiveCallbacks[i](new object[] { msg, timestamp }) != 0x00)
                                    toRemoveCallbacks.Add(receiveCallbacks[i]);

                            if (toRemoveCallbacks.Count != 0)
                            {
                                for (int i = (toRemoveCallbacks.Count - 1); i >= 0; i--)
                                    receiveCallbacks.Remove(toRemoveCallbacks[i]);

                                toRemoveCallbacks.Clear();
                            }
                        }
                        catch (Exception)
                        {

                        }
                    }
                }
                if (isreceived == true)
                {
                    isreceived = false;
                }
                else
                {
                    Thread.Sleep(2);
                }
            }
            receiveCallbacks.Clear();
        }
    }
}
