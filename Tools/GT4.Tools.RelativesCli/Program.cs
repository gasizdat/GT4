using GT4.Tools.RelativesCli;

try
{
  return await Cli.RunAsync(args, CancellationToken.None);
}
catch (Exception ex)
{
  Console.Error.WriteLine(ex.Message);
  return 1;
}
