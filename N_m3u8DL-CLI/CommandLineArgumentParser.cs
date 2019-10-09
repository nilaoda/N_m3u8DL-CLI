using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N_m3u8DL_CLI
{
    public class CommandLineArgumentParser
    {

        List<CommandLineArgument> _arguments;
        public static CommandLineArgumentParser Parse(string[] args)
        {
            return new CommandLineArgumentParser(args);
        }

        public CommandLineArgumentParser(string[] args)
        {
            _arguments = new List<CommandLineArgument>();

            for (int i = 0; i < args.Length; i++)
            {
                _arguments.Add(new CommandLineArgument(_arguments, i, args[i]));
            }

        }

        public CommandLineArgument Get(string argumentName)
        {
            return _arguments.FirstOrDefault(p => p == argumentName);
        }

        public bool Has(string argumentName)
        {
            return _arguments.Count(p => p == argumentName) > 0;
        }
    }
}
