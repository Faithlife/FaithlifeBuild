using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using SimpleExec;

namespace Faithlife.Build
{
	public static class AppRunner
	{
		public static void RunApp(string path, params string[] args) =>
			Command.Run(path, ArgumentEscaper.EscapeAndConcatenate(args.Where(x => x != null)));
	}
}
