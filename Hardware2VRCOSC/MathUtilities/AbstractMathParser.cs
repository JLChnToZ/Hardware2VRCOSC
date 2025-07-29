using System;
using System.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace MathUtilities {
    public abstract partial class AbstractMathEvalulator<TNumber> where TNumber : unmanaged, IComparable<TNumber> {
        static readonly IReadOnlyDictionary<string, TokenType> tokenMap = ImmutableDictionary.CreateRange(
            StringComparer.OrdinalIgnoreCase,
            new KeyValuePair<string, TokenType>[] {
                new("+", TokenType.Add),
                new("-", TokenType.Subtract),
                new("*", TokenType.Multiply),
                new("/", TokenType.Divide),
                new("%", TokenType.Modulo),
                new(">", TokenType.GreaterThan),
                new(">=", TokenType.GreaterThanOrEquals),
                new("<", TokenType.LessThan),
                new("<=", TokenType.LessThanOrEquals),
                new("==", TokenType.Equals),
                new("!=", TokenType.NotEquals),
                new("&&", TokenType.LogicalAnd),
                new("||", TokenType.LogicalOr),
                new("!", TokenType.LogicalNot),
                new("&", TokenType.BitwiseAnd),
                new("|", TokenType.BitwiseOr),
                new("^", TokenType.BitwiseXor),
                new("~", TokenType.BitwiseNot),
                new("<<", TokenType.LeftShift),
                new(">>", TokenType.RightShift),
                new("?", TokenType.If),
                new(":", TokenType.Else),
                new("(", TokenType.LeftParenthesis),
                new(")", TokenType.RightParenthesis),
                new(",", TokenType.Comma),
            }
        );
        static readonly byte[] precedences;
        static readonly bool[] rtlOperators;
        StringBuilder? sb;
        LightWeightDeque<Token> ops;
        LightWeightDeque<Token> result;
        Token[]? tokens;

        public Token[]? Tokens {
            get => tokens;
            set => tokens = value ?? Array.Empty<Token>();
        }

        static AbstractMathEvalulator() {
            precedences = new byte[256];
            rtlOperators = new bool[256];
            precedences[(int)TokenType.LeftParenthesis] = 0;
            precedences[(int)TokenType.RightParenthesis] = 0;
            rtlOperators[(int)TokenType.External] = true;
            precedences[(int)TokenType.External] = 1;
            rtlOperators[(int)TokenType.UnaryPlus] = true;
            precedences[(int)TokenType.UnaryPlus] = 1;
            rtlOperators[(int)TokenType.UnaryMinus] = true;
            precedences[(int)TokenType.UnaryMinus] = 1;
            rtlOperators[(int)TokenType.LogicalNot] = true;
            precedences[(int)TokenType.LogicalNot] = 1;
            rtlOperators[(int)TokenType.BitwiseNot] = true;
            precedences[(int)TokenType.BitwiseNot] = 1;
            precedences[(int)TokenType.Multiply] = 2;
            precedences[(int)TokenType.Divide] = 2;
            precedences[(int)TokenType.Modulo] = 2;
            precedences[(int)TokenType.Add] = 3;
            precedences[(int)TokenType.Subtract] = 3;
            precedences[(int)TokenType.LeftShift] = 4;
            precedences[(int)TokenType.RightShift] = 4;
            precedences[(int)TokenType.GreaterThan] = 5;
            precedences[(int)TokenType.GreaterThanOrEquals] = 5;
            precedences[(int)TokenType.LessThan] = 5;
            precedences[(int)TokenType.LessThanOrEquals] = 5;
            precedences[(int)TokenType.Equals] = 6;
            precedences[(int)TokenType.NotEquals] = 6;
            precedences[(int)TokenType.BitwiseAnd] = 7;
            precedences[(int)TokenType.BitwiseXor] = 8;
            precedences[(int)TokenType.BitwiseOr] = 9;
            precedences[(int)TokenType.LogicalAnd] = 10;
            precedences[(int)TokenType.LogicalOr] = 11;
            precedences[(int)TokenType.If] = 12;
            precedences[(int)TokenType.Else] = 13;
            precedences[(int)TokenType.Comma] = 15;
        }

        public void Parse(string expression) => ShuntingYard(Tokenize(expression));

        IEnumerable<Token> Tokenize(IEnumerable<char> expression) {
            sb ??= new StringBuilder();
            var type = ParseType.Unknown;
            var previousType = ParseType.Unknown;
            var previousTokenType = TokenType.Unknown;
            bool preivousWhiteSpace = false;
            foreach (var c in expression)
                switch (preivousWhiteSpace ? ParseType.Unknown : type) {
                    case ParseType.Number:
                        if (c == '.') {
                            previousType = type;
                            type = ParseType.NumberWithDot;
                            sb.Append(c);
                            break;
                        }
                        goto case ParseType.NumberWithDot;
                    case ParseType.NumberWithDot:
                        if (char.IsDigit(c)) {
                            sb.Append(c);
                            break;
                        }
                        goto default;
                    case ParseType.Identifier:
                        if (char.IsLetterOrDigit(c) || c == '_' || c == '.') {
                            sb.Append(c);
                            break;
                        }
                        goto default;
                    case ParseType.Operator:
                        if ((char.IsSymbol(c) || char.IsPunctuation(c)) && c != '.') {
                            sb.Append(c);
                            break;
                        }
                        goto default;
                    default:
                        foreach (var token in YieldTokens(previousTokenType, previousType, type)) {
                            yield return token;
                            previousTokenType = token.type;
                        }
                        if (char.IsWhiteSpace(c)) {
                            preivousWhiteSpace = true;
                            break;
                        }
                        preivousWhiteSpace = false;
                        previousType = type;
                        sb.Append(c);
                        switch (c) {
                            case '.': type = ParseType.NumberWithDot; break;
                            case '_': type = ParseType.Identifier; break;
                            default:
                                if (char.IsDigit(c)) {
                                    type = ParseType.Number;
                                    break;
                                }
                                if (char.IsLetter(c)) {
                                    type = ParseType.Identifier;
                                    break;
                                }
                                if (char.IsSymbol(c) || char.IsPunctuation(c)) {
                                    type = ParseType.Operator;
                                    break;
                                }
                                throw new Exception($"Unexpected token '{c}'");
                        }
                        break;
                }
            foreach (var token in YieldTokens(previousTokenType, previousType, type))
                yield return token;
        }

        IEnumerable<Token> YieldTokens(TokenType prevTokenType, ParseType prevType, ParseType type) {
            if (sb!.Length <= 0) yield break;
            var tokenData = sb.ToString();
            sb.Clear();
            switch (type) {
                case ParseType.Number:
                case ParseType.NumberWithDot: {
                        yield return new Token(ParseNumber(tokenData));
                        break;
                    }
                case ParseType.Operator: {
                        for (int offset = 0, length = tokenData.Length; offset < tokenData.Length;) {
                            var token = offset == 0 && length == tokenData.Length ?
                                tokenData : tokenData.Substring(offset, length);
                            if (tokenMap.TryGetValue(token, out var tokenType)) {
                                switch (tokenType) {
                                    case TokenType.Add:
                                        if (offset > 0 || (
                                            prevType != ParseType.Number &&
                                            prevType != ParseType.NumberWithDot &&
                                            prevType != ParseType.Identifier &&
                                            prevTokenType != TokenType.RightParenthesis)) {
                                            yield return new Token(TokenType.UnaryPlus);
                                            prevTokenType = TokenType.UnaryPlus;
                                            break;
                                        }
                                        goto default;
                                    case TokenType.Subtract:
                                        if (offset > 0 || (
                                            prevType != ParseType.Number &&
                                            prevType != ParseType.NumberWithDot &&
                                            prevType != ParseType.Identifier &&
                                            prevTokenType != TokenType.RightParenthesis)) {
                                            yield return new Token(TokenType.UnaryMinus);
                                            prevTokenType = TokenType.UnaryMinus;
                                            break;
                                        }
                                        goto default;
                                    default:
                                        yield return new Token(tokenType);
                                        prevTokenType = tokenType;
                                        break;
                                }
                                offset += length;
                                length = tokenData.Length - offset;
                            } else if (--length <= 0)
                                throw new Exception($"Unknown operator '{tokenData.Substring(offset)}'");
                        }
                        break;
                    }
                case ParseType.Identifier: {
                        if (tokenMap.TryGetValue(tokenData, out var tokenType))
                            yield return new Token(tokenType);
                        else
                            yield return new Token(TokenType.Identifier, tokenData);
                        break;
                    }
            }
        }

        void ShuntingYard(IEnumerable<Token> tokens) {
            try {
                Token lastToken = default;
                foreach (var token in tokens) {
                    switch (token.type) {
                        case TokenType.Primitive:
                            PushPreviousIdentifier(ref lastToken, true);
                            result.Add(token);
                            break;
                        case TokenType.Identifier:
                            PushPreviousIdentifier(ref lastToken, true);
                            lastToken = token;
                            break;
                        case TokenType.LeftParenthesis:
                            PushPreviousIdentifier(ref lastToken, true);
                            ops.Add(token);
                            break;
                        case TokenType.Comma:
                        case TokenType.RightParenthesis:
                            PushPreviousIdentifier(ref lastToken, false);
                            while (ops.Count > 0) {
                                var top = ops.PeekLast();
                                if (top.type == TokenType.If)
                                    throw new Exception("Mismatched parenthesis");
                                if (top.type == TokenType.LeftParenthesis) break;
                                result.Add(top);
                                ops.PopAndDiscard();
                            }
                            if (ops.Count == 0)
                                throw new Exception("Mismatched parenthesis");
                            if (token.type != TokenType.Comma) ops.PopAndDiscard();
                            break;
                        case TokenType.Else:
                            PushPreviousIdentifier(ref lastToken, false);
                            while (ops.Count > 0) {
                                var top = ops.Pop();
                                if (top.type == TokenType.LeftParenthesis)
                                    throw new Exception("Mismatched parenthesis");
                                result.Add(top);
                                if (top.type == TokenType.If) break;
                            }
                            ops.Add(token);
                            break;
                        default:
                            PushPreviousIdentifier(ref lastToken, false);
                            if (!rtlOperators[(int)token.type])
                                while (ops.Count > 0) {
                                    var top = ops.PeekLast();
                                    if (precedences[(int)top.type] > precedences[(int)token.type] ||
                                        (precedences[(int)top.type] == precedences[(int)token.type] &&
                                        !rtlOperators[(int)top.type])) break;
                                    result.Add(top);
                                    ops.Pop();
                                }
                            ops.Add(token);
                            break;
                    }
                }
                PushPreviousIdentifier(ref lastToken, false);
                while (ops.Count > 0) {
                    var token = ops.Pop();
                    switch (token.type) {
                        case TokenType.LeftParenthesis:
                            throw new Exception("Mismatched parenthesis");
                        default:
                            result.Add(token);
                            break;
                    }
                }
                this.tokens = new Token[result.Count];
                result.CopyTo(this.tokens, 0);
            } finally {
                ops.Clear();
                result.Clear();
            }
        }

        void PushPreviousIdentifier(ref Token lastToken, bool treatAsExternal) {
            if (lastToken.type != TokenType.Identifier) return;
            if (treatAsExternal) {
                ops.Add(new Token(TokenType.External, lastToken.identifierValue));
                result.Add(new Token(TokenType.LeftParenthesis));
            } else
                result.Add(lastToken);
            lastToken = default;
        }

        public override string ToString() {
            if (tokens == null || tokens.Length <= 0) return "";
            sb ??= new StringBuilder();
            try {
                foreach (var token in tokens)
                    switch (token.type) {
                        case TokenType.Unknown: sb.Append("? "); break;
                        case TokenType.Primitive:
                            sb.Append(token.numberValue).Append(' ');
                            break;
                        case TokenType.Identifier:
                            sb.Append('`').Append(token.identifierValue).Append("` ");
                            break;
                        case TokenType.External:
                            sb.Append(':').Append(token.identifierValue).Append(") ");
                            break;
                        case TokenType.LeftParenthesis:
                            sb.Append('(');
                            break;
                        case TokenType.Comma:
                            sb.Append(',');
                            break;
                        case TokenType.RightParenthesis:
                            sb.Append(") ");
                            break;
                        default:
                            sb.Append('~').Append(token.type).Append(' ');
                            break;
                    }
                return sb.ToString();
            } finally {
                sb.Clear();
            }
        }

        protected abstract TNumber ParseNumber(string value);

        enum ParseType : byte {
            Unknown, Number, NumberWithDot, Identifier, Operator,
        }

        [Serializable]
        public struct Token : IEquatable<Token> {
            public TokenType type;
            public TNumber numberValue;
            public string? identifierValue;

            public Token(TokenType type) {
                this.type = type;
                identifierValue = type == TokenType.Identifier || type == TokenType.External ? "" : null;
                numberValue = default;
            }

            public Token(TokenType type, string? identifierValue) {
                this.type = type;
                this.identifierValue = identifierValue ?? "";
                numberValue = default;
            }

            public Token(TNumber numberValue) {
                type = TokenType.Primitive;
                identifierValue = null;
                this.numberValue = numberValue;
            }

            public readonly bool Equals(Token other) =>
                type == other.type &&
                ((type != TokenType.Identifier && type != TokenType.External) || identifierValue == other.identifierValue) &&
                (type != TokenType.Primitive || EqualityComparer<TNumber>.Default.Equals(numberValue, other.numberValue));

            public override readonly bool Equals(object? obj) => obj is Token other && Equals(other);

            public override readonly int GetHashCode() {
                unchecked {
                    int hashCode = (int)type;
                    if (type == TokenType.Identifier || type == TokenType.External)
                        hashCode = (hashCode * 397) ^ (identifierValue != null ? identifierValue.GetHashCode() : 0);
                    else if (type == TokenType.Primitive)
                        hashCode = (hashCode * 397) ^ numberValue.GetHashCode();
                    return hashCode;
                }
            }
            public override string ToString() {
                switch (type) {
                    case TokenType.Primitive: return $"[{typeof(TNumber).Name}] {numberValue}";
                    case TokenType.Identifier: return $"[Identifier] {identifierValue}";
                    case TokenType.External: return $"[External] {identifierValue}";
                    default: return $"[{type}]";
                }
            }
            public static bool operator ==(Token left, Token right) => left.Equals(right);

            public static bool operator !=(Token left, Token right) => !left.Equals(right);
        }
    }

    public enum TokenType : byte {
        Unknown, Primitive, Identifier, External,
        UnaryPlus, UnaryMinus,
        Add, Subtract, Multiply, Divide, Modulo,
        LogicalAnd, LogicalOr, LogicalNot,
        BitwiseAnd, BitwiseOr, BitwiseXor, BitwiseNot,
        LeftShift, RightShift,
        GreaterThan, GreaterThanOrEquals,
        LessThan, LessThanOrEquals,
        Equals, NotEquals,
        If, Else,
        LeftParenthesis, RightParenthesis, Comma,
    }
}