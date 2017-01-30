﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SilkroadSecurityApi;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SilkroadInformationAPI.Client.Network
{
    public class ClientlessConnection
    {
        public static event Action OnDisconnect;

        private Security cl_security;
        private TransferBuffer cl_recv_buffer;
        private List<Packet> cl_packets;
        private Socket cl_socket;

        private PingStatus ServerStatus;
        private Thread PingThread;

        public uint cl_SessionID;
        public string cl_Username;
        public string cl_Password;
        public byte cl_Locale;
        public uint cl_GameVersion;

        public ClientlessConnection()
        {
            PingThread = new Thread(KeepaliveThread);
        }

        public void Start(string IP, ushort Port)
        {
            cl_security = new Security();
            cl_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            cl_recv_buffer = new TransferBuffer(8192, 0, 0);
            cl_packets = new List<Packet>();

            SroClient.RemoteSecurity = cl_security;

            cl_socket.Connect(IP, Port);
            cl_socket.NoDelay = true;
            cl_socket.Blocking = false;

            var thread = new Thread(ClientlessThread);
            thread.Start();

            if (!PingThread.IsAlive)
                PingThread.Start();
            
        }

        /// <summary>
        /// Call this twice, once before starting the connection. And the other when you receive the session id succesfully
        /// <para>Because they are used to instaniate the agent server.</para>
        /// </summary>
        /// <param name="_SessionID">Received from Packets.Gateway.LoginResponse, YOU CAN LEAVE IT 0 AT FIRST TIME.</param>
        /// <param name="_Username">User input</param>
        /// <param name="_Password">User input</param>
        /// <param name="_Locale">Media.Data.ServerInfo.Locale</param>
        /// <param name="_GameVersion">Media.Data.ServerInfo.Version</param>
        public void ConfigureSettings(uint _SessionID, string _Username, string _Password, byte _Locale, uint _GameVersion)
        {
            cl_SessionID = _SessionID;
            cl_Username = _Username;
            cl_Password = _Password;
            cl_Locale = _Locale;
            cl_GameVersion = _GameVersion;
        }

        private void ClientlessThread()
        {
            try
            {
                while (true)
                {
                    SocketError success;
                    Packet current;
                    cl_recv_buffer.Size = cl_socket.Receive(cl_recv_buffer.Buffer, 0, cl_recv_buffer.Buffer.Length, SocketFlags.None, out success);

                    if (success != SocketError.Success)
                    {
                        if (success != SocketError.WouldBlock)
                        {
                            Console.WriteLine("Disconnected!");
                            OnDisconnect?.Invoke();
                            return;
                        }
                    }
                    else if (cl_recv_buffer.Size > 0)
                    {
                        cl_security.Recv(cl_recv_buffer);
                    }
                    else
                    {
                        Console.WriteLine("Disconnected!!");
                        OnDisconnect?.Invoke();
                        return;
                    }

                    #region TransferIncoming
                    List<Packet> tmp = cl_security.TransferIncoming();
                    if (tmp != null)
                    {
                        cl_packets.AddRange(tmp);
                    }

                    if (cl_packets.Count > 0)
                    {
                        using (List<Packet>.Enumerator enumerator = cl_packets.GetEnumerator())
                        {
                            while (enumerator.MoveNext())
                            {
                                current = enumerator.Current;
                                Dispatcher.Process(current);

                                Console.WriteLine(current.Opcode.ToString("X4"));

                                if (current.Opcode == 0x2001)
                                {
                                    string module = current.ReadAscii();
                                    if (module == "GatewayServer")
                                    {
                                        ServerStatus = PingStatus.Send; //Sets the ServerStatus to Gateway so the ping works correctly!
                                        Packet p = new Packet(0x6100, true, false); //Patch request
                                        p.WriteInt8(cl_Locale);
                                        p.WriteAscii("SR_Client"); //Service name
                                        p.WriteInt32(cl_GameVersion);
                                        cl_security.Send(p);
                                    }
                                    else if (module == "AgentServer")
                                    {
                                        ServerStatus = PingStatus.Send;
                                        Packet p = new Packet(0x6103, true, false); //Login packet
                                        p.WriteUInt32(cl_SessionID); //Session ID we got from A102 Answer
                                        p.WriteAscii(cl_Username);
                                        p.WriteAscii(cl_Password);
                                        p.WriteUInt8(cl_Locale);
                                        p.WriteUInt32(0); //Mac address
                                        p.WriteUInt16(0); //Mac address
                                        cl_security.Send(p);
                                    }
                                }

                                if (current.Opcode == 0xA102)
                                {
                                    Console.WriteLine("Starting new connection to AgentServer!");
                                    return;
                                }
                            }
                        }
                        cl_packets.Clear();
                    }
                    #endregion

                    #region TransferOutgoing
                    List<KeyValuePair<TransferBuffer, Packet>> tmp2 = cl_security.TransferOutgoing();
                    if (tmp2 != null)
                    {
                        foreach (KeyValuePair<TransferBuffer, Packet> pair in tmp2)
                        {
                            TransferBuffer key = pair.Key;
                            success = SocketError.Success;
                            while (key.Offset != key.Size)
                            {
                                int num = cl_socket.Send(key.Buffer, key.Offset, key.Size - key.Offset, SocketFlags.None, out success);
                                //if ((success != SocketError.Success) && (success != SocketError.WouldBlock))
                                //{
                                //    break;
                                //}
                                key.Offset += num;
                                Thread.Sleep(1);
                            }
                            //if (success != SocketError.Success)
                            //{
                            //    break;
                            //}
                        }
                    }
                    #endregion
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                OnDisconnect?.Invoke();
            }
        }

        private void KeepaliveThread()
        {
            while (true)
            {
                if (ServerStatus == PingStatus.Send)
                {
                    SroClient.RemoteSecurity.Send(new Packet(0x2002));
                }
                Thread.Sleep(5000);
            }
        }

        public void TerminateConnection()
        {
            try
            {
                cl_socket.Close();
            }
            catch { }

            try
            {
                ServerStatus = PingStatus.None;
                PingThread.Abort();
            }
            catch { }
        }

        enum PingStatus
        {
            None,
            Send,
        }
    }
}
