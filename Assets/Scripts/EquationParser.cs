using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class EquationParser
{
    private string input;
    private int pos;

    public Func<float, float, float> Parse(string equation)
    {
        // Normalize input
        input = equation
            .ToLower()
            .Replace(" ", "")
            .Replace("**", "^");

        pos = 0;
        var expr = ParseExpression();

        if (pos != input.Length)
            throw new Exception($"Unexpected character at position {pos}: '{input[pos]}'");

        return (x, y) =>
        {
            try { return expr(x, y); }
            catch { return 0f; }
        };
    }

  

    private Func<float, float, float> ParseExpression()
    {
        var left = ParseTerm();

        while (pos < input.Length && (input[pos] == '+' || input[pos] == '-'))
        {
            char op = input[pos++];
            var right = ParseTerm();
            var l = left; var r = right; // capture for lambda
            if (op == '+') left = (x, y) => l(x, y) + r(x, y);
            else            left = (x, y) => l(x, y) - r(x, y);
        }

        return left;
    }

    private Func<float, float, float> ParseTerm()
    {
        var left = ParsePower();

        // A "term" is a chain of things multiplied or divided together. We keep
        // grabbing factors until the next character clearly isn't part of one.
        while (pos < input.Length)
        {
            char c = input[pos];

            if (c == '*' || c == '/')
            {
                // Explicit multiplication or division, e.g. "2 * y".
                pos++; // consume the operator
                var right = ParsePower();
                var l = left; var r = right;
                if (c == '*') left = (x, y) => l(x, y) * r(x, y);
                else          left = (x, y) => { float rv = r(x, y); return rv == 0 ? 0f : l(x, y) / rv; };
            }
            else if (StartsFactor(c))
            {
                // IMPLICIT multiplication: two factors written side by side with no
                // operator between them, the way you'd write it by hand. Examples:
                //   2y        -> 2 * y
                //   3sin(x)   -> 3 * sin(x)
                //   x(x+1)    -> x * (x + 1)
                // We treat this exactly like a '*' but without consuming any character.
                var right = ParsePower();
                var l = left; var r = right;
                left = (x, y) => l(x, y) * r(x, y);
            }
            else
            {
                break; // next character is '+', '-', ')', ',' or end — term is done
            }
        }

        return left;
    }

    /// <summary>
    /// True if 'c' could begin a new factor: a number, a variable/function/constant
    /// name, or an opening parenthesis. Used to detect implicit multiplication.
    /// Note: a letter directly after a letter (like "xy") is read as ONE name, so
    /// write "x*y" for that case — only number/paren boundaries multiply implicitly.
    /// </summary>
    private bool StartsFactor(char c)
    {
        return char.IsLetterOrDigit(c) || c == '.' || c == '(';
    }

    private Func<float, float, float> ParsePower()
    {
        var baseExpr = ParseUnary();

        if (pos < input.Length && input[pos] == '^')
        {
            pos++; // consume '^'
            var expExpr = ParseUnary(); // right-associative
            var b = baseExpr; var e = expExpr;
            return (x, y) => Mathf.Pow(b(x, y), e(x, y));
        }

        return baseExpr;
    }

    private Func<float, float, float> ParseUnary()
    {
        if (pos < input.Length && input[pos] == '-')
        {
            pos++;
            var inner = ParsePrimary();
            return (x, y) => -inner(x, y);
        }
        if (pos < input.Length && input[pos] == '+')
        {
            pos++;
        }
        return ParsePrimary();
    }

    private Func<float, float, float> ParsePrimary()
    {
        if (pos >= input.Length)
            throw new Exception("Unexpected end of expression.");

        // Parenthesized expression
        if (input[pos] == '(')
        {
            pos++; // consume '('
            var inner = ParseExpression();
            if (pos >= input.Length || input[pos] != ')')
                throw new Exception("Missing closing parenthesis.");
            pos++; // consume ')'
            return inner;
        }

        // Number literal
        if (char.IsDigit(input[pos]) || input[pos] == '.')
        {
            return ParseNumber();
        }

        // Identifier: variable or function
        if (char.IsLetter(input[pos]))
        {
            return ParseIdentifier();
        }

        throw new Exception($"Unexpected character '{input[pos]}' at position {pos}.");
    }

    private Func<float, float, float> ParseNumber()
    {
        int start = pos;
        while (pos < input.Length && (char.IsDigit(input[pos]) || input[pos] == '.'))
            pos++;

        float value = float.Parse(input.Substring(start, pos - start),
                                  System.Globalization.CultureInfo.InvariantCulture);
        return (x, y) => value;
    }

    private Func<float, float, float> ParseIdentifier()
    {
        int start = pos;
        while (pos < input.Length && (char.IsLetterOrDigit(input[pos]) || input[pos] == '_'))
            pos++;

        string name = input.Substring(start, pos - start);

        // Constants
        if (name == "pi")  return (x, y) => Mathf.PI;
        if (name == "e")   return (x, y) => (float)Math.E;

        // Variables
        if (name == "x")   return (x, y) => x;
        if (name == "y")   return (x, y) => y;

        // Functions — must be followed by '('
        if (pos < input.Length && input[pos] == '(')
        {
            pos++; // consume '('

            // Functions with two arguments
            if (name == "pow" || name == "atan2" || name == "log")
            {
                var arg1 = ParseExpression();
                if (pos >= input.Length || input[pos] != ',')
                    throw new Exception($"Expected ',' in {name}(a, b).");
                pos++; // consume ','
                var arg2 = ParseExpression();
                if (pos >= input.Length || input[pos] != ')')
                    throw new Exception($"Missing ')' after {name}.");
                pos++; // consume ')'

                var a1 = arg1; var a2 = arg2;
                if (name == "pow")   return (x, y) => Mathf.Pow(a1(x, y), a2(x, y));
                if (name == "atan2") return (x, y) => Mathf.Atan2(a1(x, y), a2(x, y));
                if (name == "log")   return (x, y) => (float)Math.Log(a1(x, y), a2(x, y));
            }

            // Single argument functions
            var arg = ParseExpression();
            if (pos >= input.Length || input[pos] != ')')
                throw new Exception($"Missing ')' after {name}.");
            pos++; // consume ')'

            var a = arg;
            return name switch
            {
                "sin"   => (x, y) => Mathf.Sin(a(x, y)),
                "cos"   => (x, y) => Mathf.Cos(a(x, y)),
                "tan"   => (x, y) => Mathf.Tan(a(x, y)),
                "asin"  => (x, y) => Mathf.Asin(a(x, y)),
                "acos"  => (x, y) => Mathf.Acos(a(x, y)),
                "atan"  => (x, y) => Mathf.Atan(a(x, y)),
                "sqrt"  => (x, y) => Mathf.Sqrt(Mathf.Abs(a(x, y))),
                "abs"   => (x, y) => Mathf.Abs(a(x, y)),
                "exp"   => (x, y) => Mathf.Exp(a(x, y)),
                "log"   => (x, y) => Mathf.Log(Mathf.Abs(a(x, y))),
                "log10" => (x, y) => Mathf.Log10(Mathf.Abs(a(x, y))),
                "ceil"  => (x, y) => Mathf.Ceil(a(x, y)),
                "floor" => (x, y) => Mathf.Floor(a(x, y)),
                "round" => (x, y) => Mathf.Round(a(x, y)),
                "sign"  => (x, y) => Mathf.Sign(a(x, y)),
                _ => throw new Exception($"Unknown function: '{name}'")
            };
        }

        throw new Exception($"Unknown identifier: '{name}'");
    }
}
