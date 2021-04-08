using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace Quaestor.Utilities
{
	public static class ProcessUtils
	{
		/// <summary>
		///     Starts a new process.
		/// </summary>
		/// <param name="fileName">The full path of the executable</param>
		/// <param name="arguments">The arguments</param>
		/// <param name="useShellExecute">
		///     If using false make sure to avoid dead-locks by calling
		///     Process.BeginOutputReadLine() and Process.BeginErrorReadLine() before waiting for exit.
		/// </param>
		/// <param name="createNoWindow"></param>
		/// <param name="priorityClass">The process priority class.</param>
		/// <returns></returns>
		[NotNull]
		public static Process StartProcess(
			[CanBeNull] string fileName,
			[CanBeNull] string arguments,
			bool useShellExecute,
			bool createNoWindow,
			ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal)
		{
			if (string.IsNullOrEmpty(fileName))
			{
				throw new ArgumentNullException(nameof(fileName));
			}

			var process = new Process
			{
				StartInfo = {FileName = fileName}
			};

			if (arguments != null)
			{
				process.StartInfo.Arguments = arguments;
			}

			process.StartInfo.UseShellExecute = useShellExecute;

			// make sure there is no dead-lock due to standard output
			process.StartInfo.RedirectStandardOutput = !useShellExecute;
			process.StartInfo.RedirectStandardError = !useShellExecute;

			if (createNoWindow)
			{
				process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				process.StartInfo.CreateNoWindow = true;
			}

			process.Start();

			process.PriorityClass = priorityClass;

			return process;
		}

		public static int RunningProcessesCount(string processName)
		{
			Process[] runningImagePreparations = Process.GetProcessesByName(processName);

			return runningImagePreparations.Length;
		}
	}
}
