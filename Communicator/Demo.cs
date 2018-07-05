using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Communicator.Internal;

namespace Communicator
{
    /// <summary>
    /// Shows the use of C# to drive a C++ shim
    /// </summary>
    public class Demo
    {

        static GCHandle GcShutdownDelegateHandle;
        static readonly Delegates.VoidDelegate ShutdownPtr;
        static GCHandle GcHandleRequestDelegateHandle;
        static readonly Delegates.HandleHttpRequestDelegate HandlePtr;

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

            Delegates.GetServerVariableDelegate getServerVariable, Delegates.WriteClientDelegate writeClient, Delegates.ReadClientDelegate readClient, IntPtr serverSupport)
        {
            // Send header:
            try
            {
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
            var headerCall = Marshal.GetDelegateForFunctionPointer<Delegates.ServerSupportFunctionDelegate_Headers>(ss);

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
