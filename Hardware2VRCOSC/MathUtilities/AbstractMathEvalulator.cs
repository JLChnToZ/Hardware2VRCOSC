using System;
using System.Collections.Generic;
using System.Reflection;

namespace MathUtilities {
    public partial class AbstractMathEvalulator<TNumber> {
        public delegate TNumber FunctionProcessor(Span<TNumber> arguments);
        public delegate TNumber UnaryOperatorProcessor(TNumber value);
        public delegate TNumber BinaryOperatorProcessor(TNumber value, TNumber value2);

        LightWeightDeque<TNumber> valueStack;
        LightWeightDeque<bool> conditionStack;
        LightWeightDeque<int> argumentStack;
        IDictionary<string, TNumber>? variables;
        IDictionary<string, Func<TNumber>>? functionProcessors;
        Func<Token, TNumber>[]? tokenProcessors;
        IComparer<TNumber>? comparer;
        Func<string, TNumber>? getVariableFunc;

        public IComparer<TNumber>? Comparer {
            get => comparer;
            set => comparer = value ?? Comparer<TNumber>.Default;
        }

        public Func<string, TNumber>? GetVariableFunc {
            get => getVariableFunc;
            set {
                getVariableFunc = value;
                if (value != null) variables = null;
            }
        }

        public IDictionary<string, TNumber>? Variables {
            get {
                if (getVariableFunc != null) return null;
                variables ??= new Dictionary<string, TNumber>(StringComparer.OrdinalIgnoreCase);
                return variables;
            }
            set {
                variables = value;
                if (value != null) getVariableFunc = null;
            }
        }

        protected abstract TNumber Truely { get; }

        protected virtual TNumber Falsy { get; } = default;

        protected virtual TNumber Error { get; } = default;

        public AbstractMathEvalulator() { }

        protected abstract bool IsTruely(TNumber value);

        public TNumber Evaluate() {
            try {
                foreach (var token in tokens!)
                    switch (token.type) {
                        case TokenType.LeftParenthesis:
                            argumentStack.Add(valueStack.Count);
                            break;
                        case TokenType.If: {
                                var value = valueStack.Pop();
                                bool isTrue = IsTruely(valueStack.Pop());
                                if (isTrue) valueStack.Add(value);
                                conditionStack.Add(isTrue);
                                break;
                            }
                        case TokenType.Else:
                            if (conditionStack.Pop()) valueStack.Pop();
                            break;
                        default:
                            if (tokenProcessors == null) break;
                            var processor = tokenProcessors[(int)token.type];
                            if (processor != null) valueStack.Add(processor(token));
                            break;
                    }
                return valueStack.Pop();
            } finally {
                valueStack.Clear();
                conditionStack.Clear();
                argumentStack.Clear();
            }
        }

        public TNumber GetVariable(string? key) {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (getVariableFunc != null) return getVariableFunc(key);
            variables ??= new Dictionary<string, TNumber>(StringComparer.OrdinalIgnoreCase);
            if (variables.TryGetValue(key, out var value)) return value;
            return default;
        }

        public void SetVariable(string key, TNumber value) {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (getVariableFunc != null) throw new InvalidOperationException("Cannot set variable when GetVariableFunc is set");
            variables ??= new Dictionary<string, TNumber>(StringComparer.OrdinalIgnoreCase);
            variables[key] = value;
        }

        public bool RegisterProcessor(string? functionName, FunctionProcessor? processor, bool overrideExisting = true) {
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            return RegisterProcessor(functionName, () => {
                var valuePointer = valueStack.Count;
                if (valuePointer < 1) return Error;
                int marker = argumentStack.Pop();
                if (marker > valuePointer) return Error;
                Span<TNumber> temp = stackalloc TNumber[valuePointer - marker];
                valueStack.Pop(valuePointer - marker).CopyTo(temp);
                return processor(temp);
            }, overrideExisting);
        }

