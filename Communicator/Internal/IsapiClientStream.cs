using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Communicator.Internal
{
    /// <summary>
    /// A stream interface to the ISAPI client data protocol.
    /// This handles the difference between
    /// </summary>
    public class IsapiClientStream : Stream
    {
        private readonly IntPtr _connId;
        private readonly int _bytesAvailable;
        private readonly Delegates.ReadClientDelegate _readClient;
        private readonly byte[] _initialBytes;
        private int _remaining;

        public IsapiClientStream(IntPtr connId, int bytesAvailable, int bytesDeclared, IntPtr data, Delegates.ReadClientDelegate readClient)
        {
            _connId = connId;
            _bytesAvailable = bytesAvailable;
            _readClient = readClient;
            Length = bytesDeclared;
            Position = 0;

            _remaining = bytesAvailable;
            _initialBytes = new byte[bytesAvailable];
            Marshal.Copy(data, _initialBytes, 0, bytesAvailable);
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining > 0) {
                var actual = Math.Min(_remaining, count);
                Buffer.BlockCopy(_initialBytes, _bytesAvailable - _remaining,
                                 buffer, offset, actual);
                _remaining -= actual;
                Position += actual;
                return actual;
            }

            var result = count;
            var ok = _readClient(_connId, buffer, ref result);
            if (! ok) return 0;

            Position += result;
            return result;
        }

        public override void Write(byte[] buffer, int offset, int count) { throw new NotImplementedException(); }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length { get; }
        public override long Position { get; set; }
    }
}