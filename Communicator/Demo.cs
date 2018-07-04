using System;
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

        /// <summary>
        /// Sneaky trick -- we use the host dll to talk to itself.
        /// We use this shared memory to pass function pointers back to C++
        /// Thanks to https://stackoverflow.com/a/9593846/423033
        /// </summary>
        [DllImport("RawIsapi.dll")]
        public static extern bool SetSharedMem(Int64 value);

        static GCHandle GcShutdownDelegateHandle;

        /// <summary>
        /// This is a special format of method that can be directly called by `ExecuteInDefaultAppDomain`
        /// <para>See https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/hosting/iclrruntimehost-executeindefaultappdomain-method.</para>
        /// We use this to provide function pointers for the C++ code to use.
        /// </summary>
        public static int FindFunctionPointer(string requestType)
        {
            CheckInitialSetup();

            switch (requestType) {
                case "Shutdown":
                    return WriteFunctionPointer(GcShutdownDelegateHandle);

                default: return 0;
            }
        }

        private static int WriteFunctionPointer(GCHandle hndl)
        {
            try
            {
                var bSetOk = SetSharedMem(hndl.AddrOfPinnedObject().ToInt64());
                return bSetOk ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static void CheckInitialSetup()
        {
            if (GcShutdownDelegateHandle.IsAllocated) return;

            GcShutdownDelegateHandle = GCHandle.Alloc(new VoidDelegate(ShutdownCallback));
        }

        public static void ShutdownCallback()
        {
            GcShutdownDelegateHandle.Free();
        }
    }
}
