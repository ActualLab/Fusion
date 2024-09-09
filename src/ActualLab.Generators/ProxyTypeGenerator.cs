using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ActualLab.Generators;
using static GenerationHelpers;

public class ProxyTypeGenerator
{
    private SourceProductionContext Context { get; }
    private SemanticModel SemanticModel { get; }
    private TypeDeclarationSyntax TypeDef { get; }
    private TypeSyntax TypeRef { get; } = null!;
    private ITypeSymbol TypeSymbol { get; } = null!;
    private NameSyntax NamespaceRef { get; } = null!;
    private ClassDeclarationSyntax? ClassDef { get; }
    private InterfaceDeclarationSyntax? InterfaceDef { get; }
    private bool IsGenericType { get; }
    private bool IsInterfaceProxy => InterfaceDef != null;
    private bool IsFullProxy { get; }
    private bool IsAsyncOnlyProxy => !IsFullProxy;

    private string ProxyTypeName { get; } = "";
    private ClassDeclarationSyntax ProxyDef { get; } = null!;
    private TypeSyntax ProxyRef { get; } = null!;
    private List<MemberDeclarationSyntax> StaticFields { get; } = new();
    private List<MemberDeclarationSyntax> Fields { get; } = new();
    private List<MemberDeclarationSyntax> Properties { get; } = new();
    private List<MemberDeclarationSyntax> Constructors { get; } = new();
    private List<MemberDeclarationSyntax> Methods { get; } = new();
    private List<StatementSyntax> KeepCodeMethodStatements { get; } = new();
    private Dictionary<ITypeSymbol, string> CachedInterceptFieldNames { get; } = new(SymbolEqualityComparer.Default);
    private List<StatementSyntax> PostSetInterceptorStatements { get; } = new();

    public string GeneratedCode { get; } = "";

