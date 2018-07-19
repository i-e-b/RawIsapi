using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Web;
using Communicator.Internal;
using Huygens;
using static Communicator.Internal.Delegates;
using Tag;

namespace Communicator
{
    /// <summary>
    /// Shows the use of C# to drive a C++ shim
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    [PermissionSet(SecurityAction.Demand, Unrestricted = true)]
    [SecurityPermission(SecurityAction.Demand, Unrestricted = true)]
    [SecurityCritical]
    public class Demo
    {
        static GCHandle GcShutdownDelegateHandle;
        static readonly VoidDelegate ShutdownPtr;
        static GCHandle GcHandleRequestDelegateHandle;
        static readonly HandleHttpRequestDelegate HandlePtr;

        private static DirectServer _proxy;

        /// <summary>
        /// Static constructor. Build the function pointers
        /// </summary>
        static Demo()
        {
            // TEMP: This block should be integrated into Huygens
            //Type versionInfoType = typeof(HttpApplication).Assembly.GetType("System.Web.Util.VersionInfo");
            //FieldInfo exeNameField = versionInfoType.GetField("_exeName", BindingFlags.Static | BindingFlags.NonPublic);
            //exeNameField.SetValue(null, "AnythingElse.exe");
            // END TEMP

            ShutdownPtr = ShutdownCallback; // get permanent function pointer
            GcShutdownDelegateHandle = GCHandle.Alloc(ShutdownPtr); // prevent garbage collection

            HandlePtr = HandleHttpRequestCallback;
            GcHandleRequestDelegateHandle = GCHandle.Alloc(HandlePtr);

            // Try never to let an exception escape
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
        }

        private static void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            var ex = e.Exception;
            if (ex != null) RecPrintException(ex);
            else File.AppendAllText(@"C:\Temp\RootException.txt", "\r\n\r\nNo exception");
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            //var appDomain = sender as AppDomain;
            var ex = e.ExceptionObject as Exception;

            if (ex != null) RecPrintException(ex);
            else File.AppendAllText(@"C:\Temp\RootException.txt", "\r\n\r\nNo exception");
        }

