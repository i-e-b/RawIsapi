using System;
using System.Runtime.InteropServices;

namespace Communicator.Internal
{
    /// <summary>
    /// Interop calls
    /// </summary>
    public class Win32
    {
        /// <summary>Send data back to the host</summary>
        /// <remarks>
        /// Sneaky trick -- we use the host dll to talk to itself.
        /// We use this shared memory to pass function pointers back to C++
        /// Thanks to https://stackoverflow.com/a/9593846/423033
        /// </remarks>
        [DllImport("RawIsapi.dll")]
        public static extern bool SetSharedMem(Int64 value);

        //c:\Program Files (x86)\Windows Kits\10\Include\10.0.17134.0\um\HttpExt.h
        public const int HSE_REQ_SEND_RESPONSE_HEADER = 3;    // old
        public const int HSE_REQ_SEND_RESPONSE_HEADER_EX = 1016; // new
    }
}