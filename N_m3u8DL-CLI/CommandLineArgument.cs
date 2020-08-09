using System.Collections.Generic;

namespace N_m3u8DL_CLI
{
    /**
     * https://www.cnblogs.com/linxuanchen/p/c-sharp-command-line-argument-parser.html
     */
    public class CommandLineArgument
    {
        List<CommandLineArgument> _arguments;

        int _index;

        string _argumentText;

        public CommandLineArgument Next
        {
            get
            {
                if (_index < _arguments.Count - 1)
                {
                    return _arguments[_index + 1];
                }

                return null;
            }
        }
        public CommandLineArgument Previous
        {
            get
            {
                if (_index > 0)
                {
                    return _arguments[_index - 1];
                }

                return null;
            }
        }
        internal CommandLineArgument(List<CommandLineArgument> args, int index, string argument)
        {
            _arguments = args;
            _index = index;
            _argumentText = argument;
        }

        public CommandLineArgument Take()
        {
            return Next;
        }

        public IEnumerable<CommandLineArgument> Take(int count)
        {
            var list = new List<CommandLineArgument>();
            var parent = this;
            for (int i = 0; i < count; i++)
            {
                var next = parent.Next;
                if (next == null)
                    break;

                list.Add(next);

                parent = next;
            }

            return list;
        }

        public static implicit operator string(CommandLineArgument argument)
        {
            return argument._argumentText;
        }

        public override string ToString()
        {
            return _argumentText;
        }
    }
}
