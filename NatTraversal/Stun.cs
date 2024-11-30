using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NatTraversal {
    internal enum StunClass : ushort {
        REQUEST = 0,
        INDICATION = 1,
        SUCCESS_RESPONSE = 2,
        ERROR_RESPONSE = 3
    }
    internal enum StunMethod : ushort {
        BINDING = 0x001,
        ALLOCATE = 0x003,
        REFRESH = 0x004,
        SEND = 0x006,
        DATA = 0x007,
        CREATE_PERMISSION = 0x008,
        CHANNEL_BIND = 0x009,
        CONNECT = 0x00A,
        CONNECTION_BIND = 0x00B,
        CONNECTION_ATTEMPT = 0x00C,
        GOOG_PING = 0x080,
    }
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    internal struct StunHeader {
        [FieldOffset(0)]
        ushort type;
        [FieldOffset(2)]
        ushort length;
        [FieldOffset(4)]
        uint magicCookie/* = 0x2112A442*/;
        [FieldOffset(8)]
        unsafe public fixed byte transactionID[12];

        public StunClass Class {
            get {
                ushort t = BinaryPrimitives.ReverseEndianness(type);
                return (StunClass)(((t >> 4) & 0b01) | (t >> 7) & 0b10);
            }
            init {
                type &= 0b11111011101111;
                type |= (ushort)((((ushort)value & 0x01) << 4) | (((ushort)value & 0b10) << 7));
                type = BinaryPrimitives.ReverseEndianness(type);
            }
        }
        public StunMethod Method {
            get {
                ushort t = BinaryPrimitives.ReverseEndianness(type);
                return (StunMethod)(t & 0b1111 | (t >> 5) & 0b111 | (t >> 9) & 0b11111);
            }
            init {
                type &= 0b00000100010000;
                type |= (ushort)((ushort)value & 0b1111 | ((ushort)value & 0b1110000) << 1 | ((ushort)value & 0b111110000000) << 2);
                type = BinaryPrimitives.ReverseEndianness(type);
            }
        }
        public ushort Length {
            get { return BinaryPrimitives.ReverseEndianness(length); }
        }
        public uint MagicCookie {
            get { return BinaryPrimitives.ReverseEndianness(magicCookie); }
        }
        public bool isSameTransaction(Span<byte> transactionID) {
            bool flag = false;
            unsafe {
                fixed (byte* p = this.transactionID) {
                    flag = transactionID.SequenceEqual(new Span<byte>(p, 12));
                }
            }
            return flag;
        }
    }

    internal enum StunAttrType : ushort {
        MAPPED_ADDRESS = 0x0001,
        XOR_MAPPED_ADDRESS = 0x0020
    }
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    internal struct StunAttributeHeader {
        [FieldOffset(0)]
        ushort type;
        [FieldOffset(2)]
        ushort length;
        public StunAttrType Type {
            get { return (StunAttrType)BinaryPrimitives.ReverseEndianness(type); }
        }
        public ushort Length {
            get { return BinaryPrimitives.ReverseEndianness(length); }
        }
    }

    namespace StunAttr { 
        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        internal struct MappedAddress {
            [FieldOffset(0)]
            public byte zero;//Must be 0x00
            [FieldOffset(1)]
            public byte family;//0x01:IPv4 0x02:IPv6
            [FieldOffset(2)]
            ushort port;
            [FieldOffset(4)]
            unsafe fixed byte address[4];
            [FieldOffset(4)]
            uint addressIPv4;
            public ushort Port {
                get { return BinaryPrimitives.ReverseEndianness(port); }
            }
            public uint AddressIPv4 {
                get { return BinaryPrimitives.ReverseEndianness(addressIPv4); }
            }
        }
        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        internal struct XorMappedAddress {
            [FieldOffset(0)]
            public byte zero;//Must be 0x00
            [FieldOffset(1)]
            public byte family;//0x01:IPv4 0x02:IPv6
            [FieldOffset(2)]
            ushort xport;
            [FieldOffset(4)]
            unsafe fixed byte xaddress[4];
            [FieldOffset(4)]
             uint xaddressIPv4;
            public ushort Port {
                get { return (ushort)(BinaryPrimitives.ReverseEndianness(xport) ^ 0x2112); }
            }
            public uint Address {
                get { return BinaryPrimitives.ReverseEndianness(xaddressIPv4) ^ 0x2112A442; }
            }
        }
    }
}
