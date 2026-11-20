using Osb.Engine.Core.Lang;

namespace CollectionTests;

internal static class TokenTypeSetTests
{
    internal static void RunAll()
    {
        TestEmpty();
        TestMembership();
        TestNonMembership();
        TestDuplicateEntries();
        TestInvalidEnumValues();
        TestAllNineKeywords();
        TestValuesOutsideKeywords();
    }

    private static void TestEmpty()
    {
        var set = new TokenTypeSet();
        Program.Check("TokenTypeSet.Empty.Count", set.Count == 0);
        Program.Check("TokenTypeSet.Empty.Contains", !set.Contains(TokenType.Sqrt));
    }

    private static void TestMembership()
    {
        var set = new TokenTypeSet(TokenType.Sqrt, TokenType.Pow);
        Program.Check("TokenTypeSet.Membership.Sqrt", set.Contains(TokenType.Sqrt));
        Program.Check("TokenTypeSet.Membership.Pow", set.Contains(TokenType.Pow));
    }

    private static void TestNonMembership()
    {
        var set = new TokenTypeSet(TokenType.Sqrt);
        Program.Check("TokenTypeSet.NonMembership", !set.Contains(TokenType.Pow));
        Program.Check("TokenTypeSet.NonMembership2", !set.Contains(TokenType.Str));
    }

    private static void TestDuplicateEntries()
    {
        var set = new TokenTypeSet(TokenType.Sqrt, TokenType.Sqrt, TokenType.Pow);
        Program.Check("TokenTypeSet.Duplicate.Count", set.Count == 3);
        Program.Check("TokenTypeSet.Duplicate.Contains", set.Contains(TokenType.Sqrt));
        Program.Check("TokenTypeSet.Duplicate.Contains2", set.Contains(TokenType.Pow));
    }

    private static void TestInvalidEnumValues()
    {
        var set = new TokenTypeSet((TokenType)999);
        Program.Check("TokenTypeSet.InvalidEnum.Contains", set.Contains((TokenType)999));
        Program.Check("TokenTypeSet.InvalidEnum.NonContains", !set.Contains(TokenType.Sqrt));
    }

    private static void TestAllNineKeywords()
    {
        var set = new TokenTypeSet(
            TokenType.Sqrt, TokenType.Pow, TokenType.Floor, TokenType.Ceil,
            TokenType.Str, TokenType.Number, TokenType.Bool, TokenType.Count, TokenType.TypeOf
        );
        Program.Check("TokenTypeSet.Nine.Count", set.Count == 9);
        Program.Check("TokenTypeSet.Nine.Sqrt", set.Contains(TokenType.Sqrt));
        Program.Check("TokenTypeSet.Nine.Pow", set.Contains(TokenType.Pow));
        Program.Check("TokenTypeSet.Nine.Floor", set.Contains(TokenType.Floor));
        Program.Check("TokenTypeSet.Nine.Ceil", set.Contains(TokenType.Ceil));
        Program.Check("TokenTypeSet.Nine.Str", set.Contains(TokenType.Str));
        Program.Check("TokenTypeSet.Nine.Number", set.Contains(TokenType.Number));
        Program.Check("TokenTypeSet.Nine.Bool", set.Contains(TokenType.Bool));
        Program.Check("TokenTypeSet.Nine.CountTok", set.Contains(TokenType.Count));
        Program.Check("TokenTypeSet.Nine.TypeOf", set.Contains(TokenType.TypeOf));
    }

    private static void TestValuesOutsideKeywords()
    {
        var set = new TokenTypeSet(
            TokenType.Sqrt, TokenType.Pow, TokenType.Floor, TokenType.Ceil,
            TokenType.Str, TokenType.Number, TokenType.Bool, TokenType.Count, TokenType.TypeOf
        );
        Program.Check("TokenTypeSet.Outside.LParen", !set.Contains(TokenType.LParen));
        Program.Check("TokenTypeSet.Outside.RParen", !set.Contains(TokenType.RParen));
        Program.Check("TokenTypeSet.Outside.Plus", !set.Contains(TokenType.Plus));
        Program.Check("TokenTypeSet.Outside.Identifier", !set.Contains(TokenType.Identifier));
    }
}
