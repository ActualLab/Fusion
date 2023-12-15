using System.Reflection;

namespace ActualLab.Generators.Internal;
using static SyntaxFactory;

public static class GenerationHelpers
{
    public const string InterceptionNs = "ActualLab.Interception";
    public const string InterceptionInternalNs = "ActualLab.Interception.Internal";
    public const string InterceptionGns = $"global::{InterceptionNs}";
    public const string InterceptionInternalGns = $"global::{InterceptionInternalNs}";
    public const string RequiresFullProxyInterfaceName = $"{InterceptionNs}.IRequiresFullProxy";
    public const string RequireAsyncProxyInterfaceName = $"{InterceptionNs}.IRequiresAsyncProxy";
    public const string ProxyIgnoreAttributeName = $"{InterceptionNs}.ProxyIgnoreAttribute";
    public const string ProxyClassSuffix = "Proxy";
    public const string ProxyNamespaceSuffix = "ActualLabProxies";

    // Types
    public static readonly IdentifierNameSyntax ProxyInterfaceTypeName = IdentifierName($"{InterceptionGns}.IProxy");
    public static readonly IdentifierNameSyntax InterfaceProxyBaseTypeName = IdentifierName($"{InterceptionInternalGns}.InterfaceProxy");
    public static readonly IdentifierNameSyntax InterceptorTypeName = IdentifierName($"{InterceptionGns}.Interceptor");
    public static readonly IdentifierNameSyntax ProxyHelperTypeName = IdentifierName($"{InterceptionInternalGns}.ProxyHelper");
    public static readonly IdentifierNameSyntax ArgumentListTypeName = IdentifierName($"{InterceptionGns}.ArgumentList");
    public static readonly GenericNameSyntax ArgumentListGenericTypeName = GenericName(ArgumentListTypeName.Identifier.Text);
    public static readonly IdentifierNameSyntax InvocationTypeName = IdentifierName($"{InterceptionGns}.Invocation");
    public static readonly IdentifierNameSyntax ErrorsTypeName = IdentifierName($"{InterceptionInternalGns}.Errors");
    public static readonly TypeSyntax NullableMethodInfoType = NullableType(typeof(MethodInfo).ToTypeRef());
    // Methods
    public static readonly IdentifierNameSyntax ArgumentListNewMethodName = IdentifierName("New");
    public static readonly IdentifierNameSyntax GetMethodInfoMethodName = IdentifierName("GetMethodInfo");
    public static readonly IdentifierNameSyntax InterceptMethodName = IdentifierName("Intercept");
    public static readonly GenericNameSyntax InterceptGenericMethodName = GenericName(InterceptMethodName.Identifier.Text);
    public static readonly IdentifierNameSyntax SetInterceptorMethodName = IdentifierName("SetInterceptor");
    public static readonly IdentifierNameSyntax NoInterceptorMethodName = IdentifierName("NoInterceptor");
    public static readonly IdentifierNameSyntax InterceptorIsAlreadyBoundMethodName = IdentifierName("InterceptorIsAlreadyBound");
    // Properties, fields, locals
    public static readonly IdentifierNameSyntax ProxyTargetPropertyName = IdentifierName("ProxyTarget");
    public static readonly IdentifierNameSyntax InterceptorPropertyName = IdentifierName("Interceptor");
    public static readonly IdentifierNameSyntax InterceptorFieldName = IdentifierName("__interceptor");
    public static readonly IdentifierNameSyntax InterceptorParameterName = IdentifierName("interceptor");
    public static readonly IdentifierNameSyntax InterceptedVarName = IdentifierName("intercepted");
    public static readonly IdentifierNameSyntax InvocationVarName = IdentifierName("invocation");

    // Helpers

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

    public static IdentifierNameSyntax VarIdentifierDef()
        => IdentifierName(
            Identifier(
                TriviaList(),
                SyntaxKind.VarKeyword,
                "var",
                "var",
                TriviaList()));
}
