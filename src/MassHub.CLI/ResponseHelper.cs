using System;
using System.Collections.Generic;
using System.Linq;

namespace MassHub.CLI
{
    internal static class ResponseHelper
    {
        internal static bool? AskYesNoOrDefaultResponse(string input)
        {
            Console.WriteLine(input);

            var response = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(response))
            {
                return null;
            }

            if (response.Equals("y", StringComparison.CurrentCultureIgnoreCase)
                || response.Equals("yes", StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            if (response.Equals("n", StringComparison.CurrentCultureIgnoreCase)
                || response.Equals("no", StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }

            return null;
        }

        internal static int AskAndWaitForIntResponse(string input)
        {
            int id;

            do
            {
                Console.WriteLine(input);
            } while (!int.TryParse(Console.ReadLine(), out id));

            return id;
        }

        internal static int? AskIntResponse(string input)
        {
            Console.WriteLine(input);

            var response = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(response))
            {
                return null;
            }

            return int.TryParse(response, out var actual) ? actual : (int?)null;
        }

        internal static List<string> AskListResponse(string input)
        {
            Console.WriteLine(input);

            var response = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(response))
            {
                return new List<string>();
            }

            var split = response.Split(',');

            return split.ToList();
        }
    }
}