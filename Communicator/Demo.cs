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

        GetServerVariableDelegate getServerVariable, WriteClientDelegate writeClient, ReadClientDelegate readClient,
        IntPtr serverSupport // The server support function has many forms, so we use it dynamically
    );

    ////////////////////////////////////////////////////////////////////////////////////////////////////

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
        


        //c:\Program Files (x86)\Windows Kits\10\Include\10.0.17134.0\um\HttpExt.h
        public const int HSE_REQ_SEND_RESPONSE_HEADER    = 3;    // old
        public const int HSE_REQ_SEND_RESPONSE_HEADER_EX = 1016; // new

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

            GetServerVariableDelegate getServerVariable, WriteClientDelegate writeClient, ReadClientDelegate readClient, IntPtr serverSupport)
        {
            // Send header:
            /*
    char MyHeader[4096];
    strcpy(MyHeader, "Content-type: text/html\n\n");
    pEcb->ServerSupportFunction(pEcb->ConnID, HSE_REQ_SEND_RESPONSE_HEADER, NULL, 0, (DWORD *)MyHeader);*/


            // Send headers the buggy old way:
            /*var header = CString("Content-type: text/html\n\n");
            var status = CString("My Status String");
            GCHandle pinnedArray = GCHandle.Alloc(header, GCHandleType.Pinned);
            IntPtr pointer = pinnedArray.AddrOfPinnedObject();
            int zero = 0;
            serverSupport(conn, HSE_REQ_SEND_RESPONSE_HEADER, status, ref zero, pointer);
            pinnedArray.Free();*/
            
            try {
                TryWriteHeaders(conn, serverSupport);


                var sb = new StringBuilder();
                sb.Append("<html><head><title>.Net output</title></head><body>");


                sb.Append("Hello! .Net here.<dl>");

                sb.Append("<dt>Verb</dt><dd>" + verb + "</dd>");
                sb.Append("<dt>Query</dt><dd>" + query + "</dd>");
                sb.Append("<dt>Path Info</dt><dd>" + pathInfo + "</dd>");
                sb.Append("<dt>Path Translated</dt><dd>" + pathTranslated + "</dd>");
                sb.Append("<dt>Content Type</dt><dd>" + contentType + "</dd>");

                sb.Append("</dl>");

                sb.Append("</body></html>");

                sb.Append('\0');
                var msg = Encoding.UTF8.GetBytes(sb.ToString());
                int len = msg.Length;
                writeClient(conn, msg, ref len, 0);
            }
            catch (Exception ex)
            {
                var msg = Encoding.UTF8.GetBytes(ex.ToString());
                int len = msg.Length;
                writeClient(conn, msg, ref len, 0);
            }
        }

        /// <summary>
        /// Test of 'ex' status writing
        /// </summary>
        private static void TryWriteHeaders(IntPtr conn, IntPtr ss)
        {
            var headerCall = Marshal.GetDelegateForFunctionPointer<ServerSupportFunctionDelegate_Headers>(ss);

            var data = new SendHeaderExInfo{
                fKeepConn = false,
                pszHeader = "X-Fish: I come from the marshall\r\nContent-type: text/html\r\n\r\n",
                pszStatus = "200 OK-dokey"
            };

            data.cchStatus = data.pszStatus.Length;
            data.cchHeader = data.pszHeader.Length;

            headerCall(conn, HSE_REQ_SEND_RESPONSE_HEADER_EX, data, IntPtr.Zero, IntPtr.Zero);
        }

        private static byte[] CString(string str)
        {
            return Encoding.ASCII.GetBytes(str + "\0");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class SendHeaderExInfo
    {
        /// <summary>
        /// The NULL-terminated string containing the status of the current request.
        /// </summary>
        public string pszStatus;

        /// <summary>
        /// The NULL-terminated string containing the header to return.
        /// </summary>
        public string pszHeader;

        /// <summary>
        /// The number of characters in the status code.
        /// </summary>
        public Int32 cchStatus;

        /// <summary>
        /// The number of characters in the header.
        /// </summary>
        public Int32 cchHeader;

        /// <summary>
        /// A Boolean indicating whether or not the connection that was used to process the request should remain active
        /// </summary>
        [MarshalAs(UnmanagedType.Bool)]
        public bool fKeepConn;
    }
}
