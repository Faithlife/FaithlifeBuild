using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Bullseye;
using McMaster.Extensions.CommandLineUtils;

namespace Faithlife.Build;

/// <summary>
/// Used to execute an automated build.
/// </summary>
[SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Console.WriteLine.")]
public static class BuildRunner
{
	/// <summary>
	/// Executes an automated build. Called from <c>Main</c>.
	/// </summary>
	/// <param name="args">The command-line arguments from <c>Main</c>.</param>
	/// <param name="initialize">Called to initialize the build.</param>
	/// <returns>The exit code for the build.</returns>
	public static int Execute(string[] args, Action<BuildApp> initialize) =>
		ExecuteAsync(args, initialize).GetAwaiter().GetResult();

	/// <summary>
	/// Executes an automated build. Called from <c>Main</c>.
	/// </summary>
	/// <param name="args">The command-line arguments from <c>Main</c>.</param>
	/// <param name="initialize">Called to initialize the build.</param>
	/// <returns>The exit code for the build.</returns>
	public static async Task<int> ExecuteAsync(string[] args, Action<BuildApp> initialize)
	{
		ArgumentNullException.ThrowIfNull(args);
		ArgumentNullException.ThrowIfNull(initialize);

		if (Assembly.GetEntryAssembly()?.EntryPoint?.ReturnType == typeof(void))
		{
			Console.Error.WriteLine("Application entry point returns void; it should return the result of BuildRunner.Execute.");
			return 2;
		}

		var commandLineApp = new CommandLineApplication();

		var buildApp = new BuildApp(commandLineApp);
		try
		{
			initialize(buildApp);
		}
		catch (Exception exception) when (IsMessageOnlyException(exception))
		{
			Console.Error.WriteLine(exception.Message);
			return 2;
		}

		var dryRunFlag = buildApp.AddFlag("-n|--dry-run", "Don't execute target actions");
		var skipDependenciesFlag = buildApp.AddFlag("-s|--skip-dependencies", "Don't run any target dependencies");
		var skipOption = buildApp.AddOption("--skip <targets>", "Skip the comma-delimited target dependencies");
		var parallelFlag = buildApp.AddFlag("--parallel", "Run targets in parallel");
		var noColorFlag = buildApp.AddFlag("--no-color", "Disable color output");
		var showTreeFlag = buildApp.AddFlag("--show-tree", "Show the dependency tree");
		var verboseFlag = buildApp.AddFlag("--verbose", "Show verbose output");
		var helpFlag = buildApp.AddFlag("-h|-?|--help", "Show build help");
		var targetsArgument = commandLineApp.Argument("targets", "The targets to build", multipleValues: true);

		var bullseyeTargets = new Targets();
		foreach (var target in buildApp.Targets)
			bullseyeTargets.Add(name: target.Name, description: target.Description, dependsOn: target.Dependencies, action: target.RunAsync);

		commandLineApp.OnExecuteAsync(async _ =>
		{
			var targetNames = targetsArgument.Values.WhereNotNull().ToList();

			if (targetNames.Count == 0 && buildApp.Targets.Any(x => x.Name == c_defaultTarget))
				targetNames.Add(c_defaultTarget);

			var skipDependencies = skipDependenciesFlag.Value;
			if (skipOption.Value is not null && !skipDependencies)
			{
				var skipTargetNames = new HashSet<string>(skipOption.Value.Split(',', StringSplitOptions.RemoveEmptyEntries));
				var targetNamesWithDependencies = new List<string>();

				void AddTargetAndDependencies(BuildTarget target)
				{
					if (!skipTargetNames.Contains(target.Name) && !targetNamesWithDependencies.Contains(target.Name))
					{
						foreach (var dependency in target.Dependencies.Select(name => buildApp.Targets.FirstOrDefault(x => x.Name == name)).WhereNotNull())
							AddTargetAndDependencies(dependency);

						targetNamesWithDependencies.Add(target.Name);
					}
				}

				foreach (var targetName in targetNames)
				{
					var target = buildApp.Targets.FirstOrDefault(x => x.Name == targetName);
					if (target is not null)
						AddTargetAndDependencies(target);
				}

				targetNames = targetNamesWithDependencies;
				skipDependencies = true;
			}

			if (helpFlag.Value || (targetNames.Count == 0 && !showTreeFlag.Value))
			{
				commandLineApp.ShowHelp(usePager: false);
				ShowTargets(buildApp.Targets);
			}
			else
			{
				var bullseyeArgs = targetNames.ToList();
				if (noColorFlag.Value)
					bullseyeArgs.Add("--no-color");
				if (skipDependencies)
					bullseyeArgs.Add("--skip-dependencies");
				if (showTreeFlag.Value)
					bullseyeArgs.Add("--list-tree");
				if (dryRunFlag.Value)
					bullseyeArgs.Add("--dry-run");
				if (parallelFlag.Value)
					bullseyeArgs.Add("--parallel");
				if (verboseFlag.Value)
					bullseyeArgs.Add("--verbose");
				bullseyeArgs.Add("--no-extended-chars");

				try
				{
					await bullseyeTargets.RunWithoutExitingAsync(bullseyeArgs, messageOnly: IsMessageOnlyException).ConfigureAwait(false);
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
			return await commandLineApp.ExecuteAsync(args).ConfigureAwait(false);
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