    public ProxyTypeGenerator(SourceProductionContext context, SemanticModel semanticModel, TypeDeclarationSyntax typeDef)
    {
        Context = context;
        SemanticModel = semanticModel;
        TypeDef = typeDef;
        if (SemanticModel.GetDeclaredSymbol(TypeDef) is not { } typeSymbol)
            return;
        if (TypeDef.GetNamespaceRef() is not { } namespaceRef)
            return;

        TypeRef = TypeDef.ToTypeRef();
        TypeSymbol = typeSymbol;
        NamespaceRef = namespaceRef;
        ClassDef = TypeDef as ClassDeclarationSyntax;
        InterfaceDef = TypeDef as InterfaceDeclarationSyntax;
        IsGenericType = TypeDef.TypeParameterList is { } l && l.Parameters.Count != 0;
        IsFullProxy = typeSymbol.AllInterfaces.Any(t => Equals(t.ToFullName(), RequiresFullProxyInterfaceName));
        WriteDebug?.Invoke($"{TypeSymbol.ToFullName()}: {(IsFullProxy ? "full" : "async")} proxy");

        ProxyTypeName = TypeDef.Identifier.Text + ProxyClassSuffix;
        var baseTypes = new List<TypeSyntax>() { TypeRef, ProxyInterfaceTypeName };
        if (IsInterfaceProxy)
            baseTypes.Insert(0, InterfaceProxyBaseTypeName);

        SyntaxToken accessibilityModifier;
        if (typeSymbol.DeclaredAccessibility == Accessibility.Public)
            accessibilityModifier = Token(SyntaxKind.PublicKeyword);
        else if (typeSymbol.DeclaredAccessibility == Accessibility.Internal)
            accessibilityModifier = Token(SyntaxKind.InternalKeyword);
        else
            return;

        ProxyDef = ClassDeclaration(ProxyTypeName)
            .AddModifiers(accessibilityModifier, Token(SyntaxKind.SealedKeyword))
            .WithTypeParameterList(TypeDef.TypeParameterList)
            .WithBaseList(BaseList(CommaSeparatedList(
                baseTypes.Select(t => (BaseTypeSyntax)SimpleBaseType(t)).ToArray())))
            .WithConstraintClauses(TypeDef.ConstraintClauses);
        ProxyRef = IsGenericType
            ? GenericName(Identifier(ProxyTypeName), TypeArgumentList(CommaSeparatedList<TypeSyntax>(
                TypeDef.TypeParameterList?.Parameters.Select(x => IdentifierName(x.Identifier.Text)) ?? [])))
            : IdentifierName(ProxyTypeName);

        KeepCodeMethodStatements.Add(VarStatement(
            ProxyCodeKeeperVarName.Identifier,
            InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    CodeKeeperTypeName,
                    CodeKeeperGetGenericMethodName.WithTypeArgumentList(
                        TypeArgumentList(SingletonSeparatedList<TypeSyntax>(ProxyCodeKeeperTypeName)))))));
        KeepCodeMethodStatements.Add(ExpressionStatement(
            InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ProxyCodeKeeperVarName,
                    CodeKeeperKeepProxyGenericMethodName.WithTypeArgumentList(
                        TypeArgumentList(CommaSeparatedList(TypeRef, ProxyRef)))))));

        if (!AddClassConstructors()) {
            WriteDebug?.Invoke($"[- Type] No constructors: {typeSymbol}");
            return; // No public constructors
        }
        AddProxyMethods();
        AddProxyInterfaceImplementation(); // Must be the last one
        AddModuleInitializer();

        var disableObsoleteMemberWarning1 = Trivia(
            PragmaWarningDirectiveTrivia(Token(SyntaxKind.DisableKeyword), true)
                .WithErrorCodes(SingletonSeparatedList<ExpressionSyntax>(IdentifierName("CS0618"))));
        var disableObsoleteMemberWarning2 = Trivia(
            PragmaWarningDirectiveTrivia(Token(SyntaxKind.DisableKeyword), true)
                .WithErrorCodes(SingletonSeparatedList<ExpressionSyntax>(IdentifierName("CS0672"))));
        var disableMissingXmlCommentWarning = Trivia(
            PragmaWarningDirectiveTrivia(Token(SyntaxKind.DisableKeyword), true)
                .WithErrorCodes(SingletonSeparatedList<ExpressionSyntax>(IdentifierName("CS1591"))));
        ProxyDef = ProxyDef
            .WithMembers(List(
                StaticFields
                .Concat(Fields)
                .Concat(Properties)
                .Concat(Constructors)
                .Concat(Methods)))
            .WithLeadingTrivia(
                disableObsoleteMemberWarning1,
                disableObsoleteMemberWarning2,
                disableMissingXmlCommentWarning)
            .NormalizeWhitespace();
        // WriteDebug?.Invoke(ProxyDef.ToString());

        var proxyNamespaceRef = (NameSyntax)(NamespaceRef.GetText().ToString().Length == 0
            ? IdentifierName(ProxyNamespaceSuffix)
            : QualifiedName(NamespaceRef, IdentifierName(ProxyNamespaceSuffix)));
        var proxyNamespaceDef = FileScopedNamespaceDeclaration(proxyNamespaceRef)
            .AddMembers(ProxyDef);

        // Building Compilation unit
        var syntaxRoot = SemanticModel.SyntaxTree.GetRoot();
        var usingDirectives = syntaxRoot.ChildNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(d => d.WithoutTrivia()) // Removes #ifdef, etc.
            .ToArray();
        var unit = CompilationUnit()
            .AddUsings(usingDirectives)
            .AddMembers(proxyNamespaceDef)
            .NormalizeWhitespace();

        GeneratedCode = $"""
            // <auto-generated/>
            #nullable enable
            #nullable disable warnings

            {unit.ToString()}
            """;
    }

    private bool AddClassConstructors()
    {
        List<IMethodSymbol?> constructors = TypeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Constructor && m.DeclaredAccessibility.HasFlag(Accessibility.Public))
            .ToList()!;
        if (constructors.Count == 0) {
            if (ClassDef != null)
                return false;

            constructors = [null];
        }

        foreach (var ctor in constructors) {
            if (ctor != null) {
                var parameters = ctor.Parameters
                    .Select(p => Parameter(Identifier(p.Name)).WithType(p.Type.ToTypeRef()))
                    .ToArray();
                var arguments = ctor.Parameters
                    .Select(p => Argument(IdentifierName(p.Name)))
                    .ToArray();
                Constructors.Add(
                    ConstructorDeclaration(Identifier(ProxyTypeName))
                        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                        .WithParameterList(ParameterList(CommaSeparatedList(parameters)))
                        .WithInitializer(
                            ConstructorInitializer(SyntaxKind.BaseConstructorInitializer,
                                ArgumentList(SeparatedList(arguments))))
                        .WithBody(Block()));
            }

            var defaultArguments = ctor?.Parameters
                .Select(p => Argument(
                    PostfixUnaryExpression(
                        SyntaxKind.SuppressNullableWarningExpression,
                        DefaultExpression(p.Type.ToTypeRef()))))
                .ToArray() ?? [];
            var fakeCtorCallExpression =
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        CodeKeeperTypeName,
                        FakeCallSilentlyMethodName))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(
                    ParenthesizedLambdaExpression()
                        .WithExpressionBody(
                            ObjectCreationExpression(ProxyRef)
                                .WithArgumentList(ArgumentList(SeparatedList(defaultArguments))))))));
            KeepCodeMethodStatements.Add(ExpressionStatement(fakeCtorCallExpression));
        }
        return true;
    }

    private void AddProxyMethods()
    {
        var typeRef = TypeDef.ToTypeRef();
        var methodIndex = 0;
        foreach (var (method, unwrappedReturnType) in GetProxyMethods()) {
            var modifiers = TokenList(
                method.DeclaredAccessibility.HasFlag(Accessibility.Protected)
                    ? Token(SyntaxKind.ProtectedKeyword)
                    : Token(SyntaxKind.PublicKeyword));
            if (!IsInterfaceProxy)
                modifiers = modifiers.Add(Token(SyntaxKind.OverrideKeyword));

            var returnType = method.ReturnType.ToTypeRef();
            var returnTypeIsVoid = returnType.IsVoid();
            var parameters = ParameterList(CommaSeparatedList(
                method.Parameters.Select(p =>
                    Parameter(Identifier(p.Name))
                        .WithType(p.Type.ToTypeRef()))));

            // __cachedMethodN field
            var cachedMethodFieldName = "__cachedMethod" + methodIndex;
            StaticFields.Add(
                PrivateStaticFieldDef(
                    NullableMethodInfoType,
                    Identifier(cachedMethodFieldName),
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(cachedMethodFieldName),
                        GetMethodInfoExpression(typeRef, method, parameters))));

            // __cachedInterceptedN field
            var cachedInterceptedFieldName = "__cachedIntercepted" + methodIndex;
            Fields.Add(CachedInterceptedFieldDef(Identifier(cachedInterceptedFieldName), returnType));

            // __cachedInterceptN field
            if (!CachedInterceptFieldNames.TryGetValue(method.ReturnType, out var cachedInterceptFieldName)) {
                cachedInterceptFieldName = "__cachedIntercept" + methodIndex;
                CachedInterceptFieldNames.Add(method.ReturnType, cachedInterceptFieldName);
                Fields.Add(CachedInterceptFieldDef(Identifier(cachedInterceptFieldName), returnType));

                var interceptMethod = returnTypeIsVoid
                    ? (SimpleNameSyntax)InterceptMethodName
                    : InterceptGenericMethodName
                        .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(returnType)));

                PostSetInterceptorStatements.Add(
                    ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(cachedInterceptFieldName),
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                InterceptorFieldName,
                                interceptMethod))));
                    ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(cachedInterceptFieldName),
                            GetMethodInfoExpression(typeRef, method, parameters)));
            }

            // Method body
            var argumentList = CreateArgumentList(
                method.Parameters
                    .Select(p => Argument(IdentifierName(p.Name)))
                    .ToArray());
            var body = Block(
                VarStatement(InterceptedVarName.Identifier,
                    CoalesceAssignmentExpression(
                        IdentifierName(cachedInterceptedFieldName),
                        CreateInterceptedLambda(method, parameters))),
                VarStatement(InvocationVarName.Identifier,
                    NewExpression(
                        InvocationTypeName,
                        ThisExpression(),
                        SuppressNullWarning(IdentifierName(cachedMethodFieldName)),
                        argumentList,
                        InterceptedVarName)),
                IfStatement(
                    BinaryExpression(
                        SyntaxKind.EqualsExpression,
                        IdentifierName(cachedInterceptFieldName),
                        LiteralExpression(SyntaxKind.NullLiteralExpression)),
                    ThrowStatement(NoInterceptorMethodName)),
                MaybeReturnStatement(
                    !returnTypeIsVoid,
                    InvocationExpression(
                        MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(cachedInterceptFieldName),
                                IdentifierName("Invoke")))
                            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(InvocationVarName)))))
            );

            Methods.Add(
                MethodDeclaration(returnType, Identifier(method.Name))
                    .WithModifiers(modifiers)
                    .WithParameterList(parameters)
                    .WithBody(body));

            // KeepCode statement for this method
            var isAsync = unwrappedReturnType != null;
            var keepGenericMethodName = isAsync
                ? CodeKeeperKeepAsyncMethodGenericMethodName
                : CodeKeeperKeepSyncMethodGenericMethodName;
            var keepMethodTypeArguments = TypeArgumentList(CommaSeparatedList(
                new[] { unwrappedReturnType ?? (returnTypeIsVoid ? UnitTypeName : returnType) }.Concat(
                    method.Parameters.Select(p => p.Type.ToTypeRef()))));
            var keepMethodArguments = ArgumentList(CommaSeparatedList(
                Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(method.Name)))));

            var keepMethodCallExpression =
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ProxyCodeKeeperVarName,
                        keepGenericMethodName.WithTypeArgumentList(keepMethodTypeArguments)))
                .WithArgumentList(keepMethodArguments);
            KeepCodeMethodStatements.Add(ExpressionStatement(keepMethodCallExpression));
            methodIndex++;
        }
    }

    private void AddModuleInitializer()
    {
        var bodyExpression =
            ArrowExpressionClause(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        CodeKeeperTypeName,
                        AddActionMethodName))
                .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(
                                ParenthesizedLambdaExpression()
                                    .WithModifiers(
                                        TokenList(Token(SyntaxKind.StaticKeyword)))
                                    .WithBlock(Block(KeepCodeMethodStatements))))))
                );

        var method = MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)),
                Identifier(KeepCodeMethodName.Identifier.Text));
        if (!IsGenericType)
            method = method.WithAttributeLists(SingletonList(
                AttributeList(SingletonSeparatedList(
                    Attribute(ModuleInitializerAttributeName)))));
        method = method
            .WithModifiers(
                TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithExpressionBody(bodyExpression)
            .WithSemicolonToken(
                Token(SyntaxKind.SemicolonToken));
        Methods.Add(method);
    }

    private IEnumerable<(IMethodSymbol Method, IdentifierNameSyntax? UnwrappedReturnType)> GetProxyMethods()
    {
        var hierarchy = IsInterfaceProxy
            ? TypeSymbol.GetAllInterfaces(true)
            : TypeSymbol.GetAllBaseTypes(true);
        WriteDebug?.Invoke($"Hierarchy: {string.Join(", ", hierarchy.Select(t => t.ToFullName()))}");
        var processedMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in hierarchy) {
            if (type.ToTypeRef().IsObject())
                continue;

            foreach (var (method, unwrappedReturnType) in GetDeclaredProxyMethods(type)) {
                if (!processedMethods.Add(method)) {
                    WriteDebug?.Invoke("  [-] Already processed");
                    continue;
                }

                WriteDebug?.Invoke("  [+]");
                processedMethods.Add(method);
                var overriddenMethod = method.OverriddenMethod;
                while (overriddenMethod != null) {
                    processedMethods.Add(overriddenMethod);
                    overriddenMethod = overriddenMethod.OverriddenMethod;
                }
                yield return (method, unwrappedReturnType);
            }
        }
    }

    private IEnumerable<(IMethodSymbol Method, IdentifierNameSyntax? UnwrappedReturnType)> GetDeclaredProxyMethods(ITypeSymbol type)
    {
        foreach (var member in type.GetMembers()) {
            if (member is not IMethodSymbol method)
                continue;

            var returnTypeName = method.ReturnType.ToFullName();
            if (IsDebugOutputEnabled)
                WriteDebug?.Invoke($"- {method.Name}({method.Parameters.Length}) -> {returnTypeName}");
            if (method.MethodKind is not MethodKind.Ordinary) {
                WriteDebug?.Invoke($"  [-] Non-ordinary: {method.MethodKind}");
                continue;
            }
            if (method.IsSealed || method.IsStatic || method.IsGenericMethod) {
                WriteDebug?.Invoke("  [-] Sealed, static, or generic");
                continue;
            }
            if (!(method.DeclaredAccessibility.HasFlag(Accessibility.Public)
                    || method.DeclaredAccessibility.HasFlag(Accessibility.Protected))) {
                WriteDebug?.Invoke($"  [-] Private: {method.DeclaredAccessibility}");
                continue;
            }

            if (!IsInterfaceProxy) {
                if (method.IsAbstract || !(method.IsVirtual || method.IsOverride)) {
                    WriteDebug?.Invoke("  [-] Non-virtual or abstract");
                    continue;
                }
            }

            var unwrappedReturnType = (IdentifierNameSyntax?)null;
            if (returnTypeName.OrdinalStartsWith("System.Threading.Tasks.")) {
                if (Equals(returnTypeName, "System.Threading.Tasks.Task") ||
                    Equals(returnTypeName, "System.Threading.Tasks.ValueTask"))
                    unwrappedReturnType = UnitTypeName;
                else {
                    var bracketIndex = returnTypeName.OrdinalIndexOf('<');
                    var genericName = bracketIndex >= 0 ? returnTypeName[..bracketIndex] : "";
                    if (Equals(genericName, "System.Threading.Tasks.Task") ||
                        Equals(genericName, "System.Threading.Tasks.ValueTask"))
                        unwrappedReturnType = IdentifierName(returnTypeName[(bracketIndex + 1)..^1]);
                }
            }
            if (IsAsyncOnlyProxy && unwrappedReturnType == null) {
                WriteDebug?.Invoke("  [-] Non-async method in async-only proxy");
                continue;
            }

            // Check for [ProxyIgnore]
            var m = method;
            var mustIgnore = false;
            while (m != null) {
                if (m.GetAttributes().Any(a => Equals(a.AttributeClass?.ToFullName(), ProxyIgnoreAttributeName))) {
                    mustIgnore = true;
                    break;
                }
                m = m.OverriddenMethod;
            }
            if (mustIgnore) {
                WriteDebug?.Invoke("  [-] Has [ProxyIgnore]");
                continue;
            }
            yield return (method, unwrappedReturnType);
        }
    }

    private static ExpressionSyntax GetMethodInfoExpression(
        TypeSyntax typeRef,
        IMethodSymbol method,
        ParameterListSyntax parameters)
    {
        var parameterTypes = parameters.Parameters
            .Select(p => TypeOfExpression(p.Type!))
            .ToArray<ExpressionSyntax>();

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                ProxyHelperTypeName,
                GetMethodInfoMethodName))
            .WithArgumentList(
                ArgumentList(
                    CommaSeparatedList(
                        Argument(
                            TypeOfExpression(typeRef)),
                        Argument(
                            LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(method.Name))),
                        Argument(
                            parameterTypes.Length == 0
                            ? EmptyArrayExpression<Type>()
                            : ImplicitArrayCreationExpression(parameterTypes))
                    )));
    }

    private static FieldDeclarationSyntax CachedInterceptedFieldDef(SyntaxToken name, TypeSyntax returnTypeDef)
    {
        TypeSyntax fieldTypeDef;
        if (!returnTypeDef.IsVoid()) {
            fieldTypeDef = GenericName(Identifier("global::System.Func"))
                .WithTypeArgumentList(TypeArgumentList(CommaSeparatedList(ArgumentListTypeName, returnTypeDef)));
        }
        else {
            fieldTypeDef = GenericName(Identifier("global::System.Action"))
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(ArgumentListTypeName)));
        }
        return PrivateFieldDef(NullableType(fieldTypeDef), name);
    }

    private static FieldDeclarationSyntax CachedInterceptFieldDef(SyntaxToken name, TypeSyntax returnTypeDef)
    {
        TypeSyntax fieldTypeDef;
        if (!returnTypeDef.IsVoid()) {
            fieldTypeDef = GenericName(Identifier("global::System.Func"))
                .WithTypeArgumentList(TypeArgumentList(CommaSeparatedList(InvocationTypeName, returnTypeDef)));
        }
        else {
            fieldTypeDef = GenericName(Identifier("global::System.Action"))
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(InvocationTypeName)));
        }
        return PrivateFieldDef(NullableType(fieldTypeDef), name);
    }

    private SimpleLambdaExpressionSyntax CreateInterceptedLambda(IMethodSymbol method, ParameterListSyntax parameters)
    {
        var returnTypeRef = method.ReturnType.ToTypeRef();
        var isVoidMethod = returnTypeRef.IsVoid();
        var argTypes = parameters.Parameters.Select(p => p.Type!).ToArray();
        var argListGType = GetArgumentListGTypeName(argTypes);
        var argListSType = GetArgumentListSTypeName(argTypes.Length);
        var args = IdentifierName("args");
        var ga = IdentifierName("ga");
        var sa = IdentifierName("sa");

        var statements = new List<StatementSyntax>();
        if (argTypes.Length != 0)
            statements.Add(
                IfHasTypeStatement(args, argListGType, ga.Identifier,
                    AlwaysReturnStatement(isVoidMethod, ProxyOrBaseCall(true, ga))));
        statements.Add(VarStatement(sa.Identifier, CastExpression(argListSType, args)));
        statements.Add(AlwaysReturnStatement(isVoidMethod, ProxyOrBaseCall(false, sa)));
        return SimpleLambdaExpression(Parameter(args.Identifier))
            .WithBlock(Block(List(statements)));

        ExpressionSyntax ProxyOrBaseCall(bool useGenerics, NameSyntax argListVar) {
            var callArgs = new List<ArgumentSyntax>();
            for (var i = 0; i < parameters.Parameters.Count; i++) {
                var expr = (ExpressionSyntax)MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    argListVar,
                    IdentifierName("Item" + i));
                if (!useGenerics || i >= MaxGenericArgumentListItemCount)
                    expr = CastExpression(argTypes[i], expr);
                callArgs.Add(Argument(expr));
            }

            var callTarget = IsInterfaceProxy
                ? (ExpressionSyntax)ParenthesizedExpression(
                    SuppressNullWarning(
                        CastExpression(TypeDef.ToTypeRef(), ProxyTargetPropertyName)))
                : BaseExpression();
            var call = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    callTarget,
                    IdentifierName(method.Name)),
                ArgumentList(CommaSeparatedList(callArgs))
            );
            return call;
        }
    }

    private static InvocationExpressionSyntax CreateArgumentList(params ArgumentSyntax[] newArgumentListParams)
        => InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ArgumentListTypeName,
                    ArgumentListNewMethodName))
            .WithArgumentList(
                ArgumentList(CommaSeparatedList(newArgumentListParams)));

    private void AddProxyInterfaceImplementation()
    {
        Fields.Add(
            PrivateFieldDef(NullableType(InterceptorTypeName), InterceptorFieldName.Identifier));

        var interceptorGetterDef = Block(
            IfStatement(
                BinaryExpression(
                    SyntaxKind.EqualsExpression,
                    InterceptorFieldName,
                    LiteralExpression(SyntaxKind.NullLiteralExpression)),
                ThrowStatement(NoInterceptorMethodName)),
            ReturnStatement(InterceptorFieldName));
        var interceptorSetterDef = Block(
            ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    InterceptorFieldName,
                    BinaryExpression(
                        SyntaxKind.CoalesceExpression,
                        ValueParameterName,
                        ThrowExpression<ArgumentNullException>(ValueParameterName.Identifier.Text)))));
        interceptorSetterDef = interceptorSetterDef.AddStatements(PostSetInterceptorStatements.ToArray());

        Properties.Add(
            PropertyDeclaration(InterceptorTypeName, InterceptorPropertyName.Identifier)
                .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(ProxyInterfaceTypeName))
                .WithAccessorList(
                    AccessorList(SyntaxList(
                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithBody(interceptorGetterDef),
                        AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithBody(interceptorSetterDef)))));
    }
}
