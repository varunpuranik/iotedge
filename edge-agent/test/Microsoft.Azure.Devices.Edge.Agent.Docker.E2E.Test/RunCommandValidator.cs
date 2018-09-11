namespace Microsoft.Azure.Devices.Edge.Agent.Docker.E2E.Test
{
    using System.Collections.Generic;
    using System.Diagnostics;

    public class RunCommandValidator : Validator
    {
        public string Command { get; set; }

        public string Args { get; set; }

        public Dictionary<string, string> Env { get; set; }

        public string OutputEquals { get; set; }

        public RunCommandValidator()
        {
            this.Type = ValidatorType.RunCommand;
        }

        public override bool Validate()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = this.Command,
                    Arguments = this.Args,
                    RedirectStandardOutput = true
                }
            };

            if (this.Env != null)
            {
                foreach (var kvp in this.Env)
                {
                    process.StartInfo.Environment[kvp.Key] = kvp.Value;
                }
            }

            string output = "";
            process.OutputDataReceived += (sender, args) => output += args.Data;
            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();

            return process.ExitCode == 0 && output == this.OutputEquals;
        }
    }
}