        private static void RecPrintException(Exception ex)
        {
            if (ex == null) return;
            File.AppendAllText(@"C:\Temp\RootException.txt", "\r\n\r\n" + ex);
            RecPrintException(ex.InnerException);
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

        /// <summary>
        /// A simple demo responder
        /// </summary>
        [PermissionSet(SecurityAction.Demand, Unrestricted = true)]
        [SecurityPermission(SecurityAction.Demand, Unrestricted = true, ControlAppDomain = true, UnmanagedCode = true)]
        [SecurityCritical]
        [SuppressUnmanagedCodeSecurity]
        public static void HandleHttpRequestCallback(
        #region params
            IntPtr conn,
            [MarshalAs(UnmanagedType.LPStr)] string verb,
            [MarshalAs(UnmanagedType.LPStr)] string query,
            [MarshalAs(UnmanagedType.LPStr)] string pathInfo,
            [MarshalAs(UnmanagedType.LPStr)] string pathTranslated,
            [MarshalAs(UnmanagedType.LPStr)] string contentType,

            Int32 bytesDeclared,
            Int32 bytesAvailable, // if available < declared, you need to run `readClient` to get more
            IntPtr data, // first blush of data, if any

            GetServerVariableDelegate getServerVariable, WriteClientDelegate writeClient, ReadClientDelegate readClient, IntPtr serverSupport)
        #endregion
        {
            try
            {
                if (_proxy == null) _proxy = new DirectServer("C:\\Temp\\WrappedSites\\1_rolling");
                var headerString = TryGetHeaders(conn, getServerVariable);
                
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

                    T.g("p")["Request headers: ",
                        T.g("pre")[headerString]
                    ],

                    T.g("p")["Client supplied " + bytesAvailable + " bytes out of an expected " + bytesDeclared + " bytes"]
                ];

                /*var rq = new SerialisableRequest();
                rq.Headers = SplitToDict(headerString);
                rq.Method = verb;
                rq.RequestUri = pathInfo;
                if (!string.IsNullOrWhiteSpace(query)) rq.RequestUri += "?" + query;
                rq.Content = ReadAllContent(bytesAvailable, bytesDeclared, data, readClient);
                */

                var rq = new SerialisableRequest
                {
                    RequestUri = "/values",
                    Headers = new Dictionary<string, string>(),
                    Content = new byte[0],
                    Method = "GET"
                };

                // Something is going *very* wrong with this call:
                var tx = _proxy.DirectCall(rq);
                // Maybe try running outside of ISAPI, but still under the hosted CLR



                if (bytesAvailable > 0) {
                    byte[] strBytes = new byte[bytesAvailable];
                    Marshal.Copy(data, strBytes, 0, bytesAvailable);
                    var sent = Encoding.UTF8.GetString(strBytes);
                    body.Add(T.g("p")["here's a text version of what you sent:"]);
                    body.Add(T.g("pre")[sent]);
                }


                // spit out the Huygens response
                if (tx != null)
                {
                    body.Add(T.g("hr/"));

                    var responseHeaderList = T.g("dl");
                    foreach (var header in tx.Headers)
                    {
                        responseHeaderList.Add(Def(header.Key, string.Join(",", header.Value)));
                    }
                    body.Add(
                        T.g("h2")["Huygen proxy response:"],
                        T.g("p")["Headers:"],
                        responseHeaderList,
                        T.g("p")["Status: ", tx.StatusCode + " ", tx.StatusMessage],
                        T.g("p")["Response data (as utf8 string):"],
                        T.g("pre")[Encoding.UTF8.GetString(tx.Content)]
                        );
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
                var ms = new MemoryStream();
                var bytes = Encoding.UTF8.GetBytes("<pre>" + ex + "</pre>");
                ms.Write(bytes, 0, bytes.Length);
                ms.WriteByte(0);
                ms.Seek(0, SeekOrigin.Begin);
                var msg = ms.ToArray();
                int len = msg.Length;

                TryWriteHeaders(conn, serverSupport);
                writeClient(conn, msg, ref len, 0);
            }
        }

        private static byte[] ReadAllContent(int bytesAvailable, int bytesDeclared, IntPtr data, ReadClientDelegate readClient)
        {
            if (bytesDeclared < 1) return null;

            byte[] strBytes = new byte[bytesAvailable];
            Marshal.Copy(data, strBytes, 0, bytesAvailable);
            // todo: read more data if required

            return strBytes;
        }

        private static Dictionary<string, string> SplitToDict(string headerString)
        {
            var output = new Dictionary<string,string>();
            var lines = headerString.Split(new []{'\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var bits = line.Split(new []{ ": "}, 2, StringSplitOptions.None);
                if (bits.Length != 2) continue; // invalid header

                var key = bits[0].Trim();
                var value = bits[1].Trim();
                if (output.ContainsKey(key)) output[key] = output[key] + ", " + value;
                else output.Add(key, value);
            }
            return output;
        }

        private static TagContent Def(string name, string value)
        {
            return T.g()[
                T.g("dt")[name],
                T.g("dd")[value]
            ];
        }

        /// <summary>
        /// Read headers from the incoming request
        /// </summary>
        private static string TryGetHeaders(IntPtr conn, GetServerVariableDelegate callback)
        {
            var size = 4096;
            var buffer = Marshal.AllocHGlobal(size);
            try {
                callback(conn, "UNICODE_ALL_RAW", buffer, ref size);
                return Marshal.PtrToStringUni(buffer);
            } finally {
                Marshal.FreeHGlobal(buffer);
            }
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
                pszHeader = "X-Fish: I come from the marshall\r\nX-CB: "+typeof(DirectServer).Assembly.CodeBase+"\r\nContent-type: text/html\r\n\r\n",
                pszStatus = "200 OK-dokey"
            };

            data.cchStatus = data.pszStatus.Length;
            data.cchHeader = data.pszHeader.Length;

            headerCall(conn, Win32.HSE_REQ_SEND_RESPONSE_HEADER_EX, data, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
