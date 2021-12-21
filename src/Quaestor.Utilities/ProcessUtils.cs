using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
		/// <param name="environmentVariables"></param>
		/// <returns></returns>
		[NotNull]
		public static Process StartProcess(
			[CanBeNull] string fileName,
			[CanBeNull] string arguments,
			bool useShellExecute,
			bool createNoWindow,
			IEnumerable<KeyValuePair<string, string>> environmentVariables = null)
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

			if (environmentVariables != null)
			{
				foreach (KeyValuePair<string, string> keyValuePair in environmentVariables)
				{
					StringDictionary environmentVars = process.StartInfo.EnvironmentVariables;

					if (environmentVars.ContainsKey(keyValuePair.Key))
					{
						environmentVars.Remove(keyValuePair.Key);
					}

					environmentVars.Add(keyValuePair.Key, keyValuePair.Value);
				}
			}

			process.Start();

			return process;
		}

		public static int RunningProcessesCount(string processName, int exceptProcessId)
		{
			Process[] processes = Process.GetProcessesByName(processName);

			return exceptProcessId >= 0
				? processes.AsEnumerable().Count(p => p.Id != exceptProcessId)
				: processes.Length;
		}

		public static void EnsureThreadIdInName()
		{
			Thread.CurrentThread.Name = $"Thread {Thread.CurrentThread.ManagedThreadId}";
		}
	}
}
