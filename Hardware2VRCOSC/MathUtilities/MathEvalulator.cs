using System;
using System.Diagnostics.CodeAnalysis;

namespace MathUtilities {
    public class MathEvalulator : AbstractMathEvalulator<double> {
        Random? randomProvider;

        public Random? RandomProvider {
            get => randomProvider;
            set => randomProvider = value;
        }

        protected override double Truely => 1;

        protected override double Error => double.NaN;

        static bool IsSafeInteger(double value) => value >= int.MinValue && value <= int.MaxValue;

        protected override double ParseNumber(string value) => double.Parse(value);

        protected override bool IsTruely(double value) => value != 0 && !double.IsNaN(value);

        #region Operators
        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor(TokenType.Add)] static double Add(double first, double second) => first + second;

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor(TokenType.Subtract)] static double Subtract(double first, double second) => first - second;

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor(TokenType.Multiply)] static double Multiply(double first, double second) => first * second;

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor(TokenType.Divide)] static double Divide(double first, double second) => first / second;

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor(TokenType.Modulo)] static double Modulo(double first, double second) => first % second;

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor(TokenType.UnaryPlus)] static double UnaryPlus(double value) => value;

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor(TokenType.UnaryMinus)] static double UnaryMinus(double value) => -value;

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor(TokenType.BitwiseAnd)]
        static double BitwiseAnd(double first, double second) =>
            IsSafeInteger(first) && IsSafeInteger(second) ? unchecked((int)first & (int)second) : double.NaN;

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor(TokenType.BitwiseOr)]
        static double BitwiseOr(double first, double second) =>
            IsSafeInteger(first) && IsSafeInteger(second) ? unchecked((int)first | (int)second) : double.NaN;

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor(TokenType.BitwiseXor)]
        static double BitwiseXor(double first, double second) =>
            IsSafeInteger(first) && IsSafeInteger(second) ? unchecked((int)first ^ (int)second) : double.NaN;

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor(TokenType.BitwiseNot)]
        static double BitwiseNot(double value) =>
            IsSafeInteger(value) ? unchecked((int)value) : double.NaN;

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor(TokenType.LeftShift)]
        static double LeftShift(double value, double shift) =>
            IsSafeInteger(value) && IsSafeInteger(shift) ? unchecked((int)value << (int)shift) : double.NaN;

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor(TokenType.RightShift)]
        static double RightShift(double value, double shift) =>
            IsSafeInteger(value) && IsSafeInteger(shift) ? unchecked((int)value >> (int)shift) : double.NaN;

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("abs")] static double Abs(Span<double> args) => Math.Abs(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("sqrt")] static double Sqrt(Span<double> args) => Math.Sqrt(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("cbrt")] static double Cbrt(Span<double> args) => Math.Cbrt(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("pow")] static double Pow(Span<double> args) => Math.Pow(args[0], args[1]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("lerp")] static double Lerp(Span<double> args) => args[0] + (args[1] - args[0]) * args[2];

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("remap")] static double Remap(Span<double> args) => args[3] + (args[4] - args[3]) * (args[0] - args[1]) / (args[2] - args[1]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("saturate")] static double Saturate(Span<double> args) => Math.Clamp(args[0], 0, 1);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("sign")] static double Sign(Span<double> args) => Math.Sign(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("round")] static double Round(Span<double> args) => Math.Round(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("floor")] static double Floor(Span<double> args) => Math.Floor(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("ceil")] static double Ceil(Span<double> args) => Math.Ceiling(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("trunc")] static double Trunc(Span<double> args) => Math.Truncate(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("sin")] static double Sin(Span<double> args) => Math.Sin(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("cos")] static double Cos(Span<double> args) => Math.Cos(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("tan")] static double Tan(Span<double> args) => Math.Tan(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("asin")] static double Asin(Span<double> args) => Math.Asin(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("acos")] static double Acos(Span<double> args) => Math.Acos(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("atan")]
        static double Atan(Span<double> args) {
            return args.Length switch {
                1 => Math.Atan(args[0]),
                2 => Math.Atan2(args[0], args[1]),
                _ => double.NaN,
            };
        }

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("sinh")] static double Sinh(Span<double> args) => Math.Sinh(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("cosh")] static double Cosh(Span<double> args) => Math.Cosh(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("tanh")] static double Tanh(Span<double> args) => Math.Tanh(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("asinh")] static double Asinh(Span<double> args) => Math.Asinh(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("acosh")] static double Acosh(Span<double> args) => Math.Acosh(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("atanh")] static double Atanh(Span<double> args) => Math.Atanh(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("log")]
        static double Log(Span<double> args) => args.Length switch {
            1 => Math.Log(args[0]),
            2 => Math.Log(args[0], args[1]),
            _ => double.NaN,
        };

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("exp")] static double Exp(Span<double> args) => Math.Exp(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("log10")] static double Log10(Span<double> args) => Math.Log10(args[0]);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("log2")] static double Log2(Span<double> args) => Math.Log(args[0], 2);

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("random")]
        double Random(Span<double> args) {
            randomProvider ??= new Random();
            return args.Length switch {
                0 => randomProvider.NextDouble(),
                1 => randomProvider.Next((int)args[0]),
                2 => randomProvider.Next((int)args[0], (int)args[1]),
                _ => double.NaN,
            };
        }

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("isnan")] static double IsNaN(Span<double> args) => double.IsNaN(args[0]) ? 1F : 0F;

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used by reflection")]
        [Processor("switch")]
        static double Switch(Span<double> args) {
            var index = args[0];
            return args.Length > 1 && double.IsFinite(index) ?
                args[(int)(index % (args.Length - 1) + (index < 0 ? args.Length : 1))] :
                double.NaN;
        }
        #endregion
    }
}