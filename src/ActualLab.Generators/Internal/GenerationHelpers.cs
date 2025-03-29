using System.Reflection;

namespace ActualLab.Generators.Internal;
using static SyntaxFactory;

public static class GenerationHelpers
{
    public const string SystemReactiveNs = "System.Reactive";
    public const string SystemReactiveGns = $"global::{SystemReactiveNs}";
    public const string SystemRuntimeCompilerServicesNs = "System.Runtime.CompilerServices";
    public const string SystemRuntimeCompilerServicesGns = $"global::{SystemRuntimeCompilerServicesNs}";
    public const string TrimmingNs = "ActualLab.Trimming";
    public const string InterceptionNs = "ActualLab.Interception";
    public const string InterceptionInternalNs = "ActualLab.Interception.Internal";
    public const string InterceptionTrimmingNs = "ActualLab.Interception.Trimming";
    public const string TrimmingGns = $"global::{TrimmingNs}";
    public const string InterceptionGns = $"global::{InterceptionNs}";
    public const string InterceptionInternalGns = $"global::{InterceptionInternalNs}";
    public const string InterceptionTrimmingGns = $"global::{InterceptionTrimmingNs}";
    public const string RequiresFullProxyInterfaceName = $"{InterceptionNs}.IRequiresFullProxy";
    public const string RequireAsyncProxyInterfaceName = $"{InterceptionNs}.IRequiresAsyncProxy";
    public const string ProxyIgnoreAttributeName = $"{InterceptionNs}.ProxyIgnoreAttribute";
    public const string ProxyClassSuffix = "Proxy";
    public const string ProxyNamespaceSuffix = "ActualLabProxies";
    public const int MaxGenericArgumentListItemCount = 4; // Must match MaxGenericItemCount in ArgumentList-Generated.t4

    // System types
    public static readonly IdentifierNameSyntax UnitTypeName = IdentifierName($"{SystemReactiveGns}.Unit");
    public static readonly IdentifierNameSyntax ModuleInitializerAttributeName
        = IdentifierName($"{SystemRuntimeCompilerServicesGns}.ModuleInitializer");

