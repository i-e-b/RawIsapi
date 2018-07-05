using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Communicator
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
        byte[] data,

        GetServerVariableDelegate getServerVariable, WriteClientDelegate writeClient, ReadClientDelegate readClient, ServerSupportFunctionDelegate serverSupport);

    [StructLayout(LayoutKind.Sequential)]
    public class EXTENSION_CONTROL_BLOCK {
        public Int32     cbSize;                 // size of this struct.
        public Int32     dwVersion;              // version info of this spec
        public IntPtr    ConnID;                 // Context number not to be modified!
        public Int32     dwHttpStatusCode;       // HTTP Status code

        //[MarshalAs(UnmanagedType.LPStr)]
        public IntPtr lpszLogData; // is this output?
        //CHAR      lpszLogData[HSE_LOG_BUFFER_LEN];// null terminated log info specific to this Extension DLL
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string     lpszMethod;             // REQUEST_METHOD
        [MarshalAs(UnmanagedType.LPStr)]
        public string     lpszQueryString;        // QUERY_STRING
        [MarshalAs(UnmanagedType.LPStr)]
        public string     lpszPathInfo;           // PATH_INFO
        [MarshalAs(UnmanagedType.LPStr)]
        public string     lpszPathTranslated;     // PATH_TRANSLATED

        public Int32     cbTotalBytes;           // Total bytes indicated from client
        public Int32     cbAvailable;            // Available number of bytes
        public IntPtr  /*byte[]*/    lpbData;                // pointer to cbAvailable bytes
        
        //[MarshalAs(UnmanagedType.LPStr)]
        public IntPtr/*string*/     lpszContentType;        // Content type of client data

        /*
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public GetServerVariableDelegate GetServerVariable;

        [MarshalAs(UnmanagedType.FunctionPtr)]
        public WriteClientDelegate WriteClient;

        [MarshalAs(UnmanagedType.FunctionPtr)]
        public ReadClientDelegate ReadClient;

        [MarshalAs(UnmanagedType.FunctionPtr)]
        public ServerSupportFunctionDelegate ServerSupportFunction;*/
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public delegate bool GetServerVariableDelegate(IntPtr hConn,
                                                    [MarshalAs(UnmanagedType.LPStr)]string lpszVariableName,
                                                    [MarshalAs(UnmanagedType.LPArray)]byte[] lpvBuffer,
                                                    ref Int32 lpdwSize);
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public delegate bool WriteClientDelegate(IntPtr ConnID,
                                            [MarshalAs(UnmanagedType.LPArray)]byte[] Buffer,
                                            ref Int32 lpdwBytes,
                                            Int32 dwReserved);
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public delegate bool ReadClientDelegate(IntPtr ConnID,
                                            [MarshalAs(UnmanagedType.LPArray)]byte[] lpvBuffer,
                                            ref Int32 lpdwSize);
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public delegate bool ServerSupportFunctionDelegate(IntPtr hConn,
                                                        Int32 dwHSERequest,
                                                        [MarshalAs(UnmanagedType.LPArray)]byte[] lpvBuffer,
                                                        ref Int32 lpdwSize,
                                                        ref Int32 lpdwDataType);

    /// <summary>
    /// Shows the use of C# to drive a C++ shim
    /// </summary>
    public class Demo
    {

        /// <summary>Send data back to the host</summary>
        /// <remarks>
        /// Sneaky trick -- we use the host dll to talk to itself.
        /// We use this shared memory to pass function pointers back to C++
        /// Thanks to https://stackoverflow.com/a/9593846/423033
        /// </remarks>
        [DllImport("RawIsapi.dll")]
        public static extern bool SetSharedMem(Int64 value);

        static GCHandle GcShutdownDelegateHandle;
        static readonly VoidDelegate ShutdownPtr;
        static GCHandle GcHandleRequestDelegateHandle;
        static readonly HandleHttpRequestDelegate HandlePtr;

        /// <summary>
        /// Build our function tables...
        /// </summary>
        static Demo () {
            ShutdownPtr = ShutdownCallback; // get permanent function pointer
            GcShutdownDelegateHandle = GCHandle.Alloc(ShutdownPtr); // prevent garbage collection

            HandlePtr = HandleHttpRequestCallback;
            GcHandleRequestDelegateHandle = GCHandle.Alloc(HandlePtr);
        }

        /// <summary>
        /// This is a special format of method that can be directly called by `ExecuteInDefaultAppDomain`
        /// <para>See https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/hosting/iclrruntimehost-executeindefaultappdomain-method.</para>
        /// We use this to provide function pointers for the C++ code to use.
        /// </summary>
        public static int FindFunctionPointer(string requestType)
        {
            switch (requestType) {
                case "Shutdown":
                    return WriteFunctionPointer(ShutdownPtr);

                case "Handle":
                    return WriteFunctionPointer(HandlePtr);

                default: return -1;
            }
        }

        private static int WriteFunctionPointer(Delegate del)
        {
            try
            {
                var bSetOk = SetSharedMem(Marshal.GetFunctionPointerForDelegate(del).ToInt64());
                return bSetOk ? 1 : 0;
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\Temp\InnerError.txt", "\r\nError when sending delegate: " + ex);
                return -2;
            }
        }

        public static void ShutdownCallback()
        {
            GcShutdownDelegateHandle.Free();
            GcHandleRequestDelegateHandle.Free();
        }
        
        public static void HandleHttpRequestCallback(IntPtr conn,
            [MarshalAs(UnmanagedType.LPStr)] string verb,
            [MarshalAs(UnmanagedType.LPStr)] string query,
            [MarshalAs(UnmanagedType.LPStr)] string pathInfo,
            [MarshalAs(UnmanagedType.LPStr)] string pathTranslated,
            [MarshalAs(UnmanagedType.LPStr)] string contentType,

            Int32 bytesDeclared,
            Int32 bytesAvailable,
            byte[] data,

            GetServerVariableDelegate getServerVariable, WriteClientDelegate writeClient, ReadClientDelegate readClient, ServerSupportFunctionDelegate serverSupport)
        {
            var sb = new StringBuilder();
            sb.Append("<html><head><title>.Net output</title></head><body>");


            sb.Append("Hello! .Net here.<dl>");

            sb.Append("<dt>Verb</dt><dd>"+verb+"</dd>");
            sb.Append("<dt>Query</dt><dd>"+query+"</dd>");
            sb.Append("<dt>Path Info</dt><dd>"+pathInfo+"</dd>");
            sb.Append("<dt>Path Translated</dt><dd>"+pathTranslated+"</dd>");
            sb.Append("<dt>Content Type</dt><dd>"+contentType+"</dd>");

            sb.Append("</dl>");

            sb.Append("</body></html>");

            sb.Append('\0');
            var msg = Encoding.UTF8.GetBytes(sb.ToString());
            int len = msg.Length;
            writeClient(conn, msg, ref len, 0);

        }

    }
}
