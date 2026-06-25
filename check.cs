using System.ComponentModel;
using Spectre.Console.Cli;
public class S : CommandSettings {
  [Description("x")]
  public string P { get; set; } = "";
}
