using System.ComponentModel.DataAnnotations;
using System.Security;
using System.Security.Authentication;
using ActualLab.Versioning;

namespace ActualLab.Resilience;

/// <summary>
/// Abstract base class for <see cref="TransiencyResolver"/>-s.
/// </summary>
public static class TransiencyResolvers
{
    /// <summary>
    /// This detector classifies only core errors & returns <see cref="Transiency.Unknown"/>
    /// for everything else.
    /// </summary>
    public static TransiencyResolver CoreOnly { get; set; }
        = static e => e switch {
            ITerminalException => Transiency.Terminal,
            _ when e.IsServiceProviderDisposedException() => Transiency.Terminal,
            ISuperTransientException => Transiency.SuperTransient,
            ITransientException => Transiency.Transient,
            RetryPolicyTimeoutExceededException => Transiency.NonTransient,
            TimeoutException => Transiency.Transient, // Must be transient
            _ => Transiency.Unknown,
        };

    /// <summary>
    /// This detector is used by Fusion's IComputed by default, see
    /// FusionBuilder's constructor to understand how to replace it in
    /// the DI container, or set this property to whatever you prefer
    /// before calling .AddFusion() for the first time.
    /// </summary>
    public static TransiencyResolver PreferTransient { get; set; }
        = e => CoreOnly.Invoke(e).Or(e, static e => e switch {
            // Most common errors
            NullReferenceException => Transiency.NonTransient,
            ArgumentException => Transiency.NonTransient,
            InvalidOperationException => Transiency.NonTransient, // .Single, etc. + ObjectDisposedException
            ValidationException => Transiency.NonTransient,
            ServiceException => Transiency.NonTransient,
            VersionMismatchException => Transiency.NonTransient,
            InvalidCastException => Transiency.NonTransient,
            NotSupportedException => Transiency.NonTransient,
            NotImplementedException => Transiency.NonTransient,
            FormatException => Transiency.NonTransient,
            JsonException => Transiency.NonTransient,
            SerializationException => Transiency.NonTransient,
            // Security-related
            SecurityException => Transiency.NonTransient,
            AuthenticationException => Transiency.NonTransient,
            UnauthorizedAccessException => Transiency.NonTransient,
            // Misc. "not found" errors
            KeyNotFoundException => Transiency.NonTransient,
            FileNotFoundException => Transiency.NonTransient,
            DirectoryNotFoundException => Transiency.NonTransient,
            MissingMethodException => Transiency.NonTransient,
            MissingFieldException => Transiency.NonTransient,
            MissingMemberException => Transiency.NonTransient,
            // Math
            ArithmeticException => Transiency.NonTransient,
            _ => Transiency.Transient,
        });

    /// <summary>
    /// This detector is used by Fusion's OperationReprocessor by default, see
    /// FusionBuilder.AddOperationReprocessor to understand how to replace it in
    /// the DI container, or set this property to whatever you prefer
    /// before calling .AddFusion() for the first time.
    /// </summary>
    public static TransiencyResolver PreferNonTransient { get; set; }
        = static e => CoreOnly.Invoke(e).Or(Transiency.NonTransient);
}
