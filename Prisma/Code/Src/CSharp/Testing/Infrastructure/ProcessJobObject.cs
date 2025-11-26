using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ExxerCube.Prisma.Testing.Infrastructure;

/// <summary>
/// Manages a Windows Job Object to track and terminate all child processes.
/// Solves the problem of leaked processes when opening documents with UseShellExecute=true.
/// </summary>
/// <remarks>
/// When you open a document with Process.Start() using UseShellExecute=true, the returned
/// process is often just a shell process that spawns the actual viewer (Adobe Reader, Word, etc.)
/// as a child process. Tracking only the parent PID misses these child processes.
///
/// Job Objects automatically track ALL descendant processes, ensuring complete cleanup.
/// </remarks>
public sealed class ProcessJobObject : IDisposable
{
    private IntPtr _jobHandle;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessJobObject"/> class.
    /// Creates a Windows Job Object that will terminate all assigned processes on disposal.
    /// </summary>
    public ProcessJobObject()
    {
        // Create job object
        _jobHandle = CreateJobObject(IntPtr.Zero, null);
        if (_jobHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create job object");
        }

        // Configure job to kill all processes when the job handle is closed
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            }
        };

        int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
        IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, extendedInfoPtr, false);

            if (!SetInformationJobObject(_jobHandle, JobObjectInfoType.ExtendedLimitInformation,
                extendedInfoPtr, (uint)length))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to set job object information");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(extendedInfoPtr);
        }
    }

    /// <summary>
    /// Assigns a process to this job object.
    /// All child processes spawned by this process will also be automatically assigned to the job.
    /// </summary>
    /// <param name="process">The process to assign to the job.</param>
    public void AssignProcess(Process process)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ProcessJobObject));
        }

        if (!AssignProcessToJobObject(_jobHandle, process.Handle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"Failed to assign process {process.Id} to job object");
        }
    }

    /// <summary>
    /// Disposes the job object, automatically terminating all assigned processes and their children.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_jobHandle != IntPtr.Zero)
        {
            CloseHandle(_jobHandle);
            _jobHandle = IntPtr.Zero;
        }

        _disposed = true;
    }

    // P/Invoke declarations
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType,
        IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    private enum JobObjectInfoType
    {
        ExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public int LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public int ActiveProcessLimit;
        public UIntPtr Affinity;
        public int PriorityClass;
        public int SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
