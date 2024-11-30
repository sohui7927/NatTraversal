using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NatTraversal {
    internal class SequenceControl {
        uint seqNum = 0;
        uint ackNum = 0;
        uint revAckNum = 0;
        int duplicateAckCount = 0;
        SendFIFOBuffer buf = new SendFIFOBuffer();
        WanUDPCom udpCom;
        Queue<RetransmissionTimer> timerQueue = new Queue<RetransmissionTimer>();
        readonly ushort mtu = (ushort)(1395 - Marshal.SizeOf<SequenceControlHeader>());
        readonly ushort sendWindowSize = 16384;
        ManualResetEvent eventForSendWindow = new ManualResetEvent(true);

        public SequenceControl(WanUDPCom com) { 
            udpCom = com;
        }

        void retransmission(uint retransSeqNum) {
            lock (this) {
                if(revAckNum - retransSeqNum < ushort.MaxValue) {//revAckNum >= retransSeqNum
                    retransSeqNum = revAckNum;
                }
                if (!(retransSeqNum-seqNum < ushort.MaxValue)) {//seqNum>retransSeqNum
                    ushort length = (ushort)(seqNum - retransSeqNum);
                    lock (buf) {
                        ushort index = 0;
                        if(length > mtu) {
                            do {
                                udpCom.SendData((uint)(mtu + Marshal.SizeOf<SequenceControlHeader>()), (span) => {
                                    ref SequenceControlHeader header = ref MemoryMarshal.Cast<byte, SequenceControlHeader>(span)[0];
                                    header.seqNum = retransSeqNum + index;
                                    header.ackNum = ackNum;
                                    buf.read(index, mtu, span.Slice(Marshal.SizeOf<SequenceControlHeader>()));
                                });
                                index += mtu;
                            } while (length-index > mtu);
                        }
                        udpCom.SendData((uint)(length - index + Marshal.SizeOf<SequenceControlHeader>()), (span) => {
                            ref SequenceControlHeader header = ref MemoryMarshal.Cast<byte, SequenceControlHeader>(span)[0];
                            header.seqNum = retransSeqNum + index;
                            header.ackNum = ackNum;
                            buf.read(index, (ushort)(length-index), span.Slice(Marshal.SizeOf<SequenceControlHeader>()));
                        });
                    }
                    //Debug.WriteLine("call buf read. bufLen:" + buf.Length + " pendLen:" + (ushort)(seqNum - revAckNum));
                    Debug.WriteLine("resend seqNum:" + retransSeqNum + " ackNum:" + ackNum + " length:" + length);
                }
            }
        }
        public void writeSendData(Span<byte> data) {
            while(buf.Length > sendWindowSize) {
                eventForSendWindow.Reset();
                eventForSendWindow.WaitOne();
            }
            lock (this) {
                if (data.Length > mtu) {
                    do {
                        writeSendData(data.Slice(0, mtu));
                        data = data.Slice(mtu);
                    } while (data.Length > mtu);
                }
                lock (buf) {
                    buf.write(data);
                }
                udpCom.SendData((uint)Marshal.SizeOf<SequenceControlHeader>(), (span) => {
                    ref SequenceControlHeader header = ref MemoryMarshal.Cast<byte, SequenceControlHeader>(span)[0];
                    header.seqNum = seqNum;
                    header.ackNum = ackNum;
                }, data);
                Debug.WriteLine("send seqNum:" + seqNum + " ackNum:" + ackNum + " length:" + data.Length);
                RetransmissionTimer rt = new RetransmissionTimer(seqNum, (uint)data.Length, (uint startSeqNum) => {
                    Debug.WriteLine("timeout retransmission");
                    retransmission(startSeqNum);
                });
                lock (timerQueue) {
                    if (timerQueue.Count == 0) {
                        rt.Start();
                    }
                    timerQueue.Enqueue(rt);
                }
                seqNum += (uint)data.Length;
            }
            //Debug.WriteLine("call buf write. bufLen:" + buf.Length + " pendLen:" + (ushort)(seqNum - revAckNum));
        }
        void sendAck() {
            udpCom.SendData((uint)Marshal.SizeOf<SequenceControlHeader>(), (span) => {
                ref SequenceControlHeader header = ref MemoryMarshal.Cast<byte, SequenceControlHeader>(span)[0];
                header.seqNum = seqNum;
                header.ackNum = ackNum;
            });
        }
        public void readRevData(Span<byte> span, out Span<byte> data) {
            lock (this) {
                ref SequenceControlHeader header = ref MemoryMarshal.Cast<byte, SequenceControlHeader>(span)[0];
                span = span.Slice(Marshal.SizeOf<SequenceControlHeader>());
                Debug.WriteLine("rev seqNum:" + header.seqNum + " ackNum" + header.ackNum);
                if (!(revAckNum - header.ackNum < ushort.MaxValue)) {//header.ackNum > revAckNum
                    lock (buf) {
                        buf.pop((ushort)(header.ackNum - revAckNum));
                    }
                    lock (timerQueue) {
                        while (timerQueue.Count > 0) {
                            RetransmissionTimer rt = timerQueue.Peek();
                            if (rt.startSeqNum + rt.size <= header.ackNum) {
                                timerQueue.Dequeue().Stop();
                            } else {
                                rt.Start();
                                break;
                            }
                        }
                    }
                    eventForSendWindow.Set();
                    revAckNum = header.ackNum;
                    duplicateAckCount = 0;
                } else if (header.ackNum == revAckNum) {
                    if (++duplicateAckCount >= 3 && span.Length==0 && !(header.ackNum - seqNum < ushort.MaxValue)) {//seqNum > header.ackNum
                        uint an = header.ackNum;
                        Debug.WriteLine("dupAck retransmission");
                        Task.Run(() => retransmission(an));
                    }
                }
                if (span.Length > 0) {
                    if (!(header.seqNum - ackNum < ushort.MaxValue)) {//ackNum > header.seqNum
                        if (!(ackNum - (header.seqNum + (uint)span.Length) < ushort.MaxValue)) {//header.seqNum+(uint)span.Length > ackNum
                            data = span.Slice((int)(ackNum - header.seqNum));
                            ackNum = header.seqNum + (uint)span.Length;
                        } else {
                            data = null;
                        }
                    } else if (ackNum == header.seqNum) {
                        data = span;
                        ackNum = header.seqNum + (uint)span.Length;
                    } else {//ackNum < header.seqNum
                        data = null;
                        sendAck();
                        sendAck();
                    }
                    sendAck();
                    duplicateAckCount = 0;
                } else {
                    data = null;
                }
            }
            //Debug.WriteLine("call buf pop. bufLen:" + buf.Length + " pendLen:" + (ushort)(seqNum - revAckNum));
        }
        public void StopSend() {
            lock (timerQueue) {
                while (timerQueue.Count > 0) {
                    timerQueue.Dequeue().Stop();
                }
            }
        }
    }
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    internal struct SequenceControlHeader {
        [FieldOffset(0)]
        public uint seqNum;
        [FieldOffset(4)]
        public uint ackNum;
    }
    internal class SendFIFOBuffer {
        byte[] buffer = new byte[65536];//実際に書き込めるのは最大65535 B
        ushort start=0;
        ushort end=0;
        ManualResetEvent mre = new ManualResetEvent(false);

        public ushort Length {
            get { return (ushort)(end - start); }
        }
        public void write(ReadOnlySpan<byte> span) {
            if(span.Length > 65535) {
                throw new ArgumentException("Too large size to write:" + span.Length);
            }
            while((ushort)(start-end-1)<span.Length) {
                mre.Reset();
                mre.WaitOne();
            }
            if (65536 - end >= span.Length) {
                span.CopyTo(new Span<byte>(buffer).Slice(end));
            } else {
                Span<byte> s = new Span<byte> (buffer);
                span.Slice(0, 65536-end).CopyTo(s.Slice(end));
                span.Slice(65536 - end).CopyTo(s);
            }
            end = (ushort)(end + span.Length);
            //Debug.WriteLine("buf write:" + span.Length + " after size:" + this.Length);
        }
        public void read(ushort index, ushort length, Span<byte> span) {
            if((ushort)(end-start)<index+length) {
                throw new IndexOutOfRangeException("Buffer size:"+(ushort)(end-start)+" index:"+index+" length:"+length);
            }
            if ((int)start + (int)index + (int)length <= 65536) {
                new Span<byte>(buffer, (ushort)(start + index), length).CopyTo(span);
            } else if ((int)start + (int)index < 65536) {
                Span<byte> s = new Span<byte>(buffer);
                s.Slice(start+index).CopyTo(span);
                s.Slice(0, length-(65536-(start+index))).CopyTo(span.Slice(65536-(start+index)));
            } else {
                new Span<byte>(buffer, (ushort)(start+index), length).CopyTo(span);
            }
            //Debug.WriteLine("buf read:" + length + " after size:" + this.Length);
        }
        public void pop(ushort length) {
            if((ushort)(end-start)<length) {
                throw new ArgumentOutOfRangeException("Size to read ("+length+"B) is larger than stored size ("+(ushort)(end-start)+"B)");
            }
            start += length;
            mre.Set();
            //Debug.WriteLine("buf pop:" + length + " after size:" + this.Length);
        }
    }
    internal class RetransmissionTimer {
        System.Timers.Timer timer;
        public uint startSeqNum;
        public uint size;
        public RetransmissionTimer(uint startSeqNum, uint size, Action<uint> action) {
            timer = new System.Timers.Timer();
            timer.AutoReset = true;
            timer.Interval = 5000;//timeout = 5s
            this.startSeqNum = startSeqNum;
            this.size = size;
            timer.Elapsed += (object? sender, System.Timers.ElapsedEventArgs e) => {
                action(this.startSeqNum);
            };
        }
        public void Start() {
            timer.Start();
        }
        public void Stop() {
            timer.Stop(); 
            timer.Dispose();
        }
    }
}
