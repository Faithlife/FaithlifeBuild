using System;
using System.Reflection;
using XmlDocMarkdown.Core;

namespace XmlDocGen
{
	internal sealed class Program
	{
		private static int Main(string[] args)
		{
			try
			{
				if (args.Length != 1)
				{
					Console.Error.WriteLine("Usage: XmlDocGen [docs-path]");
					return 1;
				}

				var result = XmlDocMarkdownGenerator.Generate(
					input: new XmlDocInput { Assembly = Assembly.Load("Faithlife.Build") },
					outputPath: args[0],
					settings: new XmlDocMarkdownSettings
					{
						NewLine = "\n",
						ShouldClean = true,
						SourceCodePath = "https://github.com/Faithlife/FaithlifeBuild/tree/master/src/Faithlife.Build",
					});

				foreach (var message in result.Messages)
					Console.WriteLine(message);

				return 0;
			}
			catch (Exception exception)
			{
				Console.Error.WriteLine(exception.ToString());
				return 2;
			}
		}
	}
}
