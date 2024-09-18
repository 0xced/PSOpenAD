using System;
using System.Collections.Generic;
using System.Management.Automation;
using Microsoft.Extensions.Logging;
using PSOpenAD.LDAP;

namespace PSOpenAD.Module.Commands;

internal class CmdletLogger : ILogger
{
    private readonly Cmdlet? _cmdlet;

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
            if (state is IDictionary<string, string?> stateDictionary)
            {
                errorId = stateDictionary["ErrorId"];
                errorCategory = Enum.TryParse<ErrorCategory>(stateDictionary["ErrorCategory"], out var category) ? category : ErrorCategory.NotSpecified;
            }
            else
            {
                errorId = null;
                errorCategory = ErrorCategory.NotSpecified;
            }

            ErrorRecord error = new(new LDAPException(message), errorId, errorCategory, null);
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

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}