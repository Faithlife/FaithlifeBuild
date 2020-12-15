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

			var dryRunFlag = buildApp.AddFlag("-n|--dry-run", "Don't execute target actions");
			var skipDependenciesFlag = buildApp.AddFlag("-s|--skip-dependencies", "Don't run any target dependencies");
			var skipOption = buildApp.AddOption("--skip <targets>", "Skip the comma-delimited target dependencies");
			var noColorFlag = buildApp.AddFlag("--no-color", "Disable color output");
			var showTreeFlag = buildApp.AddFlag("--show-tree", "Show the dependency tree");
			var helpFlag = buildApp.AddFlag("-h|-?|--help", "Show build help");
			var targetsArgument = commandLineApp.Argument("targets", "The targets to build", multipleValues: true);

			var bullseyeTargets = new Targets();
			foreach (var target in buildApp.Targets)
				bullseyeTargets.Add(target.Name, target.Dependencies, target.Run);

			commandLineApp.OnExecute(() =>
			{
				var bullseyeArgs = targetsArgument.Values.WhereNotNull().ToList();

				if (bullseyeArgs.Count == 0 && buildApp.Targets.Any(x => x.Name == c_defaultTarget))
					bullseyeArgs.Add(c_defaultTarget);

				var skipDependencies = skipDependenciesFlag.Value;
				if (skipOption.Value is not null && !skipDependencies)
				{
					var skipTargetNames = new HashSet<string>(skipOption.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
					skipDependencies = true;

					var targetNames = new HashSet<string>(bullseyeArgs);
					foreach (var targetName in bullseyeArgs.ToList())
					{
						var target = buildApp.Targets.FirstOrDefault(x => x.Name == targetName);
						if (target is not null)
							AddTargetDependencies(target);
					}

					void AddTargetDependencies(BuildTarget child)
					{
						foreach (var parent in child.Dependencies.Select(name => buildApp.Targets.FirstOrDefault(x => x.Name == name)).WhereNotNull())
						{
							if (!skipTargetNames.Contains(parent.Name) && targetNames.Add(parent.Name))
							{
								bullseyeArgs.Add(parent.Name);
								AddTargetDependencies(parent);
							}
						}
					}
				}

				if (helpFlag.Value || (bullseyeArgs.Count == 0 && !showTreeFlag.Value))
				{
					commandLineApp.ShowHelp(usePager: false);
					ShowTargets(buildApp.Targets);
				}
				else
				{
					if (noColorFlag.Value)
						bullseyeArgs.Add("--no-color");
					if (skipDependencies)
						bullseyeArgs.Add("--skip-dependencies");
					if (showTreeFlag.Value)
						bullseyeArgs.Add("--list-tree");
					if (dryRunFlag.Value)
						bullseyeArgs.Add("--dry-run");
					bullseyeArgs.Add("--no-extended-chars");

					try
					{
						bullseyeTargets.RunWithoutExiting(bullseyeArgs, messageOnly: IsMessageOnlyException);
					}
					catch (TargetFailedException)
					{
						return 1;
					}
					catch (Exception exception) when (IsMessageOnlyException(exception))
					{
						Console.Error.WriteLine(exception.Message);
						return 2;
					}
				}

				return 0;
			});

			try
			{
				return commandLineApp.Execute(args);
			}
			catch (Exception exception) when (IsMessageOnlyException(exception))
			{
				Console.Error.WriteLine(exception.Message);
				return 2;
			}
		}

		private static bool IsMessageOnlyException(Exception exception) =>
			exception is ApplicationException || exception is BuildException || exception is CommandParsingException || exception is InvalidUsageException;

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
