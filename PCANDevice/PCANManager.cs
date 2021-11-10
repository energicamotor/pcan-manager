/*
    PCAN Manager Library: more info at http://www.devcoons.com
    Copyright (C) 2021 Ioannis Deligiannis | Devcoons Blog

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCANDevice
{
    /// <summary>
    /// Event data for OnConnect, OnDisconnect
    /// </summary>
    public class StatusChanged : EventArgs
    {
        public bool status { get; set; }
    }

    /// <summary>
    /// Represents a generic PCAN status
    /// </summary>
    public enum PCANStatus
    {
        OK = 0x00,
        Connected = 0x01,
        Error = 0xff
    };

    /// <summary>
    /// Delegate of ReceiveCallback functions
    /// </summary>
    public delegate int PCANCallback(object[] args);

    /// <summary>
    /// Abstraction Layer of PCANBasic 
    /// </summary>
    public class PCANManager
    {
        public bool isConnected = false;
        public event EventHandler<StatusChanged> Connected;
        public event EventHandler<StatusChanged> Disconnected;
        public List<PCANCallback> receiveCallbacks = new List<PCANCallback>();
        private bool isFDEnabled = false;
        private ushort device = 0x00;
        private Thread InstanceCaller;
        public bool IsTransmitting = false;
        private volatile bool autoReceiveStatus = false;
        public volatile object TransmissionLock = new object();
        private volatile object thread_lock = new object();
        private volatile object thread_send_lock = new object();
        private volatile object thread_lock_callbacks = new object();
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
        private TPCANMsg _CANMsg = new TPCANMsg() { MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD };  
        private TPCANMsg _CANMsgExt = new TPCANMsg() { MSGTYPE = TPCANMessageType.PCAN_MESSAGE_EXTENDED };
        private TPCANMsgFD _CANMsgFD = new TPCANMsgFD() { MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD };
        private TPCANMsgFD _CANMsgFDExt = new TPCANMsgFD() { MSGTYPE = TPCANMessageType.PCAN_MESSAGE_EXTENDED };

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

        public static bool IsPCANFD(ushort handle)
        {
            TPCANChannelInformation[] BUFF = new TPCANChannelInformation[1];
            PCANBasic.GetValue(handle, TPCANParameter.PCAN_HARDWARE_NAME, BUFF);
            return BUFF[0].device_name.ToLower().Contains("fd");
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
            isFDEnabled = false;
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
                isFDEnabled = false;
                return stsResult == TPCANStatus.PCAN_ERROR_OK ? PCANStatus.OK : PCANStatus.Error;
            }
            catch (Exception)
            {
                return PCANStatus.Error;
            }
        }


        private string CANFDConfiguration(TPCANBaudrate baud,TPCANDatarate data)
        {
            string configuration = "";

            switch (baud)
            {
                case TPCANBaudrate.PCAN_BAUD_250K:
                    switch (data)
                    {
                        case TPCANDatarate.PCAN_DATARATE_2M:
                            configuration = "f_clock = 80000000, nom_brp = 20, nom_tseg1 = 12, nom_tseg2 = 3, nom_sjw = 1, data_brp = 4, data_tseg1 = 7, data_tseg2 = 2, data_sjw = 1";
                            break;
                        case TPCANDatarate.PCAN_DATARATE_4M:
                            configuration = "f_clock = 80000000, nom_brp = 20, nom_tseg1 = 12, nom_tseg2 = 3, nom_sjw = 1, data_brp = 2, data_tseg1 = 7, data_tseg2 = 2, data_sjw = 1";
                            break;
                        case TPCANDatarate.PCAN_DATARATE_8M:
                            configuration = "f_clock = 80000000, nom_brp = 20, nom_tseg1 = 12, nom_tseg2 = 3, nom_sjw = 1, data_brp = 1, data_tseg1 = 7, data_tseg2 = 2, data_sjw = 1";
                            break;
                        case TPCANDatarate.PCAN_DATARATE_10M:
                            configuration = "f_clock = 80000000, nom_brp = 20, nom_tseg1 = 12, nom_tseg2 = 3, nom_sjw = 1, data_brp = 1, data_tseg1 = 5, data_tseg2 = 2, data_sjw = 1";
                            break;
                        default:
                            return null;
                    }
                    break;
                case TPCANBaudrate.PCAN_BAUD_500K:
                    switch (data)
                    {
                        case TPCANDatarate.PCAN_DATARATE_2M:
                            configuration = "f_clock = 80000000, nom_brp = 10, nom_tseg1 = 12, nom_tseg2 = 3, nom_sjw = 1, data_brp = 4, data_tseg1 = 7, data_tseg2 = 2, data_sjw = 1";
                            break;
                        case TPCANDatarate.PCAN_DATARATE_4M:
                            configuration = "f_clock = 80000000, nom_brp = 10, nom_tseg1 = 12, nom_tseg2 = 3, nom_sjw = 1, data_brp = 2, data_tseg1 = 7, data_tseg2 = 2, data_sjw = 1";
                            break;
                        case TPCANDatarate.PCAN_DATARATE_8M:
                            configuration = "f_clock = 80000000, nom_brp = 10, nom_tseg1 = 12, nom_tseg2 = 3, nom_sjw = 1, data_brp = 1, data_tseg1 = 7, data_tseg2 = 2, data_sjw = 1";
                            break;
                        case TPCANDatarate.PCAN_DATARATE_10M:
                            configuration = "f_clock = 80000000, nom_brp = 10, nom_tseg1 = 12, nom_tseg2 = 3, nom_sjw = 1, data_brp = 1, data_tseg1 = 5, data_tseg2 = 2, data_sjw = 1";
                            break;
                        default:
                            return null;
                    }
                    break;
                case TPCANBaudrate.PCAN_BAUD_1M:
                    switch (data)
                    {
                        case TPCANDatarate.PCAN_DATARATE_2M:
                            configuration = "f_clock = 80000000, nom_brp = 10, nom_tseg1 = 5, nom_tseg2 = 2, nom_sjw = 1, data_brp = 4, data_tseg1 = 7, data_tseg2 = 2, data_sjw = 1";
                            break;
                        case TPCANDatarate.PCAN_DATARATE_4M:
                            configuration = "f_clock = 80000000, nom_brp = 10, nom_tseg1 = 5, nom_tseg2 = 2, nom_sjw = 1, data_brp = 2, data_tseg1 = 7, data_tseg2 = 2, data_sjw = 1";
                            break;
                        case TPCANDatarate.PCAN_DATARATE_8M:
                            configuration = "f_clock = 80000000, nom_brp = 10, nom_tseg1 = 5, nom_tseg2 = 2, nom_sjw = 1, data_brp = 1, data_tseg1 = 7, data_tseg2 = 2, data_sjw = 1";
                            break;
                        case TPCANDatarate.PCAN_DATARATE_10M:
                            configuration = "f_clock = 80000000, nom_brp = 10, nom_tseg1 = 5, nom_tseg2 = 2, nom_sjw = 1, data_brp = 1, data_tseg1 = 5, data_tseg2 = 2, data_sjw = 1";
                            break;
                        default:
                            return null;
                    }
                    break;
                default:
                    return null;
            }
            return configuration;
        }

        public PCANStatus ConnectFD(ushort handle, TPCANBaudrate baud, TPCANDatarate data)
        {
            TPCANStatus stsResult = TPCANStatus.PCAN_ERROR_UNKNOWN;

            lock (thread_lock)
            {
                if (IsPCANFD(handle) == false)
		{
                    return PCANStatus.Error;
		}
		
                if(baud != TPCANBaudrate.PCAN_BAUD_500K && baud != TPCANBaudrate.PCAN_BAUD_250K && baud != TPCANBaudrate.PCAN_BAUD_1M)
                {
                    return PCANStatus.Error;
                }
                string configuration = CANFDConfiguration(baud,data);

                if (string.IsNullOrEmpty(configuration))
		{
                    return PCANStatus.Error;
		}
		
                stsResult = PCANBasic.InitializeFD(handle, configuration);
                stsResult = PCANBasic.FilterMessages(handle, 0x000, 0x1FFFFFFF, TPCANMode.PCAN_MODE_EXTENDED);
                device = stsResult == TPCANStatus.PCAN_ERROR_OK ? handle : (ushort)0x00;
            }
            OnConnect(new StatusChanged() { status = true });
            isFDEnabled = true;
            return stsResult == TPCANStatus.PCAN_ERROR_OK ? PCANStatus.OK : PCANStatus.Error;
        }

        public PCANStatus ConnectFD(ushort handle, TPCANBaudrate baud, TPCANDatarate data, int filterLow, int filterHigh)
        {
            TPCANStatus stsResult = TPCANStatus.PCAN_ERROR_UNKNOWN;
            try
            {
                lock (thread_lock)
                {
                    if (IsPCANFD(handle) == false)
		    {
                        return PCANStatus.Error;
		    }
		    
                    if (baud != TPCANBaudrate.PCAN_BAUD_500K && baud != TPCANBaudrate.PCAN_BAUD_250K && baud != TPCANBaudrate.PCAN_BAUD_1M)
                    {
                        return PCANStatus.Error;
                    }
                    string configuration = CANFDConfiguration(baud, data);

                    if (string.IsNullOrEmpty(configuration))
		    {
                        return PCANStatus.Error;
		    }
		    
                    stsResult = PCANBasic.InitializeFD(handle, configuration);
                    stsResult = PCANBasic.FilterMessages(handle, (uint)filterLow, (uint)filterHigh, TPCANMode.PCAN_MODE_EXTENDED);
                    device = stsResult == TPCANStatus.PCAN_ERROR_OK ? handle : (ushort)0x00;
                }
                OnConnect(new StatusChanged() { status = true });
                isFDEnabled = true;
                return stsResult == TPCANStatus.PCAN_ERROR_OK ? PCANStatus.OK : PCANStatus.Error;
            }
            catch (Exception)
            {
                return PCANStatus.Error;
            }
        }

        protected virtual void OnConnect(StatusChanged e)
        {
            Connected?.Invoke(this, e);
        }

        protected virtual void OnDisconnect(StatusChanged e)
        {
            Disconnected?.Invoke(this, e);
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

        public int IsConnected()
        {
            return device;
        }

        public static int GetDataLength(TPCANMsgFD msg)
        {
            if (((byte)msg.DLC) <= 8)
	    {
                return msg.DLC;
	    }
	    
            switch (msg.DLC)
            {
                case 9:
                    return 12;
                case 10:
                    return 16;
                case 11:
                    return 20;
                case 12:
                    return 24;
                case 13:
                    return 32;
                case 14:
                    return 48;
                case 15:
                    return 64;
                default:
                    return 0;
            }
        }

        public PCANStatus SendFrame(int canID, int DLC, byte[] data)
        {
            IsTransmitting = true;
            lock (thread_send_lock)
            {
                Thread.SpinWait(600);
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
                    Thread.SpinWait(100);
                    IsTransmitting = false;
                    return stsResult == TPCANStatus.PCAN_ERROR_OK ? PCANStatus.OK : PCANStatus.Error;
                }
                catch (Exception)
                {
                    Thread.SpinWait(6000);
                    IsTransmitting = false;
                    return PCANStatus.Error;
                }
            }
        }

        public PCANStatus SendFrameFD(int canID, int DLC, byte[] data)
        {
            IsTransmitting = true;
            lock (thread_send_lock)
            {
                Thread.SpinWait(6000);
                TPCANStatus stsResult = TPCANStatus.PCAN_ERROR_UNKNOWN;
                try
                {
                    byte[] tmp = new byte[64];
                    for (int i = 0; i < data.Length; i++)
		    {
                        tmp[i] = data[i];
		    }
		    
                    _CANMsgFD.ID = (uint)canID;

                    if(((byte)DLC) <= 8)
                    {
                        _CANMsgFD.DLC = (byte)DLC;
                    }
                    else if (((byte)DLC) <= 12)
                    {
                        _CANMsgFD.DLC = 9;
                    }
                    else if (((byte)DLC) <= 16)
                    {
                        _CANMsgFD.DLC = (byte)10;
                    }
                    else if (((byte)DLC) <= 20)
                    {
                        _CANMsgFD.DLC = (byte)11;
                    }
                    else if (((byte)DLC) <= 24)
                    {
                        _CANMsgFD.DLC = (byte)12;
                    }
                    else if (((byte)DLC) <= 32)
                    {
                        _CANMsgFD.DLC = (byte)13;
                    }
                    else if (((byte)DLC) <= 48)
                    {
                        _CANMsgFD.DLC = (byte)14;
                    }
                    else if (((byte)DLC) <= 64)
                    {
                        _CANMsgFD.DLC = (byte)15;
                    }
                  
                    _CANMsgFD.DATA = tmp;
                    _CANMsgFD.MSGTYPE = TPCANMessageType.PCAN_MESSAGE_FD | TPCANMessageType.PCAN_MESSAGE_STANDARD;
                    lock (thread_lock)
                    {
                        stsResult = PCANBasic.WriteFD(device, ref _CANMsgFD);
                    }
                    Thread.SpinWait(100);
                    IsTransmitting = false;
                    return stsResult == TPCANStatus.PCAN_ERROR_OK ? PCANStatus.OK : PCANStatus.Error;
                }
                catch (Exception)
                {
                    Thread.SpinWait(6000);
                    IsTransmitting = false;
                    return PCANStatus.Error;
                }
            }
        }

        public PCANStatus SendFrameFDExt(int canID, int DLC, byte[] data)
        {
            IsTransmitting = true;
            lock (thread_send_lock)
            {
                Thread.SpinWait(6000);
                TPCANStatus stsResult = TPCANStatus.PCAN_ERROR_UNKNOWN;
                try
                {
                    byte[] tmp = new byte[64];
                    for (int i = 0; i < data.Length; i++)
		    {
                        tmp[i] = data[i];
		    }
		    
                    _CANMsgFD.ID = (uint)canID;

                    if (((byte)DLC) <= 8)
                    {
                        _CANMsgFD.DLC = (byte)DLC;
                    }
                    else if (((byte)DLC) <= 12)
                    {
                        _CANMsgFD.DLC = 9;
                    }
                    else if (((byte)DLC) <= 16)
                    {
                        _CANMsgFD.DLC = (byte)10;
                    }
                    else if (((byte)DLC) <= 20)
                    {
                        _CANMsgFD.DLC = (byte)11;
                    }
                    else if (((byte)DLC) <= 24)
                    {
                        _CANMsgFD.DLC = (byte)12;
                    }
                    else if (((byte)DLC) <= 32)
                    {
                        _CANMsgFD.DLC = (byte)13;
                    }
                    else if (((byte)DLC) <= 48)
                    {
                        _CANMsgFD.DLC = (byte)14;
                    }
                    else if (((byte)DLC) <= 64)
                    {
                        _CANMsgFD.DLC = (byte)15;
                    }

                    _CANMsgFD.DATA = tmp;
                    _CANMsgFD.MSGTYPE = TPCANMessageType.PCAN_MESSAGE_FD | TPCANMessageType.PCAN_MESSAGE_EXTENDED;
                    lock (thread_lock)
                    {
                        stsResult = PCANBasic.WriteFD(device, ref _CANMsgFD);
                    }
                    Thread.SpinWait(100);
                    IsTransmitting = false;
                    return stsResult == TPCANStatus.PCAN_ERROR_OK ? PCANStatus.OK : PCANStatus.Error;
                }
                catch (Exception)
                {
                    Thread.SpinWait(6000);
                    IsTransmitting = false;
                    return PCANStatus.Error;
                }
            }
        }

        public PCANStatus SendFrameExt(int canID, int DLC, byte[] data)
        {
            IsTransmitting = true;
            lock (thread_send_lock)
            {
                Thread.SpinWait(6000);
                TPCANStatus stsResult = TPCANStatus.PCAN_ERROR_UNKNOWN;
                try
                {
                    byte[] tmp = new byte[8];
                    for (int i = 0; i < data.Length; i++)
		    {
                        tmp[i] = data[i];
		    }
		    
                    _CANMsgExt.ID = (uint)canID;
                    _CANMsgExt.LEN = (byte)DLC;
                    _CANMsgExt.DATA = tmp;

                    lock (thread_lock)
                    {
                        stsResult = PCANBasic.Write(device, ref _CANMsgExt);
                    }
                    Thread.SpinWait(100);
                    IsTransmitting = false;
                    return stsResult == TPCANStatus.PCAN_ERROR_OK ? PCANStatus.OK : PCANStatus.Error;
                }
                catch (Exception)
                {
                    Thread.SpinWait(6000);
                    IsTransmitting = false;
                    return PCANStatus.Error;
                }
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

        public PCANStatus RetrieveFrameFD(out TPCANMsgFD CANMsg, out ulong CANTimeStamp)
        {
            TPCANStatus stsResult = TPCANStatus.PCAN_ERROR_UNKNOWN;

            CANMsg = new TPCANMsgFD();

            try
            {
                lock (thread_lock)
                {
                    stsResult = PCANBasic.ReadFD(device, out CANMsg, out CANTimeStamp);
                }

                return CANMsg.MSGTYPE == TPCANMessageType.PCAN_MESSAGE_STATUS ? PCANStatus.Error : (stsResult != TPCANStatus.PCAN_ERROR_QRCVEMPTY ? PCANStatus.OK : PCANStatus.Error);
            }
            catch (Exception)
            {
                CANMsg = new TPCANMsgFD();
                CANTimeStamp = 0;
                return PCANStatus.Error;
            }
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
		{
                    if (receiveCallbacks[i] == callback)
                    {
                        index_found = i;
                        break;
                    }
		}
                if (index_found != -1)
		{
                    receiveCallbacks.RemoveAt(index_found);
		}
            }
        }

        public void ActivateAutoReceive()
        {
            if (isFDEnabled == false)
            {
                InstanceCaller = new Thread(new ThreadStart(AutoReceive));
            }
            else
            {
                InstanceCaller = new Thread(new ThreadStart(AutoReceiveFD));
            }
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
			    {
                                if (receiveCallbacks[i](new object[] { msg, timestamp }) != 0x00)
				{
					toRemoveCallbacks.Add(receiveCallbacks[i]);
				}
			    }
                            if (toRemoveCallbacks.Count != 0)
                            {
                                for (int i = (toRemoveCallbacks.Count - 1); i >= 0; i--)
				{
					receiveCallbacks.Remove(toRemoveCallbacks[i]);
				}
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

        private void AutoReceiveFD()
        {
            TPCANMsgFD msg;
            ulong timestamp;
            List<PCANCallback> toRemoveCallbacks = new List<PCANCallback>();
            bool isreceived = false;
            while (autoReceiveStatus)
            {
                while (RetrieveFrameFD(out msg, out timestamp) == PCANStatus.OK)
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
			    {
                                if (receiveCallbacks[i](new object[] { msg, timestamp }) != 0x00)
				{    
					toRemoveCallbacks.Add(receiveCallbacks[i]);
				}
			    }
                            if (toRemoveCallbacks.Count != 0)
                            {
                                for (int i = (toRemoveCallbacks.Count - 1); i >= 0; i--)
                                {
					receiveCallbacks.Remove(toRemoveCallbacks[i]);
				}
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