    // Types
    public static readonly IdentifierNameSyntax ProxyInterfaceTypeName = IdentifierName($"{InterceptionGns}.IProxy");
    public static readonly IdentifierNameSyntax InterfaceProxyBaseTypeName = IdentifierName($"{InterceptionInternalGns}.InterfaceProxy");
    public static readonly IdentifierNameSyntax InterceptorTypeName = IdentifierName($"{InterceptionGns}.Interceptor");
    public static readonly IdentifierNameSyntax ProxyHelperTypeName = IdentifierName($"{InterceptionInternalGns}.ProxyHelper");
    public static readonly IdentifierNameSyntax ArgumentListTypeName = IdentifierName($"{InterceptionGns}.ArgumentList");
    public static readonly IdentifierNameSyntax ArgumentList0TypeName = IdentifierName($"{InterceptionGns}.ArgumentList0");
    public static readonly IdentifierNameSyntax InvocationTypeName = IdentifierName($"{InterceptionGns}.Invocation");
    public static readonly IdentifierNameSyntax CodeKeeperTypeName = IdentifierName($"{TrimmingGns}.CodeKeeper");
    public static readonly IdentifierNameSyntax ProxyCodeKeeperTypeName = IdentifierName($"{InterceptionTrimmingGns}.ProxyCodeKeeper");
    public static readonly IdentifierNameSyntax ErrorsTypeName = IdentifierName($"{InterceptionInternalGns}.Errors");
    public static readonly TypeSyntax NullableMethodInfoType = NullableType(typeof(MethodInfo).ToTypeRef());
    // Methods
    public static readonly IdentifierNameSyntax ArgumentListNewMethodName = IdentifierName("New");
    public static readonly IdentifierNameSyntax GetMethodInfoMethodName = IdentifierName("GetMethodInfo");
    public static readonly IdentifierNameSyntax InterceptMethodName = IdentifierName("Intercept");
    public static readonly GenericNameSyntax InterceptGenericMethodName = GenericName(InterceptMethodName.Identifier.Text);
    public static readonly IdentifierNameSyntax NoInterceptorMethodName = IdentifierName("NoInterceptor");
    public static readonly IdentifierNameSyntax KeepCodeMethodName = IdentifierName("KeepCode");
    public static readonly IdentifierNameSyntax AddActionMethodName = IdentifierName("AddAction");
    public static readonly IdentifierNameSyntax AddFakeActionMethodName = IdentifierName("AddFakeAction");
    public static readonly IdentifierNameSyntax FakeCallSilentlyMethodName = IdentifierName("FakeCallSilently");
    public static readonly GenericNameSyntax CodeKeeperGetGenericMethodName = GenericName("Get");
    public static readonly GenericNameSyntax CodeKeeperKeepProxyGenericMethodName = GenericName("KeepProxy");
    public static readonly GenericNameSyntax CodeKeeperKeepAsyncMethodGenericMethodName = GenericName("KeepAsyncMethod");
    public static readonly GenericNameSyntax CodeKeeperKeepSyncMethodGenericMethodName = GenericName("KeepSyncMethod");
    // Properties, fields, locals
    public static readonly IdentifierNameSyntax ProxyTargetPropertyName = IdentifierName("ProxyTarget");
    public static readonly IdentifierNameSyntax InterceptorPropertyName = IdentifierName("Interceptor");
    public static readonly IdentifierNameSyntax InterceptorFieldName = IdentifierName("__interceptor");
    public static readonly IdentifierNameSyntax ValueParameterName = IdentifierName("value");
    public static readonly IdentifierNameSyntax InterceptedVarName = IdentifierName("intercepted");
    public static readonly IdentifierNameSyntax InvocationVarName = IdentifierName("invocation");
    public static readonly IdentifierNameSyntax ProxyCodeKeeperVarName = IdentifierName("proxyCodeKeeper");

    // Helpers

    public static NameSyntax GetArgumentListSTypeName(int itemCount)
        => itemCount == 0
            ? ArgumentList0TypeName
            : IdentifierName($"{InterceptionGns}.ArgumentListS{itemCount}");

    public static NameSyntax GetArgumentListGTypeName(params TypeSyntax[] itemTypes)
    {
        if (itemTypes.Length == 0)
            return ArgumentList0TypeName;

        var genericArgTypes = itemTypes.Take(MaxGenericArgumentListItemCount);
        return GenericName($"{InterceptionGns}.ArgumentListG{itemTypes.Length}")
            .WithTypeArgumentList(TypeArgumentList(CommaSeparatedList(genericArgTypes)));
    }

    public static ObjectCreationExpressionSyntax NewExpression(TypeSyntax type, params ExpressionSyntax[] arguments)
        => ObjectCreationExpression(type)
            .WithArgumentList(ArgumentList(CommaSeparatedList(arguments.Select(Argument))));