        protected bool RegisterProcessor(string? functionName, Func<TNumber>? processor, bool overrideExisting = true) {
            if (string.IsNullOrEmpty(functionName)) throw new ArgumentNullException(nameof(functionName));
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            functionProcessors ??= new Dictionary<string, Func<TNumber>>(StringComparer.OrdinalIgnoreCase);
            if (functionProcessors.ContainsKey(functionName)) {
                if (!overrideExisting) return false;
                functionProcessors[functionName] = processor;
            } else
                functionProcessors.Add(functionName, processor);
            return true;
        }

        public bool RegisterProcessor(TokenType type, UnaryOperatorProcessor? processor, bool overrideExisting = true) {
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            return RegisterProcessor(type, (Token _) => {
                if (valueStack.Count < 1) return Error;
                return processor(valueStack.Pop());
            }, overrideExisting);
        }

        public bool RegisterProcessor(TokenType type, BinaryOperatorProcessor? processor, bool overrideExisting = true) {
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            return RegisterProcessor(type, (Token _) => {
                if (valueStack.Count < 2) return Error;
                var second = valueStack.Pop();
                var first = valueStack.Pop();
                return processor(first, second);
            }, overrideExisting);
        }

        protected bool RegisterProcessor(TokenType type, Func<Token, TNumber>? processor, bool overrideExisting = true) {
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            tokenProcessors ??= new Func<Token, TNumber>[256];
            if (tokenProcessors[(int)type] != null) {
                if (!overrideExisting) return false;
                tokenProcessors[(int)type] = processor;
            } else
                tokenProcessors[(int)type] = processor;
            return true;
        }

        public virtual void RegisterDefaultFunctions() {
            foreach (var methodInfo in GetType().GetMethods(
                BindingFlags.Static |
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public |
                BindingFlags.FlattenHierarchy
            )) {
                var attribute = methodInfo.GetCustomAttribute<ProcessorAttribute>();
                if (attribute == null) continue;
                var parameters = methodInfo.GetParameters();
                switch (parameters.Length) {
                    case 0:
                        if (methodInfo.IsStatic || attribute.Type != TokenType.External) goto default;
                        RegisterProcessor(attribute.FunctionName, Delegate.CreateDelegate(typeof(Func<TNumber>), this, methodInfo) as Func<TNumber>);
                        break;
                    case 1:
                        if (parameters[0].ParameterType == typeof(Token)) {
                            if (methodInfo.IsStatic) goto default;
                            RegisterProcessor(attribute.Type, Delegate.CreateDelegate(typeof(Func<Token, TNumber>), this, methodInfo) as Func<Token, TNumber>);
                        } else if (attribute.Type == TokenType.External)
                            RegisterProcessor(attribute.FunctionName, methodInfo.IsStatic ?
                                Delegate.CreateDelegate(typeof(FunctionProcessor), methodInfo) as FunctionProcessor :
                                Delegate.CreateDelegate(typeof(FunctionProcessor), this, methodInfo) as FunctionProcessor
                            );
                        else
                            RegisterProcessor(attribute.Type, methodInfo.IsStatic ?
                                Delegate.CreateDelegate(typeof(UnaryOperatorProcessor), methodInfo) as UnaryOperatorProcessor :
                                Delegate.CreateDelegate(typeof(UnaryOperatorProcessor), this, methodInfo) as UnaryOperatorProcessor
                            );
                        break;
                    case 2:
                        if (attribute.Type == TokenType.External) goto default;
                        RegisterProcessor(attribute.Type, methodInfo.IsStatic ?
                            Delegate.CreateDelegate(typeof(BinaryOperatorProcessor), methodInfo) as BinaryOperatorProcessor :
                            Delegate.CreateDelegate(typeof(BinaryOperatorProcessor), this, methodInfo) as BinaryOperatorProcessor
                        );
                        break;
                    default:
                        throw new NotSupportedException($"Method {methodInfo.Name} has unsupported signature");
                }
            }
        }

        #region Operators
        [Processor("min")]
        protected TNumber Min(Span<TNumber> args) {
            if (comparer == null) comparer = Comparer<TNumber>.Default;
            if (args.Length == 0) return Error;
            var min = args[0];
            for (int i = 1; i < args.Length; i++)
                if (comparer.Compare(args[i], min) < 0)
                    min = args[i];
            return min;
        }

