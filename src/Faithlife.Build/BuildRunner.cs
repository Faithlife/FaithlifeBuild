using System;
using System.Collections.Generic;
using System.Linq;
using Bullseye;
using McMaster.Extensions.CommandLineUtils;

namespace Faithlife.Build
{
	/// <summary>
	/// Used to execute an automated build.
	/// </summary>
	public static class BuildRunner
	{
		/// <summary>
		/// Executes an automated build. Called from <c>Main</c>.
		/// </summary>
		/// <param name="args">The command-line arguments from <c>Main</c>.</param>
		/// <param name="initialize">Called to initialize the build.</param>
		/// <returns>The exit code for the build.</returns>
		public static int Execute(string[] args, Action<BuildApp> initialize)
		{
			if (args == null)
				throw new ArgumentNullException(nameof(args));
			if (initialize == null)
				throw new ArgumentNullException(nameof(initialize));

			var commandLineApp = new CommandLineApplication();

			var buildApp = new BuildApp(commandLineApp);
			initialize(buildApp);

			var noColorFlag = buildApp.AddFlag("--no-color", "Disable color output");
			var skipDependenciesFlag = buildApp.AddFlag("-s|--skip-dependencies", "Don't run target dependencies");
			var helpFlag = buildApp.AddFlag("-h|-?|--help", "Show build help");
			var targetsArgument = commandLineApp.Argument("targets", "The targets to build", multipleValues: true);

			var bullseyeTargets = new Targets();
			foreach (var target in buildApp.Targets)
				bullseyeTargets.Add(target.Name, target.Dependencies, target.Run);

			commandLineApp.OnExecute(() =>
			{
				var bullseyeArgs = targetsArgument.Values.ToList();

				if (bullseyeArgs.Count == 0 && buildApp.Targets.Any(x => x.Name == c_defaultTarget))
					bullseyeArgs.Add(c_defaultTarget);

				if (helpFlag.Value || bullseyeArgs.Count == 0)
				{
					commandLineApp.ShowHelp(usePager: false);
					ShowTargets(buildApp.Targets);
				}
				else
				{
					if (noColorFlag.Value)
						bullseyeArgs.Add("--no-color");
					if (skipDependenciesFlag.Value)
						bullseyeArgs.Add("--skip-dependencies");
					bullseyeArgs.Add("--no-extended-chars");

					try
					{
						bullseyeTargets.RunWithoutExiting(bullseyeArgs);
					}
					catch (TargetFailedException)
					{
						return 1;
					}
					catch (Exception exception) when (exception is ApplicationException || exception is CommandParsingException || exception is InvalidUsageException)
					{
						Console.Error.WriteLine(exception.Message);
						return 2;
					}
				}

				return 0;
			});

			return commandLineApp.Execute(args);
		}

		private static void ShowTargets(IReadOnlyList<BuildTarget> targets)
		{
			var targetsToShow = targets.Where(x => x.Name != c_defaultTarget || !string.IsNullOrEmpty(x.Description)).ToList();
			if (targetsToShow.Count != 0)
			{
				Console.WriteLine("Targets:");
				var maxTargetLength = targetsToShow.Select(x => x.Name.Length).Max();
				foreach (var target in targetsToShow)
					Console.WriteLine("  {0}  {1}", target.Name.PadRight(maxTargetLength), target.Description);
			}
		}

		private const string c_defaultTarget = "default";
	}
}