    public static InvocationExpressionSyntax EmptyArrayExpression<TItem>()
        => EmptyArrayExpression(typeof(TItem).ToTypeRef());
    public static InvocationExpressionSyntax EmptyArrayExpression(TypeSyntax itemTypeRef)
        => InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    AliasQualifiedName(
                        IdentifierName(Token(SyntaxKind.GlobalKeyword)),
                        IdentifierName("System")),
                    IdentifierName("Array")),
                GenericName(Identifier("Empty"))
                    .WithTypeArgumentList(
                        TypeArgumentList(
                            SingletonSeparatedList(
                                itemTypeRef
                            )))));

    public static ImplicitArrayCreationExpressionSyntax ImplicitArrayCreationExpression(params ExpressionSyntax[] itemExpressions)
        => SyntaxFactory.ImplicitArrayCreationExpression(
            InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                CommaSeparatedList(itemExpressions)));

    public static FieldDeclarationSyntax PrivateFieldDef(TypeSyntax type, SyntaxToken name, ExpressionSyntax? initializer = null)
        => PrivateFieldDef(type, name, false, initializer);
    public static FieldDeclarationSyntax PrivateStaticFieldDef(TypeSyntax type, SyntaxToken name, ExpressionSyntax? initializer = null)
        => PrivateFieldDef(type, name, true, initializer);
    public static FieldDeclarationSyntax PrivateFieldDef(TypeSyntax type, SyntaxToken name, bool isStatic, ExpressionSyntax? initializer = null)
    {
        var initializerClause = initializer == null
            ? null
            : EqualsValueClause(initializer);
        var fieldDeclaration = FieldDeclaration(
            VariableDeclaration(type)
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(name, null, initializerClause))));
        return fieldDeclaration.WithModifiers(isStatic
            ? TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword))
            : TokenList(Token(SyntaxKind.PrivateKeyword)));
    }

    public static LocalDeclarationStatementSyntax VarStatement(SyntaxToken name, ExpressionSyntax initializer)
        => LocalDeclarationStatement(
            VariableDeclaration(VarIdentifierDef())
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(name)
                        .WithInitializer(EqualsValueClause(initializer)))));

    public static StatementSyntax IfHasTypeStatement(
        ExpressionSyntax expression,
        TypeSyntax typeSyntax,
        SyntaxToken varIdentifier,
        StatementSyntax trueStatement,
        StatementSyntax? falseStatement = null)
        => IfStatement(
            IsPatternExpression(
                expression,
                DeclarationPattern(
                    typeSyntax,
                    SingleVariableDesignation(varIdentifier))),
            trueStatement,
            falseStatement != null ? ElseClause(falseStatement) : null);

    public static StatementSyntax AlwaysReturnStatement(bool returnsVoid, ExpressionSyntax expression)
        => !returnsVoid
            ? ReturnStatement(expression)
            : Block(ExpressionStatement(expression), ReturnStatement());

    public static StatementSyntax MaybeReturnStatement(bool mustReturn, ExpressionSyntax expression)
        => mustReturn
            ? ReturnStatement(expression)
            : ExpressionStatement(expression);

    public static ThrowStatementSyntax ThrowStatement(IdentifierNameSyntax methodName)
        => SyntaxFactory.ThrowStatement(
            InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ErrorsTypeName,
                        methodName))
                .WithArgumentList(ArgumentList()));

    public static ThrowExpressionSyntax ThrowExpression<TException>(string message)
        where TException : Exception
        => SyntaxFactory.ThrowExpression(
            ObjectCreationExpression(typeof(TException).ToTypeRef())
                .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(
                                LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(message)))))));

    public static PostfixUnaryExpressionSyntax SuppressNullWarning(ExpressionSyntax expression)
        => PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression, expression);

    public static AssignmentExpressionSyntax CoalesceAssignmentExpression(ExpressionSyntax left, ExpressionSyntax right)
        => AssignmentExpression(SyntaxKind.CoalesceAssignmentExpression, left, right);

    public static SeparatedSyntaxList<TNode> CommaSeparatedList<TNode>(params TNode[] nodes)
        where TNode : SyntaxNode
        => CommaSeparatedList((IEnumerable<TNode>)nodes);

    public static SeparatedSyntaxList<TNode> CommaSeparatedList<TNode>(IEnumerable<TNode> nodes)
        where TNode : SyntaxNode
    {
        var list = new List<SyntaxNodeOrToken>();
        foreach (var nodeOrToken in nodes) {
            if (list.Count > 0)
                list.Add(Token(SyntaxKind.CommaToken));
            list.Add(nodeOrToken);
        }
        return SeparatedList<TNode>(NodeOrTokenList(list));
    }

    public static SyntaxList<TNode> SyntaxList<TNode>(params TNode[] nodes)
        where TNode : SyntaxNode
        => new(nodes);

    public static IdentifierNameSyntax VarIdentifierDef()
        => IdentifierName(
            Identifier(
                TriviaList(),
                SyntaxKind.VarKeyword,
                "var",
                "var",
                TriviaList()));
}