        [Processor("max")]
        protected TNumber Max(Span<TNumber> args) {
            if (comparer == null) comparer = Comparer<TNumber>.Default;
            if (args.Length == 0) return Error;
            var max = args[0];
            for (int i = 1; i < args.Length; i++)
                if (comparer.Compare(args[i], max) > 0)
                    max = args[i];
            return max;
        }

        [Processor("clamp")]
        protected TNumber Clamp(Span<TNumber> args) {
            if (comparer == null) comparer = Comparer<TNumber>.Default;
            if (args.Length < 3) return Error;
            return comparer.Compare(args[2], args[0]) < 0 ? args[0] :
                comparer.Compare(args[2], args[1]) > 0 ? args[1] :
                args[2];
        }

        [Processor(TokenType.Equals)]
        protected virtual TNumber Equals(TNumber first, TNumber second) {
            if (comparer == null) comparer = Comparer<TNumber>.Default;
            return comparer.Compare(first, second) == 0 ? Truely : Falsy;
        }

        [Processor(TokenType.NotEquals)]
        protected virtual TNumber NotEquals(TNumber first, TNumber second) {
            if (comparer == null) comparer = Comparer<TNumber>.Default;
            return comparer.Compare(first, second) != 0 ? Truely : Falsy;
        }

        [Processor(TokenType.GreaterThan)]
        protected virtual TNumber GreaterThan(TNumber first, TNumber second) {
            if (comparer == null) comparer = Comparer<TNumber>.Default;
            return comparer.Compare(first, second) > 0 ? Truely : Falsy;
        }

        [Processor(TokenType.GreaterThanOrEquals)]
        protected virtual TNumber GreaterThanOrEquals(TNumber first, TNumber second) {
            if (comparer == null) comparer = Comparer<TNumber>.Default;
            return comparer.Compare(first, second) >= 0 ? Truely : Falsy;
        }

        [Processor(TokenType.LessThan)]
        protected virtual TNumber LessThan(TNumber first, TNumber second) {
            if (comparer == null) comparer = Comparer<TNumber>.Default;
            return comparer.Compare(first, second) < 0 ? Truely : Falsy;
        }

        [Processor(TokenType.LessThanOrEquals)]
        protected virtual TNumber LessThanOrEquals(TNumber first, TNumber second) {
            if (comparer == null) comparer = Comparer<TNumber>.Default;
            return comparer.Compare(first, second) <= 0 ? Truely : Falsy;
        }

        [Processor(TokenType.LogicalAnd)]
        protected virtual TNumber LogicalAnd(TNumber first, TNumber second) =>
            IsTruely(first) ? second : first;

        [Processor(TokenType.LogicalOr)]
        protected virtual TNumber LogicalOr(TNumber first, TNumber second) =>
            IsTruely(first) ? first : second;

        [Processor(TokenType.LogicalNot)]
        protected virtual TNumber LogicalNot(TNumber value) =>
            IsTruely(value) ? Falsy : Truely;

        [Processor(TokenType.Primitive)]
        protected virtual TNumber Primitive(Token token) => token.numberValue;

        [Processor(TokenType.Identifier)]
        protected virtual TNumber Identifier(Token token) => GetVariable(token.identifierValue);

        [Processor(TokenType.External)]
        protected virtual TNumber External(Token token) {
            if (functionProcessors == null || !functionProcessors.TryGetValue(token.identifierValue!, out var externProc))
                throw new Exception($"External function {token.identifierValue} not found");
            return externProc();
        }
        #endregion
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ProcessorAttribute : Attribute {
        public TokenType Type { get; private set; }
        public string? FunctionName { get; private set; }

        public ProcessorAttribute(TokenType type) {
            Type = type;
        }

        public ProcessorAttribute(string functionName) {
            Type = TokenType.External;
            FunctionName = functionName;
        }
    }
}