using System;
using System.Runtime.InteropServices;

namespace Communicator.Internal
{
    public class Delegates
    {
        // Any delegates should match exactly on the C++ side
        public delegate void VoidDelegate();
        public delegate void StringStringDelegate([MarshalAs(UnmanagedType.LPWStr)]string input, [MarshalAs(UnmanagedType.BStr)]out string output);

        /// <summary>
        /// Handle a request from IIS.
        /// </summary>
        /// <remarks>This is the parts of EXTENSION_CONTROL_BLOCK that are useful</remarks>
        public delegate void HandleHttpRequestDelegate(IntPtr conn,
            [MarshalAs(UnmanagedType.LPStr)] string verb,
            [MarshalAs(UnmanagedType.LPStr)] string query,
            [MarshalAs(UnmanagedType.LPStr)] string pathInfo,
            [MarshalAs(UnmanagedType.LPStr)] string pathTranslated,
            [MarshalAs(UnmanagedType.LPStr)] string contentType,

            Int32 bytesDeclared,
            Int32 bytesAvailable,
            IntPtr data,

            GetServerVariableDelegate getServerVariable, WriteClientDelegate writeClient, ReadClientDelegate readClient,
            IntPtr serverSupport // The server support function has many forms, so we use it dynamically
        );

        ////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Read context server variables (IIS speak for request headers)
        /// </summary>
        /// <param name="hConn">Context id</param>
        /// <param name="lpszVariableName">variable anem</param>
        /// <param name="lpvBuffer">A caller-allocated buffer for response</param>
        /// <param name="lpdwSize">Size of buffer. If failed due to too small, required buffer size is put in here</param>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public delegate bool GetServerVariableDelegate(IntPtr hConn,
                                                        [MarshalAs(UnmanagedType.LPStr)]string lpszVariableName,
                                                        IntPtr lpvBuffer,
                                                        ref Int32 lpdwSize);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public delegate bool WriteClientDelegate(IntPtr ConnID,
                                                [MarshalAs(UnmanagedType.LPArray)]byte[] Buffer,
                                                ref Int32 lpdwBytes,
                                                Int32 dwReserved);

        /// <summary>
        /// Read client supplied data
        /// https://msdn.microsoft.com/en-us/library/ms525214(v=vs.90).aspx
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public delegate bool ReadClientDelegate(IntPtr ConnID,
                                                [MarshalAs(UnmanagedType.LPArray)]byte[] lpvBuffer,
                                                ref Int32 lpdwSize);

        // The various forms of ServerSupportFunction...
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public delegate bool ServerSupportFunctionDelegate_Generic(IntPtr hConn,
                                                            Int32 dwHSERequest,
                                                            IntPtr lpvBuffer,
                                                            IntPtr lpdwSize,
                                                            IntPtr lpdwDataType);


        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public delegate bool ServerSupportFunctionDelegate_Headers(IntPtr hConn,
            Int32 dwHSERequest,
            SendHeaderExInfo lpvBuffer,
            IntPtr lpdwSize,
            IntPtr lpdwDataType);
    }
}