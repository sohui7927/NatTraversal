using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Transactions;

namespace NatTraversal {
    internal static class Program {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());


        }

    }

    internal class WanUDPCom {
        UdpClient udp;
        IPEndPoint stunep = new IPEndPoint(Dns.GetHostAddresses("stun.l.google.com", AddressFamily.InterNetwork)[0], 19302);
        Task<UdpReceiveResult>? stunRevTask = null;
        CancellationTokenSource stunCts = new CancellationTokenSource();
        System.Timers.Timer stunRevCancelTimer;
        List<byte[]> pendingStunTransactionList;
        IPEndPoint? sendEp;
        bool revFlag=false;
        public Action<string> revMessageCallback;
        public Action relayStopCallback;
        TCPRelayCom? tcpRelayCom = null;
        SequenceControl? seqControl = null;
        System.Timers.Timer keepAliveTimer;
        int lastSendTick;
        const int mtu = 1400;

        public WanUDPCom() {
            udp = new UdpClient();
            udp.DontFragment = false;
            pendingStunTransactionList = new List<byte[]>(10);
            revMessageCallback = (s) => { };
            relayStopCallback = () => { };
            stunRevCancelTimer = new System.Timers.Timer();
            stunRevCancelTimer.AutoReset = false;
            stunRevCancelTimer.Interval = 5000;//timeout = 5s
            stunRevCancelTimer.Elapsed += (object? sender, System.Timers.ElapsedEventArgs e) => {
                stunCts.Cancel();
                stunCts.Dispose();
            };
            keepAliveTimer = new System.Timers.Timer();
            keepAliveTimer.AutoReset = true;
            keepAliveTimer.Interval = 30000;//keep alive interval = 30s
            keepAliveTimer.Elapsed += (object? sender, System.Timers.ElapsedEventArgs e) => {
                if (Environment.TickCount - lastSendTick > 30000) {
                    SendPacket(DataType.KeepAlive, 0, (s) => { });
                    lastSendTick = Environment.TickCount;
                }
            };
        }
        public void SendStunRequest(Action<IPEndPoint> callback) {
            byte[] data = {
                0x00, 0x01, 0x00, 0x00, // 0b00 | STUN Message Type | Message Length
                0x21, 0x12, 0xa4, 0x42, // Magic Cookie
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Transaction ID
            };
            byte[] transactionID = new byte[12];
            using(RandomNumberGenerator rand = RandomNumberGenerator.Create()) {
                rand.GetBytes(transactionID);
            }
            pendingStunTransactionList.Add(transactionID);
            for(int i=0;i<transactionID.Length;i++) {
                data[i + 8] = transactionID[i];
            }
            udp.SendAsync(data, data.Length, stunep);

            if (stunRevTask != null && !stunRevTask.IsCompleted) {
                stunRevCancelTimer.Stop();
                stunRevCancelTimer.Start();
            } else {
                stunCts = new CancellationTokenSource();
                stunRevTask = udp.ReceiveAsync(stunCts.Token).AsTask();
                stunRevTask.ContinueWith((task) => {
                    stunRevCancelTimer.Stop();
                    byte[] revdata = task.Result.Buffer;
                    if (revdata.Length < 20)
                        throw new Exception("too short STUN message");
                    uint address;
                    ushort port;
                    Span<byte> span = revdata.AsSpan();
                    ref StunHeader stunHeader = ref MemoryMarshal.Cast<byte, StunHeader>(span)[0];
                    span = span.Slice(Marshal.SizeOf<StunHeader>());
                    if(stunHeader.MagicCookie!= 0x2112A442 || !stunHeader.isSameTransaction(transactionID)) {
                        Console.Error.WriteLine("Receive unexpected packet:" + BitConverter.ToString(revdata));
                        Debug.WriteLine("Receive unexpected packet:" + BitConverter.ToString(revdata));
                    }
                    if (stunHeader.Class == StunClass.ERROR_RESPONSE) {
                        throw new Exception("Receive STUN error response");
                    } else if (stunHeader.Class != StunClass.SUCCESS_RESPONSE || stunHeader.Method != StunMethod.BINDING) {
                        Debug.WriteLine("Unexpected Response; Class:" + stunHeader.Class + " Method:" + stunHeader.Method);
                    }
                    if (stunHeader.Length > span.Length) {
                        throw new Exception("STUN length is bigger than the length of received packed");
                    } else if (span.Length < Marshal.SizeOf<StunAttributeHeader>()) {
                        throw new Exception("Too short length to read STUN attribute header");
                    }
                    ref StunAttributeHeader attributeHeader = ref MemoryMarshal.Cast<byte, StunAttributeHeader>(span)[0];
                    span = span.Slice(Marshal.SizeOf<StunAttributeHeader>());
                    switch (attributeHeader.Type) {
                        case StunAttrType.MAPPED_ADDRESS:
                            if (span.Length < Marshal.SizeOf<StunAttr.MappedAddress>())
                                throw new Exception("Too short length to read");
                            ref StunAttr.MappedAddress mappedAddress = ref MemoryMarshal.Cast<byte, StunAttr.MappedAddress>(span)[0];
                            address = mappedAddress.AddressIPv4;
                            port = mappedAddress.Port;
                            break;
                        case StunAttrType.XOR_MAPPED_ADDRESS:
                            if (span.Length < Marshal.SizeOf<StunAttr.XorMappedAddress>())
                                throw new Exception("Too short length to read");
                            ref StunAttr.XorMappedAddress xorMappedAddress = ref MemoryMarshal.Cast<byte, StunAttr.XorMappedAddress>(span)[0];
                            address = xorMappedAddress.Address;
                            port = xorMappedAddress.Port;
                            break;
                        default:
                            throw new Exception("Unexpected STUN attribute type");
                    }
                    callback(new IPEndPoint(BinaryPrimitives.ReverseEndianness(address), port));
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
                stunRevCancelTimer.Start();
            }

        }
        public void StartReceive() {
            if(revFlag) 
                return;
            revFlag = true;
            Task task = new Task(() => {
                Span<byte> revdata=null;
                Span<byte> buffer = new byte[1500];
                int index=0;
                while (revFlag) {
                    while(index < Marshal.SizeOf<PacketHeader>()) {
                        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                        revdata = udp.Receive(ref ep);
                        if (sendEp != ep) {
                            Debug.WriteLine("receive from unexpected endpoint:" + ep.ToString());
                        }
                        //Debug.WriteLine("receive data:"+revdata.Length);
                        if (index+revdata.Length > Marshal.SizeOf<PacketHeader>()) {
                            revdata[..(Marshal.SizeOf<PacketHeader>() - index)].CopyTo(buffer[index..]);
                            revdata = revdata[(Marshal.SizeOf<PacketHeader>() - index)..];
                            index = Marshal.SizeOf<PacketHeader>();
                        } else {
                            revdata.CopyTo(buffer[index..]);
                            index += revdata.Length;
                            revdata = null;
                        }
                    }
                    index = 0;
                    PacketHeader header = MemoryMarshal.Cast<byte, PacketHeader>(buffer)[0];//no ref
                    byte[] buf = new byte[header.length];//XXX: while‚ÌŠO‚Éo‚·
                    int i = 0;
                    if (revdata != null) {
                        if (revdata.Length > buf.Length) {
                            revdata[..buf.Length].CopyTo(buf);
                            revdata[buf.Length..revdata.Length].CopyTo(buffer);
                            index = revdata.Length - buf.Length;
                            i = buf.Length;
                        } else {
                            revdata.CopyTo(buf);
                            i = revdata.Length;
                        }
                    }
                    for (; i < buf.Length; i += revdata.Length) {
                        revdata = udp.Receive(ref sendEp);
                        if (revdata.Length > buf.Length - i) {
                            revdata[..(buf.Length - i)].CopyTo(buf.AsSpan(i));
                            revdata[(buf.Length - i)..revdata.Length].CopyTo(buffer);
                            index = revdata.Length - (buf.Length - i);
                        } else {
                            revdata.CopyTo(buf.AsSpan(i));
                        }
                    }
                    Debug.WriteLine("receive packet :"+(Marshal.SizeOf<PacketHeader>()+buf.Length));
                    switch (header.type) {
                        case DataType.Message:
                            revMessageCallback.Invoke(Encoding.UTF8.GetString(buf));
                            break;
                        case DataType.Data:
                            //tcpRelayCom?.relayReceive(buf);
                            Span<byte> span= null;
                            seqControl?.readRevData(buf, out span);
                            if (span != null)
                                tcpRelayCom?.relayReceive(span);
                            break;
                        case DataType.KeepAlive:
                            break;
                        case DataType.Disconnect:
                            this.StopRelay();
                            relayStopCallback();
                            break;
                        default:
                            throw new Exception("Unexpected packet type");
                    }
                }
            });
            task.ContinueWith(t => {
                Debug.WriteLine(t.Exception);
            }, TaskContinuationOptions.OnlyOnFaulted);
            task.Start();
            keepAliveTimer.Start();
        }
        public void StopReceive() {
            revFlag = false;
            keepAliveTimer.Stop();
        }
        public IPEndPoint SendEp {
            set { sendEp = value; }
        }
        enum DataType : byte {
            Message=1,
            Data=2,
            KeepAlive=3,
            Disconnect=4,
        }
        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        struct PacketHeader {
            [FieldOffset(0)]
            public DataType type;
            [FieldOffset(1)]
            public uint length;
        }
        public delegate void SetData(Span<byte> span);
        void SendPacket(DataType type, uint size, SetData action) {
            if(sendEp == null) {
                throw new Exception("sendEp must be set before calling");
            }
            byte[] data = new byte[size+Marshal.SizeOf<PacketHeader>()];
            if (data.Length > mtu) {
                Debug.WriteLine("Too large size to send");
            }
            ref PacketHeader header = ref MemoryMarshal.Cast<byte, PacketHeader>(data)[0];
            header.type = type;
            header.length = size;
            action.Invoke(data.AsSpan(Marshal.SizeOf<PacketHeader>()));
            lock (udp) {
                udp.Send(data, sendEp);
                //Task.Delay(10).Wait();
            }
            lastSendTick = Environment.TickCount;
            Debug.WriteLine("Send data(length:"+ data.Length+") to "+sendEp.ToString());
        }
        void SendPacket(DataType type, uint headerSize, SetData headerSetAction, Span<byte> payload) {
            if (sendEp == null) {
                throw new Exception("sendEp must be set before calling");
            }
            byte[] data = new byte[headerSize + payload.Length + Marshal.SizeOf<PacketHeader>()];
            if (data.Length > mtu) {
                Debug.WriteLine("Too large size to send");
            }
            ref PacketHeader header = ref MemoryMarshal.Cast<byte, PacketHeader>(data)[0];
            header.type = type;
            header.length = headerSize + (uint)payload.Length;
            Span<byte> span = data.AsSpan(Marshal.SizeOf<PacketHeader>());
            headerSetAction.Invoke(span);
            payload.CopyTo(span.Slice((int)headerSize));
            lock (udp) {
                udp.Send(data, sendEp);
                //Task.Delay(10).Wait();
            }
            lastSendTick = Environment.TickCount;
            Debug.WriteLine("Send data(length:" + data.Length + ") to " + sendEp.ToString());
        }
        public void SendMessage(string text) {
            SendPacket(DataType.Message, (uint)Encoding.UTF8.GetByteCount(text), (span) => {
                Encoding.UTF8.GetBytes(text, span);
            });
        }
        public void SendData(uint size, SetData action) {
            SendPacket(DataType.Data, size, action);
        }
        public void SendData(uint headerSize, SetData headerSetAction, Span<byte> payload) {
            SendPacket(DataType.Data, headerSize, headerSetAction, payload);
        }
        public void RelaySend(byte[] data, uint bytes) {
            //SendPacket(DataType.Data, bytes, (span) => {
            //    Span<byte> s = data;
            //    s.Slice(0, (int)bytes).CopyTo(span);
            //});
            seqControl?.writeSendData(new Span<byte>(data, 0, (int)bytes));
        }
        public void StartRelay(bool isServer, int port) {
            seqControl = new SequenceControl(this);
            Action dissconnectCallback = () => {
                this.StopRelay();
                SendPacket(DataType.Disconnect, 0, (span) => { });
                relayStopCallback();
            };
            if (isServer) {
                tcpRelayCom = new TCPClientCom(port, dissconnectCallback);
            } else {
                tcpRelayCom = new TCPServerCom(port, dissconnectCallback);
            }
            tcpRelayCom.StartRelay(this);
        }
        public void StopRelay() {
            tcpRelayCom?.StopRelay();
            tcpRelayCom = null;
            seqControl?.StopSend();
            seqControl = null;
        }
    }

    internal abstract class TCPRelayCom {
        NetworkStream? stream = null;
        CancellationTokenSource? cts = null;
        Task? relaySendTask = null;
        Action disconnectCallback;

        public TCPRelayCom(Action dissconnectCallback) {
            this.disconnectCallback = dissconnectCallback;
        }
        abstract protected Task<NetworkStream> Connect(CancellationToken ct);
        public void StartRelay(WanUDPCom udpCom) {
            cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            Task<NetworkStream> task = Connect(ct);
            relaySendTask = task.ContinueWith((t) => {
                stream = t.Result;
                byte[] buf = new byte[1380];
                int readByte;
                while (!ct.IsCancellationRequested) {
                    try {
                        readByte = stream.Read(buf, 0, buf.Length);
                    }catch(IOException e) {
                        if(e.InnerException is SocketException) {
                            break;
                        }
                        throw e;
                    }
                    if (readByte != 0) {
                        udpCom.RelaySend(buf, (uint)readByte);
                    } else {
                        disconnectCallback();
                    }
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
            task.ContinueWith((t) => {
                Debug.WriteLine(t.Exception);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        public void relayReceive(Span<byte> span) {
            if (stream!=null && stream.CanWrite) {
                stream?.Write(span);
            }
        }
        public virtual void StopRelay() {
            lock (this) {
                cts?.Cancel();
                cts?.Dispose();
                cts = null;
                relaySendTask?.ContinueWith((t) => {
                    relaySendTask = null;
                    stream?.Dispose();
                    stream = null;
                });
            }
        }
    }
    internal class TCPClientCom : TCPRelayCom {
        TcpClient tcpClient;
        IPEndPoint ipep;
        public TCPClientCom(int port, Action dissconnectCallback):base(dissconnectCallback) {
            tcpClient = new TcpClient();
            tcpClient.NoDelay = true;
            ipep = new IPEndPoint(IPAddress.Loopback, port);
        }

        protected override Task<NetworkStream> Connect(CancellationToken ct) {
            return tcpClient.ConnectAsync(ipep, ct).AsTask().ContinueWith((t) => tcpClient.GetStream(), TaskContinuationOptions.OnlyOnRanToCompletion);
        }
        public override void StopRelay() {
            base.StopRelay();
            tcpClient.Dispose();
        }
    }
    internal class TCPServerCom : TCPRelayCom {
        TcpListener tcpListener;
        TcpClient? client = null;

        public TCPServerCom(int port, Action dissconnectCallback):base(dissconnectCallback) {
            tcpListener = new TcpListener(IPAddress.Any, port);
        }
        protected override Task<NetworkStream> Connect(CancellationToken ct) {
            tcpListener.Start();
            return tcpListener.AcceptTcpClientAsync(ct).AsTask().ContinueWith((t) => {
                client = t.Result;
                client.NoDelay = true;
                return client.GetStream();
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
        public override void StopRelay() {
            base.StopRelay();
            client?.Dispose();
            tcpListener.Stop();
        }
    }

    
}