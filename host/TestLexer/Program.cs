using System;
using Osb.Lang.Lexing;
using Osb.Lang.Parsing;

namespace TestInterpreter
{
    class Program
    {
        static void Main()
        {
            string source = "ENUM Color RED GREEN BLUE END ENUM\n\nFUNCTION MAIN()\n    VAR c\n    c = Color.RED\n    PRINT c\nEND FUNCTION";
            Console.WriteLine("Source bytes:");
            foreach (byte b in System.Text.Encoding.UTF8.GetBytes(source))
            {
                Console.Write($"{b:X2} ");
            }
            Console.WriteLine();
            
            var lexer = new Lexer(source);
            var tokens = lexer.Tokenize();
            
            Console.WriteLine("\nTokens:");
            foreach (var tok in tokens)
            {
                Console.WriteLine($"  {tok.Type,-15} '{tok.Lexeme}'");
            }
            
            Console.WriteLine("\nParsing...");
            var parser = new Parser(tokens);
            var program = parser.Parse();
            Console.WriteLine($"Parsed successfully: {program.Enums.Count} enums");
        }
    }
}