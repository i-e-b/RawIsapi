using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Communicator
{
    /// <summary>
    /// Shows the use of C# to drive a C++ shim
    /// </summary>
    public class Demo
    {
        // Any delegates should match exactly on the C++ side
        public delegate void VoidDelegate();

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

        /// <summary>
        /// Build our function tables...
        /// </summary>
        static Demo () {
            ShutdownPtr = ShutdownCallback; // get permanent function pointer
            GcShutdownDelegateHandle = GCHandle.Alloc(ShutdownPtr); // prevent garbage collection
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
        }
    }
}
