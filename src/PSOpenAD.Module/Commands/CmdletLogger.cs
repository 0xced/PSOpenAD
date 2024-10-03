using System;
using System.Collections.Generic;
using System.Management.Automation;
using Microsoft.Extensions.Logging;
using PSOpenAD.LDAP;

namespace PSOpenAD.Module.Commands;

internal class CmdletLogger : ILogger
{
    private readonly Cmdlet? _cmdlet;
    private readonly Stack<IDictionary<string, string>> _scopes = new();

    public CmdletLogger(Cmdlet? cmdlet)
    {
        _cmdlet = cmdlet;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);

        if (logLevel == LogLevel.Error)
        {
            string? errorId;
            ErrorCategory errorCategory;
            ErrorDetails? errorDetails;
            if (_scopes.TryPeek(out var scope))
            {
                errorId = scope["ErrorId"];
                errorCategory = Enum.TryParse<ErrorCategory>(scope["ErrorCategory"], out var category) ? category : ErrorCategory.NotSpecified;
                if (scope.TryGetValue("ErrorDetails", out var errorDetailsMessage))
                {
                    errorDetails = new ErrorDetails(errorDetailsMessage);
                    if (scope.TryGetValue("ErrorRecommendedAction", out var recommendedAction))
                    {
                        errorDetails.RecommendedAction = recommendedAction;
                    }
                }
                else
                {
                    errorDetails = null;
                }
            }
            else
            {
                errorId = null;
                errorCategory = ErrorCategory.NotSpecified;
                errorDetails = null;
            }

            ErrorRecord error = new(exception ?? (errorCategory == ErrorCategory.InvalidArgument ? new ArgumentException(message) : new LDAPException(message)), errorId, errorCategory, null)
            {
                ErrorDetails = errorDetails,
            };
            _cmdlet?.WriteError(error);
        }
        else
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                    _cmdlet?.WriteVerbose(message);
                    break;
                case LogLevel.Debug:
                    _cmdlet?.WriteDebug(message);
                    break;
                case LogLevel.Information:
                    _cmdlet?.WriteInformation(new InformationRecord(message, null));
                    break;
                case LogLevel.Warning:
                    _cmdlet?.WriteWarning(message);
                    break;
                case LogLevel.Critical:
                    _cmdlet?.WriteWarning(message);
                    break;
                case LogLevel.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
            }
        }
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        if (state is IDictionary<string, string> stateDictionary)
        {
            return new Scope<IDictionary<string, string>>(_scopes, stateDictionary);
        }

        return null;
    }

    private class Scope<T> : IDisposable
    {
        private readonly Stack<T> _stack;

        public Scope(Stack<T> stack, T item)
        {
            _stack = stack;
            _stack.Push(item);
        }

        public void Dispose()
        {
            _stack.Pop();
        }
    }
}