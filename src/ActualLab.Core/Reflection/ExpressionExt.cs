using System.Linq.Expressions;
using ActualLab.Internal;

namespace ActualLab.Reflection;

public static class ExpressionExt
{
    public static Expression PropertyOrField(Expression source, MemberInfo propertyOrField)
    {
        var declaringType = propertyOrField.DeclaringType;
        if (propertyOrField is PropertyInfo pi) {
            var isStatic = (pi.GetMethod ?? pi.SetMethod!).IsStatic;
            return Expression.Property(isStatic ? null : MaybeConvert(source, declaringType!), pi);
        }

        if (propertyOrField is FieldInfo fi)
            return Expression.Field(fi.IsStatic ? null : MaybeConvert(source, declaringType!), fi);

        throw new ArgumentOutOfRangeException(nameof(propertyOrField));
    }

    public static Expression ConvertToObject(Expression expression)
    {
        var type = expression.Type;
        if (type == typeof(void))
            return Expression.Block([expression, Expression.Constant(null, typeof(object))]);
        if (type.IsValueType)
            return Expression.Convert(expression, typeof(object));

        return expression;
    }

    public static Expression MaybeConvert(Expression expression, Type expectedType)
        => expression.Type == expectedType
            ? expression
            : Expression.Convert(expression, expectedType);

    public static (Type memberType, string memberName) MemberTypeAndName<T, TValue>(
        this Expression<Func<T, TValue>> expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var memberExpression = expression.Body as MemberExpression;

        (Type memberType, string memberName) TypeAndName(MemberExpression me)
            => (me.Member.ReturnType(), me.Member.Name);

        if (memberExpression != null)
            return TypeAndName(memberExpression);
        if (!(expression.Body is UnaryExpression body))
            throw Errors.ExpressionDoesNotSpecifyAMember(expression.ToString());
        memberExpression = body.Operand as MemberExpression;
        if (memberExpression == null)
            throw Errors.ExpressionDoesNotSpecifyAMember(expression.ToString());

        return TypeAndName(memberExpression);
    }
}
