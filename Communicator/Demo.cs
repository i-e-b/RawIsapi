using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Communicator.Internal;
using static Communicator.Internal.Delegates;
using Tag;

namespace Communicator
{
    /// <summary>
    /// Shows the use of C# to drive a C++ shim
    /// </summary>
    public class Demo
    {

        static GCHandle GcShutdownDelegateHandle;
        static readonly VoidDelegate ShutdownPtr;
        static GCHandle GcHandleRequestDelegateHandle;
        static readonly HandleHttpRequestDelegate HandlePtr;

        /// <summary>
        /// Build our function tables...
        /// </summary>
        static Demo()
        {
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
            switch (requestType)
            {
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
                var bSetOk = Win32.SetSharedMem(Marshal.GetFunctionPointerForDelegate(del).ToInt64());
                return bSetOk ? 1 : 0;
            }
            catch //(Exception ex)
            {
                //File.AppendAllText(@"C:\Temp\InnerError.txt", "\r\nError when sending delegate: " + ex);
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
            IntPtr data,

            GetServerVariableDelegate getServerVariable, WriteClientDelegate writeClient, ReadClientDelegate readClient, IntPtr serverSupport)
        {
            try
            {
                var head = T.g("head")[T.g("title")[".Net Output"]];
                
                var body = T.g("body")[
                    T.g("h1")["Hello"],
                    T.g("p")[".Net here!"],
                    T.g("p")["You called me with these properties:"],

                    T.g("dl")[
                        Def("Verb", verb),
                        Def("Query string", query),
                        Def("URL path", pathInfo),
                        Def("Equivalent file path", pathTranslated),
                        Def("Requested content type", contentType)
                    ],

                    T.g("p")["Client supplied "+bytesAvailable+" bytes out of an expected "+bytesDeclared+" bytes"]
                ];

                if (bytesAvailable > 0) {
                    byte[] strBytes = new byte[bytesAvailable];
                    Marshal.Copy(data, strBytes, 0, bytesAvailable);
                    var sent = Encoding.UTF8.GetString(strBytes);
                    body.Add(T.g("p")["here's a text version of what you sent:"]);
                    body.Add(T.g("pre")[sent]);
                }

                var page = T.g("html")[ head, body ];

                var ms = new MemoryStream();
                page.StreamTo(ms, Encoding.UTF8);
                ms.WriteByte(0);
                ms.Seek(0, SeekOrigin.Begin);
                var msg = ms.ToArray();
                int len = msg.Length;

                TryWriteHeaders(conn, serverSupport);
                writeClient(conn, msg, ref len, 0);
            }
            catch (Exception ex)
            {
                var msg = Encoding.UTF8.GetBytes(ex.ToString());
                int len = msg.Length;
                writeClient(conn, msg, ref len, 0);
            }
        }

        private static TagContent Def(string name, string value)
        {
            return T.g()[
                T.g("dt")[name],
                T.g("dd")[value]
            ];
        }

        /// <summary>
        /// Test of 'ex' status writing
        /// </summary>
        private static void TryWriteHeaders(IntPtr conn, IntPtr ss)
        {
            var headerCall = Marshal.GetDelegateForFunctionPointer<ServerSupportFunctionDelegate_Headers>(ss);

            var data = new SendHeaderExInfo
            {
                fKeepConn = false,
                pszHeader = "X-Fish: I come from the marshall\r\nContent-type: text/html\r\n\r\n",
                pszStatus = "200 OK-dokey"
            };

            data.cchStatus = data.pszStatus.Length;
            data.cchHeader = data.pszHeader.Length;

            headerCall(conn, Win32.HSE_REQ_SEND_RESPONSE_HEADER_EX, data, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
