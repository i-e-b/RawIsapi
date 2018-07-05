using System;
using System.Runtime.InteropServices;

namespace Communicator.Internal
{
    /// <summary>
    /// Structure for sending headers and status to IIS/ISAPI
    /// </summary>
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