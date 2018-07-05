using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Communicator
{

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

        public IntPtr A;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public WriteClientDelegate WriteClient;
        //public IntPtr B;
        public IntPtr C;
        public IntPtr D;

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
        // Any delegates should match exactly on the C++ side
        public delegate void VoidDelegate();
        public delegate void StringStringDelegate([MarshalAs(UnmanagedType.LPWStr)]string input, [MarshalAs(UnmanagedType.BStr)]out string output);
        public delegate void HandleHttpRequestDelegate(IntPtr conn, WriteClientDelegate ecb);

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
        static GCHandle GcTestDelegateHandle;
        static readonly StringStringDelegate TestPtr;
        static GCHandle GcHandleRequestDelegateHandle;
        static readonly HandleHttpRequestDelegate HandlePtr;

        /// <summary>
        /// Build our function tables...
        /// </summary>
        static Demo () {
            ShutdownPtr = ShutdownCallback; // get permanent function pointer
            GcShutdownDelegateHandle = GCHandle.Alloc(ShutdownPtr); // prevent garbage collection

            TestPtr = TestCallback;
            GcTestDelegateHandle = GCHandle.Alloc(TestPtr);

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

                case "Test":
                    return WriteFunctionPointer(TestPtr);

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
            GcTestDelegateHandle.Free();
            GcHandleRequestDelegateHandle.Free();
        }
        
        static string lastMsg = "No call made";
        
        public static void HandleHttpRequestCallback(IntPtr conn, WriteClientDelegate ecb)
        {
            var msg = Encoding.UTF8.GetBytes("Hello directly from .Net!\0");
            int len = msg.Length;
            ecb(conn, msg, ref len, 0);
            
            //var msg = Encoding.UTF8.GetBytes("Hello directly from .Net!");
            //int leng = msg.Length;
            //ecb.WriteClient(ecb.ConnID, msg, ref leng, 0);
            /*try
            {
                lastMsg = "\r\nActual pointer: " + ecb.ToString("X");

                var ptr = new IntPtr(ecb);
                lastMsg += "\r\nDerefd pointer: " + ptr.ToString("X");
                // var x = Marshal.PtrToStructure<EXTENSION_CONTROL_BLOCK>(ecb);
                //lastMsg = x.lpszQueryString;
            }
            catch (Exception ex)
            {
                lastMsg = ex.ToString();
            }*/
        }

        public static void TestCallback(string input, out string output)
        {
            //Debugger.Launch(); // this doesn't quite work...
            //output = string.Join("", input.Reverse()); // just a test

            output = lastMsg;
        }
    }
}
