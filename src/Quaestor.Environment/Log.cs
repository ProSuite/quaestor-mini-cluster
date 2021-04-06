using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Quaestor.Environment
{
	public static class Log
	{
		private static ILoggerFactory _loggerFactory = new NullLoggerFactory();

		public static void SetLoggerFactory(ILoggerFactory loggerFactory)
		{
			_loggerFactory = loggerFactory;
		}

		public static ILogger CreateLogger(string categoryName)
		{
			return _loggerFactory.CreateLogger(categoryName);
		}

		public static ILogger<T> CreateLogger<T>()
		{
			return _loggerFactory.CreateLogger<T>();
		}
	}
}
